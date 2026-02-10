using UnityEngine;
using System.Collections.Generic;

public class CameraFollow : MonoBehaviour
{
    [Header("跟随目标")]
    [SerializeField] private Transform target; // 要跟随的目标（玩家）
    [SerializeField] private Vector3 offset = new Vector3(0, 0, -10); // 摄像头偏移量

    [Header("跟随设置")]
    [SerializeField] private float smoothTime = 0.2f; // 平滑时间（秒），值越小跟随越紧
    [SerializeField] private float maxSpeed = 20f; // 最大跟随速度
    [SerializeField] private bool useFixedUpdate = true; // 是否使用FixedUpdate

    [Header("摄像头比例")]
    [SerializeField] private bool useFixedAspectRatio = true; // 是否使用固定宽高比
    [SerializeField] private float aspectWidth = 4f; // 宽高比宽度
    [SerializeField] private float aspectHeight = 3f; // 宽高比高度
    [SerializeField] private bool forceResolutionInEditor = false; // 是否在编辑器中强制分辨率

    [Header("边界设置")]
    [SerializeField] private bool useBoundaries = true; // 是否启用边界限制

    [System.Serializable]
    public class CameraBoundary
    {
        public string boundaryName = "区域"; // 区域名称（方便识别）
        public float minX = -10f; // X轴最小值
        public float maxX = 10f; // X轴最大值
        public float minY = -5f; // Y轴最小值
        public float maxY = 5f; // Y轴最大值
        public bool isActive = true; // 是否激活此边界
        public bool useForNearestSearch = true; // 是否用于最近搜索

        // 可选：可以在Unity中显示一个颜色标识
        public Color debugColor = new Color(1f, 0.5f, 0f, 0.3f);

        // 计算边界中心点
        public Vector3 Center
        {
            get { return new Vector3((minX + maxX) / 2f, (minY + maxY) / 2f, 0); }
        }

        // 计算边界大小
        public Vector2 Size
        {
            get { return new Vector2(maxX - minX, maxY - minY); }
        }

        // 判断点是否在此边界内
        public bool Contains(Vector3 point)
        {
            return point.x >= minX && point.x <= maxX &&
                   point.y >= minY && point.y <= maxY;
        }

        // 计算点到边界边缘的最短距离（点在边界内时返回0）
        public float DistanceToEdge(Vector3 point)
        {
            // 如果点在边界内，距离为0
            if (Contains(point)) return 0f;

            // 计算点到边界每条边的距离，取最小值
            // 水平方向距离
            float distToLeft = Mathf.Abs(point.x - minX);
            float distToRight = Mathf.Abs(point.x - maxX);
            float minHorizontalDist = Mathf.Min(distToLeft, distToRight);

            // 垂直方向距离
            float distToBottom = Mathf.Abs(point.y - minY);
            float distToTop = Mathf.Abs(point.y - maxY);
            float minVerticalDist = Mathf.Min(distToBottom, distToTop);

            // 计算到边角的距离（对角线）
            float distToBottomLeft = Vector2.Distance(point, new Vector2(minX, minY));
            float distToBottomRight = Vector2.Distance(point, new Vector2(maxX, minY));
            float distToTopLeft = Vector2.Distance(point, new Vector2(minX, maxY));
            float distToTopRight = Vector2.Distance(point, new Vector2(maxX, maxY));
            float minCornerDist = Mathf.Min(distToBottomLeft, distToBottomRight, distToTopLeft, distToTopRight);

            // 返回最小值
            return Mathf.Min(minHorizontalDist, minVerticalDist, minCornerDist);
        }

        // 计算点到边界边缘的最近点
        public Vector3 GetClosestEdgePoint(Vector3 point)
        {
            // 如果点在边界内，返回该点（已经在边界内）
            if (Contains(point)) return point;

            // 限制到边界内
            float clampedX = Mathf.Clamp(point.x, minX, maxX);
            float clampedY = Mathf.Clamp(point.y, minY, maxY);

            return new Vector3(clampedX, clampedY, point.z);
        }

        // 将点限制在边界内
        public Vector3 ClampToBoundary(Vector3 point)
        {
            return new Vector3(
                Mathf.Clamp(point.x, minX, maxX),
                Mathf.Clamp(point.y, minY, maxY),
                point.z
            );
        }
    }

