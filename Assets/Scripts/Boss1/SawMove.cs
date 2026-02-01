using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SawMove : MonoBehaviour
{
    // Boss状态枚举
    private enum BossState
    {
        FirstAction,          // 第一个动作：向下移动
        PauseBetweenActions,  // 第一个到第二个动作间停顿
        SecondAction,         // 第二个动作：向左移动并持续释放火花
        PauseBeforeChoice,    // 第二个动作后的停顿（等待选择）
        ThirdAction,          // 第三个动作：向下匀速移动
        FourthAction,         // 第四个动作：加速返回并释放火花
        PauseAfterFourth,     // 第四个动作后停顿
        FifthAction,          // 第五个动作：瞬移然后返回
        Idle                  // 空闲状态
    }

    [Header("旋转设置")]
    [SerializeField] private float rotationSpeed = 180f; // 旋转速度

    [Header("动作间停顿")]
    [SerializeField] private float pauseBetweenActions = 1.0f; // 动作间停顿时间
    [SerializeField] private float pauseBeforeThird = 0.5f; // 第三个动作前停顿时间
    [SerializeField] private float pauseBeforeFourth = 0.8f; // 第四个动作前停顿时间
    [SerializeField] private float pauseAfterFourth = 1.0f; // 第四个动作后停顿时间

    [Header("第一个动作设置")]
    [SerializeField] private float moveDistance = 5f; // 向下移动的距离
    [SerializeField] private float acceleration = 2f; // 向下加速度
    [SerializeField] private AnimationCurve speedCurve; // 速度曲线（可选）

    [Header("第二个动作设置")]
    [SerializeField] private float secondMoveDistance = 10f; // 向左移动的距离
    [SerializeField] private float secondInitialSpeed = 8f; // 初始速度（也是结束速度）
    [SerializeField] private float secondMaxSpeed = 15f; // 最高速度
    [SerializeField] private AnimationCurve secondSpeedCurve; // 第二个动作的速度曲线

    [Header("第三个动作设置")]
    [SerializeField] private float thirdMoveDistance = 4f; // 向下匀速移动的距离
    [SerializeField] private float thirdMoveSpeed = 6f; // 向下匀速移动的速度

    [Header("第四个动作设置")]
    [SerializeField] private float fourthAcceleration = 3f; // 第四个动作的加速度
    [SerializeField] private float fourthMaxSpeed = 12f; // 第四个动作的最大速度

    [Header("第五个动作设置")]
    [SerializeField] private float fifthTeleportDistance = 15f; // 瞬移到起始点左边的距离
    [SerializeField] private float fifthReturnSpeed = 10f; // 返回起始点的速度

    [Header("火花设置")]
    [SerializeField] private GameObject sparkPrefab; // 火花预制体
    [SerializeField] private int sparkPoolSize = 80; // 对象池大小
    [SerializeField] private float sparkLifetime = 2f; // 火花生命周期

    [Header("火花生成半径设置")]
    [SerializeField] private float firstActionSparkRadius = 10f; // 第一个动作火花生成半径
    [SerializeField] private float secondActionSparkRadius = 10f; // 第二个动作火花生成半径
    [SerializeField] private float fourthActionSparkRadius = 10f; // 第四个动作火花生成半径
    [SerializeField] private float fourthEndSparkRadius = 10f; // 第四个动作结尾火花生成半径

    [Header("第一个动作火花设置")]
    [SerializeField] private int sparkCountOnHit = 15; // 触地时生成的火花数量
    [SerializeField] private float minSparkForce = 5f; // 火花最小喷射力
    [SerializeField] private float maxSparkForce = 12f; // 火花最大喷射力
    [SerializeField] private float sparkUpwardBias = 0.3f; // 火花向上偏置（0-1，越高越向上）

    [Header("第二个动作火花设置")]
    [SerializeField] private int sparksPerFrame = 3; // 每帧释放的火花数量
    [SerializeField] private float secondMinSparkForce = 3f; // 第二个动作火花最小力
    [SerializeField] private float secondMaxSparkForce = 8f; // 第二个动作火花最大力
    [SerializeField] private float secondSparkUpwardBias = 0.1f; // 第二个动作火花向上偏置

    [Header("第四个动作火花设置")]
    [SerializeField] private int fourthSparksPerFrame = 4; // 第四个动作每帧释放的火花数量
    [SerializeField] private float fourthMinSparkForce = 4f; // 第四个动作火花最小力
    [SerializeField] private float fourthMaxSparkForce = 10f; // 第四个动作火花最大力
    [SerializeField] private float fourthSparkUpwardBias = 0.2f; // 第四个动作火花向上偏置

    [Header("第四个动作结尾火花设置")]
    [SerializeField] private int fourthEndSparkCount = 20; // 第四个动作结尾爆出的火花数量
    [SerializeField] private float fourthEndMinSparkForce = 6f; // 第四个动作结尾火花最小力
    [SerializeField] private float fourthEndMaxSparkForce = 14f; // 第四个动作结尾火花最大力
    [SerializeField] private float fourthEndSparkUpwardBias = 0.4f; // 第四个动作结尾火花向上偏置

    // 私有变量
    private Vector3 startPosition; // 起始位置
    private Vector3 firstActionEndPosition; // 第一个动作结束位置
    private Vector3 secondActionEndPosition; // 第二个动作结束位置
    private Vector3 thirdActionEndPosition; // 第三个动作结束位置
    private Vector3 fifthTeleportPosition; // 第五个动作瞬移位置
    private Queue<GameObject> sparkPool; // 对象池队列
    private BossState currentState = BossState.FirstAction;
    private float currentSpeed = 0f; // 当前移动速度
    private float movedDistance = 0f; // 已移动距离
    private float secondMovedDistance = 0f; // 第二个动作已移动距离
    private float thirdMovedDistance = 0f; // 第三个动作已移动距离
    private float fourthMovedDistance = 0f; // 第四个动作已移动距离
    private float fifthMovedDistance = 0f; // 第五个动作已移动距离
    private float pauseTimer = 0f; // 停顿计时器
    private float currentPauseTime = 0f; // 当前停顿时间
    private BossState nextStateAfterPause = BossState.FirstAction; // 停顿后进入的状态
    private bool isReturning = false; // 第四个动作是否正在返回
    private float fourthCurrentSpeed = 0f; // 第四个动作当前速度
    private bool hasExecutedFourthAction = false; // 标记是否执行过第四个动作

    void Start()
    {
        // 记录起始位置
        startPosition = transform.position;

        // 初始化对象池
        InitializeSparkPool();

        // 开始第一个动作
        StartFirstAction();
    }

    void Update()
    {
        // 持续旋转（所有状态都旋转）
        RotateContinuously();

        // 根据状态执行不同的行为
        switch (currentState)
        {
            case BossState.FirstAction:
                UpdateFirstAction();
                break;

            case BossState.PauseBetweenActions:
                UpdatePause();
                break;

            case BossState.SecondAction:
                UpdateSecondAction();
                break;

            case BossState.PauseBeforeChoice:
                UpdatePauseBeforeChoice();
                break;

            case BossState.ThirdAction:
                UpdateThirdAction();
                break;

            case BossState.FourthAction:
                UpdateFourthAction();
                break;

            case BossState.PauseAfterFourth:
                UpdatePauseAfterFourth();
                break;

            case BossState.FifthAction:
                UpdateFifthAction();
                break;

            case BossState.Idle:
                // 空闲状态，不执行移动
                break;
        }
    }

    // 初始化火花对象池
    private void InitializeSparkPool()
    {
        sparkPool = new Queue<GameObject>();

        for (int i = 0; i < sparkPoolSize; i++)
        {
            GameObject spark = Instantiate(sparkPrefab, Vector3.zero, Quaternion.identity);
            spark.SetActive(false);

            // 为火花添加自动回收脚本
            SparkAutoReturn sparkScript = spark.GetComponent<SparkAutoReturn>();
            if (sparkScript == null)
            {
                sparkScript = spark.AddComponent<SparkAutoReturn>();
            }
            sparkScript.Initialize(this, sparkLifetime);

            sparkPool.Enqueue(spark);
        }
    }

    // 从对象池获取火花
    private GameObject GetSparkFromPool()
    {
        if (sparkPool.Count > 0)
        {
            GameObject spark = sparkPool.Dequeue();
            spark.SetActive(true);
            return spark;
        }
        else
        {
            // 如果对象池为空，创建新实例
            GameObject spark = Instantiate(sparkPrefab, Vector3.zero, Quaternion.identity);

            // 为火花添加自动回收脚本
            SparkAutoReturn sparkScript = spark.AddComponent<SparkAutoReturn>();
            sparkScript.Initialize(this, sparkLifetime);

            return spark;
        }
    }

    // 回收火花到对象池
    public void ReturnSparkToPool(GameObject spark)
    {
        if (spark != null && sparkPool != null)
        {
            spark.SetActive(false);
            sparkPool.Enqueue(spark);
        }
    }

    // 持续旋转
    private void RotateContinuously()
    {
        transform.Rotate(0, 0, rotationSpeed * Time.deltaTime);
    }

    // 开始第一个动作
    private void StartFirstAction()
    {
        currentState = BossState.FirstAction;
        currentSpeed = 0f;
        movedDistance = 0f;
    }

    // 更新第一个动作
    private void UpdateFirstAction()
    {
        // 计算加速度（可以使用曲线或线性加速）
        float accelerationValue = acceleration;
        if (speedCurve != null && speedCurve.keys.Length > 0)
        {
            accelerationValue = speedCurve.Evaluate(movedDistance / moveDistance) * acceleration;
        }

        // 增加速度
        currentSpeed += accelerationValue * Time.deltaTime;

        // 计算本次帧移动距离
        float moveThisFrame = currentSpeed * Time.deltaTime;

        // 更新位置
        transform.Translate(0, -moveThisFrame, 0, Space.World);

        // 更新已移动距离
        movedDistance += moveThisFrame;

        // 检查是否到达终点
        if (movedDistance >= moveDistance)
        {
            // 确保精确到达终点
            transform.position = new Vector3(
                startPosition.x,
                startPosition.y - moveDistance,
                startPosition.z
            );

            // 记录第一个动作结束位置
            firstActionEndPosition = transform.position;

            // 停止移动并生成火花
            ReachedFirstActionEnd();
        }
    }

    // 第一个动作到达终点时的处理
    private void ReachedFirstActionEnd()
    {
        // 立即生成所有火花
        GenerateFirstActionSparks();

        // 进入停顿状态
        StartPause(pauseBetweenActions, BossState.SecondAction);
    }

    // 开始停顿
    private void StartPause(float pauseTime, BossState nextState)
    {
        if (nextState == BossState.PauseBetweenActions)
        {
            currentState = BossState.PauseBetweenActions;
        }
        else if (nextState == BossState.PauseAfterFourth)
        {
            currentState = BossState.PauseAfterFourth;
        }
        else
        {
            currentState = BossState.PauseBetweenActions;
        }

        pauseTimer = 0f;
        currentPauseTime = pauseTime;
        nextStateAfterPause = nextState;
    }

    // 更新停顿状态
    private void UpdatePause()
    {
        pauseTimer += Time.deltaTime;

        // 检查停顿时间是否结束
        if (pauseTimer >= currentPauseTime)
        {
            // 停顿结束，进入下一个状态
            currentState = nextStateAfterPause;

            // 根据下一个状态初始化
            switch (nextStateAfterPause)
            {
                case BossState.SecondAction:
                    StartSecondAction();
                    break;
                case BossState.ThirdAction:
                    StartThirdAction();
                    break;
                case BossState.FourthAction:
                    StartFourthAction();
                    break;
                case BossState.FifthAction:
                    StartFifthAction();
                    break;
            }
        }
    }

    // 更新第四个动作后的停顿
    private void UpdatePauseAfterFourth()
    {
        pauseTimer += Time.deltaTime;

        // 检查停顿时间是否结束
        if (pauseTimer >= currentPauseTime)
        {
            // 停顿结束，重新开始第二个动作
            StartSecondAction();
        }
    }

    // 开始停顿选择
    private void StartPauseBeforeChoice()
    {
        currentState = BossState.PauseBeforeChoice;
        pauseTimer = 0f;

        // 根据是否执行过第四个动作来决定选择
        if (!hasExecutedFourthAction)
        {
            // 50%概率选择第三个或第四个动作
            float randomValue = Random.value;

            if (randomValue < 0.5f)
            {
                // 选择第三个动作
                currentPauseTime = pauseBeforeThird;
                nextStateAfterPause = BossState.ThirdAction;
            }
            else
            {
                // 选择第四个动作
                currentPauseTime = pauseBeforeFourth;
                nextStateAfterPause = BossState.FourthAction;
            }
        }
        else
        {
            // 已经执行过第四个动作，这次直接选择第三个动作
            currentPauseTime = pauseBeforeThird;
            nextStateAfterPause = BossState.ThirdAction;
        }
    }

    // 更新停顿选择状态
    private void UpdatePauseBeforeChoice()
    {
        pauseTimer += Time.deltaTime;

        // 检查停顿时间是否结束
        if (pauseTimer >= currentPauseTime)
        {
            // 停顿结束，进入选择的状态
            currentState = nextStateAfterPause;

            // 根据下一个状态初始化
            switch (nextStateAfterPause)
            {
                case BossState.ThirdAction:
                    StartThirdAction();
                    break;
                case BossState.FourthAction:
                    StartFourthAction();
                    break;
            }
        }
    }

    // 生成第一个动作的火花（瞬间全部迸发）
    private void GenerateFirstActionSparks()
    {
        for (int i = 0; i < sparkCountOnHit; i++)
        {
            SpawnSpark(minSparkForce, maxSparkForce, sparkUpwardBias, firstActionSparkRadius);
        }
    }

    // 生成第四个动作结尾的火花（瞬间全部迸发）
    private void GenerateFourthActionEndSparks()
    {
        for (int i = 0; i < fourthEndSparkCount; i++)
        {
            SpawnSpark(fourthEndMinSparkForce, fourthEndMaxSparkForce, fourthEndSparkUpwardBias, fourthEndSparkRadius);
        }
    }

    // 开始第二个动作
    private void StartSecondAction()
    {
        currentState = BossState.SecondAction;
        secondMovedDistance = 0f;

        // 计算第二个动作结束位置（向左移动）
        secondActionEndPosition = firstActionEndPosition + new Vector3(-secondMoveDistance, 0, 0);
    }

    // 更新第二个动作
    private void UpdateSecondAction()
    {
        // 计算当前速度（使用曲线控制，先快后慢）
        float normalizedTime = secondMovedDistance / secondMoveDistance;
        float speedMultiplier = 1f;

        if (secondSpeedCurve != null && secondSpeedCurve.keys.Length > 0)
        {
            speedMultiplier = secondSpeedCurve.Evaluate(normalizedTime);
        }
        else
        {
            // 默认曲线：先快后慢
            if (normalizedTime < 0.5f)
            {
                speedMultiplier = Mathf.Lerp(secondInitialSpeed, secondMaxSpeed, normalizedTime * 2f) / secondMaxSpeed;
            }
            else
            {
                speedMultiplier = Mathf.Lerp(secondMaxSpeed, secondInitialSpeed, (normalizedTime - 0.5f) * 2f) / secondMaxSpeed;
            }
        }

        float currentSecondSpeed = secondMaxSpeed * speedMultiplier;

        // 计算本次帧移动距离
        float moveThisFrame = currentSecondSpeed * Time.deltaTime;

        // 更新位置（向左移动）
        transform.Translate(-moveThisFrame, 0, 0, Space.World);

        // 更新已移动距离
        secondMovedDistance += moveThisFrame;

        // 每帧释放火花（在指定半径的圆内随机生成）
        for (int i = 0; i < sparksPerFrame; i++)
        {
            SpawnSpark(secondMinSparkForce, secondMaxSparkForce, secondSparkUpwardBias, secondActionSparkRadius);
        }

        // 检查是否到达终点
        if (secondMovedDistance >= secondMoveDistance)
        {
            // 确保精确到达终点
            transform.position = secondActionEndPosition;

            // 进入选择前的停顿
            StartPauseBeforeChoice();
        }
    }

    // 开始第三个动作
    private void StartThirdAction()
    {
        currentState = BossState.ThirdAction;
        thirdMovedDistance = 0f;

        // 计算第三个动作结束位置（向下移动）
        thirdActionEndPosition = transform.position + new Vector3(0, -thirdMoveDistance, 0);
    }

    // 更新第三个动作
    private void UpdateThirdAction()
    {
        // 计算本次帧移动距离（匀速）
        float moveThisFrame = thirdMoveSpeed * Time.deltaTime;

        // 更新位置（向下移动）
        transform.Translate(0, -moveThisFrame, 0, Space.World);

        // 更新已移动距离
        thirdMovedDistance += moveThisFrame;

        // 检查是否到达终点
        if (thirdMovedDistance >= thirdMoveDistance)
        {
            // 确保精确到达终点
            transform.position = thirdActionEndPosition;

            // 无需间隔，立即开始第五个动作
            StartFifthAction();
        }
    }

    // 开始第四个动作
    private void StartFourthAction()
    {
        currentState = BossState.FourthAction;
        fourthMovedDistance = 0f;
        fourthCurrentSpeed = 0f;
        isReturning = true;

        // 标记已执行过第四个动作
        hasExecutedFourthAction = true;
    }

    // 更新第四个动作
    private void UpdateFourthAction()
    {
        if (isReturning)
        {
            // 计算加速度
            fourthCurrentSpeed += fourthAcceleration * Time.deltaTime;

            // 限制最大速度
            if (fourthCurrentSpeed > fourthMaxSpeed)
            {
                fourthCurrentSpeed = fourthMaxSpeed;
            }

            // 计算本次帧移动距离
            float moveThisFrame = fourthCurrentSpeed * Time.deltaTime;

            // 更新位置（向右移动，返回第二个动作开始的位置）
            // 注意：第二个动作是向左移动，所以返回是向右移动
            transform.Translate(moveThisFrame, 0, 0, Space.World);

            // 更新已移动距离
            fourthMovedDistance += moveThisFrame;

            // 每帧释放火花（在指定半径的圆内随机生成）
            for (int i = 0; i < fourthSparksPerFrame; i++)
            {
                SpawnSpark(fourthMinSparkForce, fourthMaxSparkForce, fourthSparkUpwardBias, fourthActionSparkRadius);
            }

            // 检查是否到达终点（返回到第二个动作开始的位置）
            if (fourthMovedDistance >= secondMoveDistance)
            {
                // 确保精确到达终点
                transform.position = firstActionEndPosition;

                // 生成第四个动作结尾的火花效果
                GenerateFourthActionEndSparks();

                // 进入第四个动作后的停顿
                currentState = BossState.PauseAfterFourth;
                pauseTimer = 0f;
                currentPauseTime = pauseAfterFourth;
            }
        }
    }

    // 开始第五个动作
    private void StartFifthAction()
    {
        currentState = BossState.FifthAction;
        fifthMovedDistance = 0f;

        // 计算瞬移位置：起始位置左边的指定距离
        fifthTeleportPosition = startPosition + new Vector3(-fifthTeleportDistance, 0, 0);

        // 立即瞬移到指定位置
        transform.position = fifthTeleportPosition;
    }

    // 更新第五个动作
    private void UpdateFifthAction()
    {
        // 计算当前位置到起始位置的距离
        float distanceToStart = Vector3.Distance(transform.position, startPosition);

        if (distanceToStart > 0.01f)
        {
            // 计算移动方向
            Vector3 direction = (startPosition - transform.position).normalized;

            // 计算本次帧移动距离
            float moveThisFrame = fifthReturnSpeed * Time.deltaTime;

            // 更新位置
            transform.Translate(direction * moveThisFrame, Space.World);

            // 更新已移动距离
            fifthMovedDistance += moveThisFrame;
        }
        else
        {
            // 确保精确到达起始位置
            transform.position = startPosition;

            // 第五个动作完成，重置状态并重新开始第一个动作，形成循环
            hasExecutedFourthAction = false; // 重置标记
            StartFirstAction();
        }
    }

    // 生成单个火花（在指定半径的圆内随机位置生成）
    private void SpawnSpark(float minForce, float maxForce, float upwardBias, float spawnRadius)
    {
        // 从对象池获取火花
        GameObject spark = GetSparkFromPool();

        // 在半径为spawnRadius的圆内随机生成位置
        // 生成随机角度和随机半径
        float randomAngle = Random.Range(0f, 360f) * Mathf.Deg2Rad;
        float randomRadius = Random.Range(0f, spawnRadius);

        // 计算随机偏移
        Vector3 randomOffset = new Vector3(
            Mathf.Cos(randomAngle) * randomRadius,
            Mathf.Sin(randomAngle) * randomRadius,
            0
        );

        // 设置火花位置
        spark.transform.position = transform.position + randomOffset;

        // 获取或添加Rigidbody2D组件
        Rigidbody2D rb = spark.GetComponent<Rigidbody2D>();
        if (rb == null)
        {
            rb = spark.AddComponent<Rigidbody2D>();
            rb.gravityScale = 1f; // 确保有重力
        }

        // 重置火花的速度和旋转
        rb.velocity = Vector2.zero;
        rb.angularVelocity = 0f;

        // 随机方向（带向上偏置）
        Vector2 direction = GetRandomDirectionWithUpwardBias(upwardBias);

        // 随机力大小
        float randomForce = Random.Range(minForce, maxForce);

        // 添加力
        rb.AddForce(direction * randomForce, ForceMode2D.Impulse);

        // 添加随机旋转扭矩
        rb.AddTorque(Random.Range(-100f, 100f));
    }

    // 获取带向上偏置的随机方向
    private Vector2 GetRandomDirectionWithUpwardBias(float upwardBias)
    {
        // 基础随机角度
        float angle;

        // 根据向上偏置调整角度分布
        if (upwardBias > 0)
        {
            // 使用加权随机，使更多火花向上方飞出
            float bias = Mathf.Clamp01(upwardBias);
            float randomValue = Random.value;

            if (randomValue < bias)
            {
                // 偏置范围内，角度集中在向上方向（-45°到45°）
                angle = Random.Range(-45f, 45f);
            }
            else
            {
                // 非偏置范围，全角度随机
                angle = Random.Range(0f, 360f);
            }
        }
        else
        {
            // 无偏置，全角度随机
            angle = Random.Range(0f, 360f);
        }

        // 转换为方向向量
        return new Vector2(
            Mathf.Cos(angle * Mathf.Deg2Rad),
            Mathf.Sin(angle * Mathf.Deg2Rad)
        );
    }

    // 重置到起始位置（可选，用于重复使用）
    public void ResetToStart()
    {
        transform.position = startPosition;
        currentState = BossState.FirstAction;
        hasExecutedFourthAction = false;
        StartFirstAction();
    }

    // 公共方法：外部修改旋转速度
    public void SetRotationSpeed(float newSpeed)
    {
        rotationSpeed = newSpeed;
    }

    // 公共方法：外部修改停顿时间
    public void SetPauseBetweenActions(float newPauseTime)
    {
        pauseBetweenActions = newPauseTime;
    }

    // 公共方法：外部修改第四个动作结尾火花数量
    public void SetFourthEndSparkCount(int newCount)
    {
        fourthEndSparkCount = newCount;
    }

    // 公共方法：外部修改第五个动作瞬移距离
    public void SetFifthTeleportDistance(float newDistance)
    {
        fifthTeleportDistance = newDistance;
    }

    // 公共方法：外部修改第一个动作火花生成半径
    public void SetFirstActionSparkRadius(float newRadius)
    {
        firstActionSparkRadius = newRadius;
    }

    // 公共方法：外部修改第二个动作火花生成半径
    public void SetSecondActionSparkRadius(float newRadius)
    {
        secondActionSparkRadius = newRadius;
    }

    // 公共方法：外部修改第四个动作火花生成半径
    public void SetFourthActionSparkRadius(float newRadius)
    {
        fourthActionSparkRadius = newRadius;
    }

    // 公共方法：外部修改第四个动作结尾火花生成半径
    public void SetFourthEndSparkRadius(float newRadius)
    {
        fourthEndSparkRadius = newRadius;
    }

    // 在编辑器中绘制移动路径和火花生成半径（可视化辅助）
    void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.red;

        Vector3 currentPos = Application.isPlaying ? startPosition : transform.position;
        Vector3 firstEndPos = currentPos - new Vector3(0, moveDistance, 0);

        // 绘制第一个动作路径
        Gizmos.DrawSphere(currentPos, 0.2f);
        Gizmos.DrawSphere(firstEndPos, 0.2f);
        Gizmos.DrawLine(currentPos, firstEndPos);
        DrawArrow(currentPos, firstEndPos);

        // 绘制第二个动作路径
        Gizmos.color = Color.blue;
        Vector3 secondEndPos = firstEndPos + new Vector3(-secondMoveDistance, 0, 0);
        Gizmos.DrawSphere(firstEndPos, 0.2f);
        Gizmos.DrawSphere(secondEndPos, 0.2f);
        Gizmos.DrawLine(firstEndPos, secondEndPos);
        DrawArrow(firstEndPos, secondEndPos);

        // 绘制第三个动作路径
        Gizmos.color = Color.green;
        Vector3 thirdEndPos = secondEndPos + new Vector3(0, -thirdMoveDistance, 0);
        Gizmos.DrawSphere(secondEndPos, 0.2f);
        Gizmos.DrawSphere(thirdEndPos, 0.2f);
        Gizmos.DrawLine(secondEndPos, thirdEndPos);
        DrawArrow(secondEndPos, thirdEndPos);

        // 绘制第四个动作路径（返回到第二个动作开始的位置）
        Gizmos.color = Color.yellow;
        Gizmos.DrawSphere(firstEndPos, 0.2f);
        Gizmos.DrawLine(secondEndPos, firstEndPos);
        DrawArrow(secondEndPos, firstEndPos);

        // 绘制第五个动作路径
        Gizmos.color = Color.magenta;
        Vector3 teleportPos = currentPos + new Vector3(-fifthTeleportDistance, 0, 0);
        Gizmos.DrawSphere(teleportPos, 0.2f);
        Gizmos.DrawSphere(currentPos, 0.2f);
        Gizmos.DrawLine(teleportPos, currentPos);
        DrawArrow(teleportPos, currentPos);

        // 标记火花生成半径（在关键位置显示）
        Gizmos.color = Color.cyan;

        // 第一个动作火花生成半径
        Gizmos.DrawWireSphere(firstEndPos, firstActionSparkRadius);

        // 第二个动作火花生成半径（沿路径显示几个示例位置）
        for (int i = 0; i < 5; i++)
        {
            float t = i / 4f;
            Vector3 samplePos = Vector3.Lerp(firstEndPos, secondEndPos, t);
            Gizmos.DrawWireSphere(samplePos, secondActionSparkRadius);
        }

        // 第四个动作火花生成半径（沿返回路径显示几个示例位置）
        for (int i = 0; i < 5; i++)
        {
            float t = i / 4f;
            Vector3 samplePos = Vector3.Lerp(secondEndPos, firstEndPos, t);
            Gizmos.DrawWireSphere(samplePos, fourthActionSparkRadius);
        }

        // 第四个动作结尾火花生成半径
        Gizmos.DrawWireSphere(firstEndPos, fourthEndSparkRadius);

        // 标记起始位置
        Gizmos.color = Color.white;
        Gizmos.DrawWireSphere(currentPos, 0.3f);
    }

    // 绘制箭头辅助方法
    private void DrawArrow(Vector3 from, Vector3 to)
    {
        Vector3 direction = (to - from).normalized;
        float arrowLength = 0.5f;

        Vector3 left = Quaternion.Euler(0, 0, 135) * direction * arrowLength;
        Vector3 right = Quaternion.Euler(0, 0, -135) * direction * arrowLength;

        Gizmos.DrawLine(to, to + left);
        Gizmos.DrawLine(to, to + right);
    }
}

// 火花自动回收脚本
public class SparkAutoReturn : MonoBehaviour
{
    private SawMove bossScript;
    private float lifetime;
    private float timer;

    public void Initialize(SawMove boss, float lifeTime)
    {
        bossScript = boss;
        lifetime = lifeTime;
    }

    void OnEnable()
    {
        timer = 0f;
    }

    void Update()
    {
        timer += Time.deltaTime;

        if (timer >= lifetime)
        {
            // 如果火花超出生命周期，自动回收
            if (bossScript != null)
            {
                bossScript.ReturnSparkToPool(gameObject);
            }
            else
            {
                // 如果没有bossScript，直接销毁
                Destroy(gameObject);
            }
        }
    }
}