    [Header("边界区域")]
    [SerializeField] private CameraBoundary[] boundaries; // 多个边界区域

    [Header("边界切换设置")]
    [SerializeField] private bool autoSwitchToNearest = true; // 是否自动切换到最近边界

    [Header("边界过渡")]
    [SerializeField] private bool smoothBoundaryTransition = true; // 是否平滑过渡边界
    [SerializeField] private float transitionSmoothTime = 0.5f; // 边界过渡平滑时间

    [Header("调试选项")]
    [SerializeField] private bool showDebugGizmos = true; // 是否显示调试图形
    [SerializeField] private bool showDebugInfo = false; // 是否显示调试信息
    [SerializeField] private bool showActiveBoundary = true; // 是否高亮显示当前边界
    [SerializeField] private bool alwaysShowInEditor = true; // 是否在编辑模式下总是显示

    // 用于平滑跟随的私有变量
    private Vector3 velocity = Vector3.zero;
    private int currentBoundaryIndex = -1; // 当前使用的边界索引
    private Vector3 transitionStartPosition; // 过渡起始位置
    private bool isTransitioning = false; // 是否正在过渡
    private float transitionProgress = 0f; // 过渡进度

    void Start()
    {
        // 如果没有边界设置，创建一个默认边界
        if (boundaries == null || boundaries.Length == 0)
        {
            boundaries = new CameraBoundary[1];
            boundaries[0] = new CameraBoundary();
            boundaries[0].boundaryName = "默认边界";
            Debug.Log("摄像头创建了默认边界区域");
        }

        // 初始化摄像头位置到玩家位置
        if (target != null)
        {
            Vector3 startPosition = target.position + offset;

            // 寻找最近的边界（使用X轴距离）
            if (useBoundaries && autoSwitchToNearest)
            {
                int nearestIndex = FindNearestBoundaryIndexByXDistance(target.position);
                if (nearestIndex >= 0)
                {
                    currentBoundaryIndex = nearestIndex;
                    startPosition = boundaries[nearestIndex].ClampToBoundary(startPosition);
                }
            }

            transform.position = startPosition;
            transitionStartPosition = startPosition;
        }

        // 设置摄像头比例为4:3
        SetupCameraAspectRatio();
    }

    // 设置摄像头宽高比
    void SetupCameraAspectRatio()
    {
        if (!useFixedAspectRatio) return;

        // 获取摄像头组件
        Camera cam = GetComponent<Camera>();
        if (cam == null)
        {
            Debug.LogError("摄像头组件未找到！");
            return;
        }

        // 计算目标宽高比
        float targetAspect = aspectWidth / aspectHeight;

        // 获取当前视口的宽高比
        float windowAspect = (float)Screen.width / (float)Screen.height;

        // 计算缩放高度（letterbox）
        float scaleHeight = windowAspect / targetAspect;

        // 创建矩形用于设置摄像头视口
        Rect rect = cam.rect;

        if (scaleHeight < 1.0f)
        {
            // 如果窗口高度大于目标高度，添加黑边（letterbox）
            rect.width = 1.0f;
            rect.height = scaleHeight;
            rect.x = 0;
            rect.y = (1.0f - scaleHeight) / 2.0f;
        }
        else
        {
            // 如果窗口宽度大于目标宽度，添加黑边（pillarbox）
            float scaleWidth = 1.0f / scaleHeight;
            rect.width = scaleWidth;
            rect.height = 1.0f;
            rect.x = (1.0f - scaleWidth) / 2.0f;
            rect.y = 0;
        }

        cam.rect = rect;

        Debug.Log($"摄像头比例设置为 {aspectWidth}:{aspectHeight} (目标比例: {targetAspect:F2}, 窗口比例: {windowAspect:F2})");

        // 如果在编辑器中，并且需要强制分辨率
#if UNITY_EDITOR
        if (forceResolutionInEditor && !Application.isPlaying)
        {
            // 这里只是提示，实际分辨率设置需要在播放器中
            Debug.Log("在编辑器中，建议将游戏窗口设置为4:3比例以获得最佳效果");
        }
#endif
    }

    void FixedUpdate()
    {
        // 如果没有设置目标，尝试查找玩家标签的对象
        if (target == null)
        {
            target = GlobalPlayer.Instance?.Player?.transform;
        }

        // 如果启用了FixedUpdate同步，在这里处理摄像头跟随
        if (useFixedUpdate)
        {
            FollowTarget();
        }
    }

    void LateUpdate()
    {
        // 如果没有使用FixedUpdate，则在LateUpdate中处理
        if (!useFixedUpdate)
        {
            FollowTarget();
        }
    }

    void FollowTarget()
    {
        if (target == null) return;

        // 计算目标位置
        Vector3 desiredPosition = target.position + offset;

        // 如果需要，应用边界限制
        if (useBoundaries)
        {
            // 自动寻找最近边界（基于X轴距离）
            if (autoSwitchToNearest)
            {
                TrySwitchToNearestBoundaryByXDistance(target.position);
            }

            // 应用当前边界的限制
            if (currentBoundaryIndex >= 0 && currentBoundaryIndex < boundaries.Length)
            {
                CameraBoundary currentBoundary = boundaries[currentBoundaryIndex];

                // 如果正在边界过渡，进行平滑过渡
                if (isTransitioning && smoothBoundaryTransition)
                {
                    // 计算过渡位置
                    transitionProgress += Time.deltaTime / transitionSmoothTime;
                    transitionProgress = Mathf.Clamp01(transitionProgress);

                    // 计算当前边界限制的位置
                    Vector3 clampedPosition = currentBoundary.ClampToBoundary(desiredPosition);

                    // 从过渡起始位置平滑移动到目标位置
                    desiredPosition = Vector3.Lerp(transitionStartPosition, clampedPosition, transitionProgress);

                    // 过渡完成
                    if (transitionProgress >= 1f)
                    {
                        isTransitioning = false;
                    }
                }
                else
                {
                    // 直接应用边界限制
                    desiredPosition = currentBoundary.ClampToBoundary(desiredPosition);
                }
            }
        }

        // 使用SmoothDamp进行平滑跟随
        transform.position = Vector3.SmoothDamp(
            transform.position,
            desiredPosition,
            ref velocity,
            smoothTime,
            maxSpeed
        );
    }

    // 根据X轴距离寻找最近边界的索引
    int FindNearestBoundaryIndexByXDistance(Vector3 position)
    {
        int nearestIndex = -1;
        float nearestDistance = float.MaxValue;

        for (int i = 0; i < boundaries.Length; i++)
        {
            if (!boundaries[i].isActive || !boundaries[i].useForNearestSearch) continue;

            // 只计算X轴距离，忽略Y轴
            float distance = 0f;

            if (position.x < boundaries[i].minX)
            {
                // 在左边界的左侧
                distance = Mathf.Abs(position.x - boundaries[i].minX);
            }
            else if (position.x > boundaries[i].maxX)
            {
                // 在右边界的右侧
                distance = Mathf.Abs(position.x - boundaries[i].maxX);
            }
            else
            {
                // 在X轴范围内，距离为0
                distance = 0f;
            }

            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearestIndex = i;
            }
        }

        return nearestIndex;
    }

    // 尝试切换到最近边界（基于X轴距离）
    void TrySwitchToNearestBoundaryByXDistance(Vector3 playerPosition)
    {
        // 寻找最近边界
        int nearestIndex = FindNearestBoundaryIndexByXDistance(playerPosition);
        if (nearestIndex < 0 || nearestIndex == currentBoundaryIndex) return;

        // 切换到最近边界（无冷却时间，无阈值，直接切换）
        SwitchToBoundary(nearestIndex);
    }

    // 切换到指定边界
    public void SwitchToBoundary(int index)
    {
        if (index < 0 || index >= boundaries.Length) return;
        if (index == currentBoundaryIndex) return;

        Debug.Log($"摄像头切换到边界: {boundaries[index].boundaryName}");

        // 开始边界过渡
        if (smoothBoundaryTransition)
        {
            isTransitioning = true;
            transitionProgress = 0f;
            transitionStartPosition = transform.position - offset;
        }

        currentBoundaryIndex = index;
    }

    // 切换到指定边界（通过名称）
    public void SwitchToBoundary(string boundaryName)
    {
        for (int i = 0; i < boundaries.Length; i++)
        {
            if (boundaries[i].boundaryName == boundaryName)
            {
                SwitchToBoundary(i);
                return;
            }
        }
        Debug.LogError($"未找到名称为 '{boundaryName}' 的边界区域！");
    }

    // 强制切换到最近的边界（基于X轴距离）
    public void ForceSwitchToNearest()
    {
        if (target == null) return;

        int nearestIndex = FindNearestBoundaryIndexByXDistance(target.position);
        if (nearestIndex >= 0)
        {
            SwitchToBoundary(nearestIndex);
        }
    }

    // 获取当前边界
    public CameraBoundary GetCurrentBoundary()
    {
        if (currentBoundaryIndex >= 0 && currentBoundaryIndex < boundaries.Length)
        {
            return boundaries[currentBoundaryIndex];
        }
        return null;
    }

    // 检查目标是否在当前边界内
    public bool IsTargetInCurrentBoundary()
    {
        if (target == null || currentBoundaryIndex < 0) return false;
        if (currentBoundaryIndex >= boundaries.Length) return false;

        return boundaries[currentBoundaryIndex].Contains(target.position);
    }

    // 获取目标到当前边界的X轴距离
    public float GetXDistanceToCurrentBoundary()
    {
        if (target == null || currentBoundaryIndex < 0) return float.MaxValue;
        if (currentBoundaryIndex >= boundaries.Length) return float.MaxValue;

        CameraBoundary boundary = boundaries[currentBoundaryIndex];

        if (target.position.x < boundary.minX)
        {
            return Mathf.Abs(target.position.x - boundary.minX);
        }
        else if (target.position.x > boundary.maxX)
        {
            return Mathf.Abs(target.position.x - boundary.maxX);
        }
        else
        {
            return 0f;
        }
    }

    // 激活一个边界
    public void ActivateBoundary(int index)
    {
        if (index >= 0 && index < boundaries.Length)
        {
            boundaries[index].isActive = true;
        }
    }

    // 取消激活一个边界
    public void DeactivateBoundary(int index)
    {
        if (index >= 0 && index < boundaries.Length)
        {
            boundaries[index].isActive = false;

            // 如果当前边界被取消激活，切换到最近边界
            if (index == currentBoundaryIndex)
            {
                ForceSwitchToNearest();
            }
        }
    }

    // 设置边界是否用于最近搜索
    public void SetBoundarySearchable(int index, bool searchable)
    {
        if (index >= 0 && index < boundaries.Length)
        {
            boundaries[index].useForNearestSearch = searchable;
        }
    }

    // 添加新的边界区域
    public void AddBoundary(CameraBoundary newBoundary)
    {
        System.Array.Resize(ref boundaries, boundaries.Length + 1);
        boundaries[boundaries.Length - 1] = newBoundary;
    }

    // 移除指定索引的边界区域
    public void RemoveBoundary(int index)
    {
        if (index >= 0 && index < boundaries.Length)
        {
            // 如果是当前边界，先切换到其他边界
            if (index == currentBoundaryIndex)
            {
                ForceSwitchToNearest();
                currentBoundaryIndex = -1;
            }

            // 从边界数组中移除
            for (int i = index; i < boundaries.Length - 1; i++)
            {
                boundaries[i] = boundaries[i + 1];
            }
            System.Array.Resize(ref boundaries, boundaries.Length - 1);
        }
    }

    // 在Scene视图中绘制调试图形（无论是否选中都会绘制）
    void OnDrawGizmos()
    {
        if (!showDebugGizmos || boundaries == null) return;
        if (!alwaysShowInEditor && !Application.isPlaying) return;

        DrawAllBoundaries();
    }

    // 选中时绘制更详细的调试图形
    void OnDrawGizmosSelected()
    {
        if (!showDebugGizmos || boundaries == null) return;

        DrawAllBoundaries(true);
    }

    // 绘制所有边界
    void DrawAllBoundaries(bool isSelected = false)
    {
        for (int i = 0; i < boundaries.Length; i++)
        {
            CameraBoundary boundary = boundaries[i];
            bool isActive = boundary.isActive;
            bool isCurrent = (i == currentBoundaryIndex) && Application.isPlaying;

            // 设置颜色
            Color fillColor = boundary.debugColor;
            Color borderColor = boundary.debugColor * 1.5f;

            // 编辑模式下区分激活状态
            if (!Application.isPlaying)
            {
                fillColor = isActive ? boundary.debugColor : boundary.debugColor * 0.5f;
                borderColor = isActive ? boundary.debugColor * 1.5f : boundary.debugColor * 0.7f;
            }
            else if (!isActive)
            {
                fillColor *= 0.3f;
                borderColor *= 0.3f;
            }
            else if (isCurrent && showActiveBoundary)
            {
                fillColor = new Color(0f, 1f, 0f, 0.3f);
                borderColor = Color.green;
            }

            Gizmos.color = fillColor;

            // 计算边界中心点和大小
            Vector3 center = boundary.Center;
            Vector3 size = new Vector3(boundary.Size.x, boundary.Size.y, 0.1f);

            // 绘制立方体表示边界
            Gizmos.DrawCube(center, size);

            // 绘制边框
            Gizmos.color = borderColor;
            Gizmos.DrawWireCube(center, size);

            // 绘制中心点
            if (Application.isPlaying && isActive)
            {
                Gizmos.color = isCurrent ? Color.green : Color.yellow;
                Gizmos.DrawSphere(center, 0.2f);
            }

            // 在编辑模式下，绘制坐标轴辅助线
            if (!Application.isPlaying)
            {
                DrawBoundaryHelperLines(boundary, isActive);
            }

            // 显示边界名称
#if UNITY_EDITOR
            string label = $"{boundary.boundaryName} (Index: {i})";
            if (!Application.isPlaying)
            {
                label += $" [{(isActive ? "激活" : "未激活")}]";
            }
            else
            {
                if (!isActive) label += " [未激活]";
                if (isCurrent) label += " [当前]";
            }

            UnityEditor.Handles.Label(
                new Vector3(center.x, boundary.maxY + 0.5f, 0),
                label
            );

            // 显示边界范围
            UnityEditor.Handles.Label(
                new Vector3(center.x, boundary.maxY + 0.8f, 0),
                $"X:{boundary.minX:F1}~{boundary.maxX:F1} Y:{boundary.minY:F1}~{boundary.maxY:F1}",
                new GUIStyle() { fontSize = 9 }
            );
#endif
        }

        // 绘制当前边界到目标的X轴距离（仅在运行时）
        if (Application.isPlaying && target != null && currentBoundaryIndex >= 0 && currentBoundaryIndex < boundaries.Length && isSelected)
        {
            CameraBoundary currentBoundary = boundaries[currentBoundaryIndex];

            // 计算X轴上的最近点
            Vector3 xClosestPoint;
            if (target.position.x < currentBoundary.minX)
            {
                xClosestPoint = new Vector3(currentBoundary.minX, target.position.y, 0);
            }
            else if (target.position.x > currentBoundary.maxX)
            {
                xClosestPoint = new Vector3(currentBoundary.maxX, target.position.y, 0);
            }
            else
            {
                xClosestPoint = target.position;
            }

            Gizmos.color = Color.red;
            Gizmos.DrawLine(target.position, xClosestPoint);
            Gizmos.DrawWireSphere(xClosestPoint, 0.3f);

#if UNITY_EDITOR
            float xDistance = GetXDistanceToCurrentBoundary();
            UnityEditor.Handles.Label(
                (target.position + xClosestPoint) / 2f,
                $"X轴距离: {xDistance:F1}",
                new GUIStyle() { normal = new GUIStyleState() { textColor = Color.red } }
            );
#endif
        }
    }

    // 绘制边界辅助线
    void DrawBoundaryHelperLines(CameraBoundary boundary, bool isActive)
    {
        Color helperColor = isActive ? Color.white * 0.8f : Color.gray * 0.5f;
        Gizmos.color = helperColor;

        // 绘制边界的四个角
        float cornerSize = 0.2f;
        Vector3 bottomLeft = new Vector3(boundary.minX, boundary.minY, 0);
        Vector3 bottomRight = new Vector3(boundary.maxX, boundary.minY, 0);
        Vector3 topLeft = new Vector3(boundary.minX, boundary.maxY, 0);
        Vector3 topRight = new Vector3(boundary.maxX, boundary.maxY, 0);

        Gizmos.DrawSphere(bottomLeft, cornerSize);
        Gizmos.DrawSphere(bottomRight, cornerSize);
        Gizmos.DrawSphere(topLeft, cornerSize);
        Gizmos.DrawSphere(topRight, cornerSize);

        // 绘制边界的中心十字线
        Vector3 center = boundary.Center;
        float crossSize = Mathf.Min(boundary.Size.x, boundary.Size.y) * 0.2f;

        Gizmos.DrawLine(
            new Vector3(center.x - crossSize, center.y, 0),
            new Vector3(center.x + crossSize, center.y, 0)
        );

        Gizmos.DrawLine(
            new Vector3(center.x, center.y - crossSize, 0),
            new Vector3(center.x, center.y + crossSize, 0)
        );
    }

    // 在游戏运行时显示调试信息
    void OnGUI()
    {
        if (!showDebugInfo) return;

        GUI.skin.label.fontSize = 12;
        GUI.contentColor = Color.white;

        string info = "摄像头调试信息:\n";
        info += $"位置: {transform.position:F2}\n";
        info += $"目标: {(target != null ? target.name : "无")}\n";
        info += $"平滑时间: {smoothTime:F2}\n";
        info += $"最大速度: {maxSpeed:F1}\n";
        info += $"自动切换: {autoSwitchToNearest}\n";
        info += $"当前边界: {(currentBoundaryIndex >= 0 ? boundaries[currentBoundaryIndex].boundaryName : "无")}\n";

        if (target != null && currentBoundaryIndex >= 0)
        {
            info += $"目标在边界内: {IsTargetInCurrentBoundary()}\n";
            info += $"到边界X轴距离: {GetXDistanceToCurrentBoundary():F2}\n";

            // 显示最近边界信息
            int nearestIndex = FindNearestBoundaryIndexByXDistance(target.position);
            if (nearestIndex >= 0 && nearestIndex != currentBoundaryIndex)
            {
                float nearestDistance = GetXDistanceToBoundary(target.position, nearestIndex);
                info += $"最近边界: {boundaries[nearestIndex].boundaryName} (X轴距离: {nearestDistance:F2})\n";
            }
        }

        info += $"边界数量: {boundaries.Length}\n";

        // 显示摄像头比例信息
        Camera cam = GetComponent<Camera>();
        if (cam != null)
        {
            info += $"摄像头比例: {(useFixedAspectRatio ? $"{aspectWidth}:{aspectHeight}" : "自适应")}\n";
            info += $"视口矩形: {cam.rect}\n";
        }

        GUI.Label(new Rect(10, 10, 350, 280), info);
    }

    // 辅助方法：获取目标到指定边界的X轴距离
    private float GetXDistanceToBoundary(Vector3 position, int boundaryIndex)
    {
        if (boundaryIndex < 0 || boundaryIndex >= boundaries.Length) return float.MaxValue;

        CameraBoundary boundary = boundaries[boundaryIndex];

        if (position.x < boundary.minX)
        {
            return Mathf.Abs(position.x - boundary.minX);
        }
        else if (position.x > boundary.maxX)
        {
            return Mathf.Abs(position.x - boundary.maxX);
        }
        else
        {
            return 0f;
        }
    }

    // 属性访问器
    public Transform Target
    {
        get { return target; }
        set { target = value; }
    }

    public float SmoothTime
    {
        get { return smoothTime; }
        set { smoothTime = Mathf.Clamp(value, 0.01f, 1f); }
    }

    public float MaxSpeed
    {
        get { return maxSpeed; }
        set { maxSpeed = Mathf.Max(value, 1f); }
    }

    public bool UseBoundaries
    {
        get { return useBoundaries; }
        set { useBoundaries = value; }
    }

    public bool UseFixedUpdate
    {
        get { return useFixedUpdate; }
        set { useFixedUpdate = value; }
    }

    public bool AutoSwitchToNearest
    {
        get { return autoSwitchToNearest; }
        set { autoSwitchToNearest = value; }
    }

    public int CurrentBoundaryIndex
    {
        get { return currentBoundaryIndex; }
        set { SwitchToBoundary(value); }
    }

    public CameraBoundary[] Boundaries
    {
        get { return boundaries; }
        set { boundaries = value; }
    }

    public bool UseFixedAspectRatio
    {
        get { return useFixedAspectRatio; }
        set
        {
            useFixedAspectRatio = value;
            if (value) SetupCameraAspectRatio();
        }
    }

    public float AspectWidth
    {
        get { return aspectWidth; }
        set
        {
            aspectWidth = Mathf.Max(1f, value);
            if (useFixedAspectRatio) SetupCameraAspectRatio();
        }
    }

    public float AspectHeight
    {
        get { return aspectHeight; }
        set
        {
            aspectHeight = Mathf.Max(1f, value);
            if (useFixedAspectRatio) SetupCameraAspectRatio();
        }
    }
}