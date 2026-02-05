using UnityEngine;
using System.Collections.Generic;

public class ParallaxEffect : MonoBehaviour
{
    [Header("摄像机设置")]
    public Camera targetCamera;  // 目标摄像机

    [Header("物体和系数设置")]
    public List<GameObject> targetObjects = new List<GameObject>();  // 所有需要移动的物体
    public List<float> movementFactors = new List<float>();  // 每个物体的移动系数

    private Vector3 lastCameraPosition;  // 上一帧摄像机位置
    private Vector3 cameraDelta;         // 摄像机移动向量

    void Start()
    {
        // 如果没指定摄像机，使用主摄像机
        if (targetCamera == null)
        {
            targetCamera = Camera.main;
        }

        if (targetCamera != null)
        {
            // 初始化记录摄像机位置
            lastCameraPosition = targetCamera.transform.position;
        }
        else
        {
            Debug.LogWarning("未找到摄像机！");
        }

        // 确保列表长度一致
        ValidateLists();
    }

    void Update()
    {
        if (targetCamera == null) return;

        // 计算摄像机移动的差值
        Vector3 currentCameraPosition = targetCamera.transform.position;
        cameraDelta = currentCameraPosition - lastCameraPosition;

        // 更新每个物体的位置
        UpdateObjectsPosition();

        // 保存当前摄像机位置供下一帧使用
        lastCameraPosition = currentCameraPosition;
    }

    void UpdateObjectsPosition()
    {
        // 确保列表有效
        if (targetObjects.Count != movementFactors.Count)
        {
            Debug.LogWarning("物体列表和系数列表长度不一致！");
            return;
        }

        // 遍历所有物体，根据系数移动它们
        for (int i = 0; i < targetObjects.Count; i++)
        {
            GameObject obj = targetObjects[i];
            float factor = movementFactors[i];

            if (obj != null && factor != 0)
            {
                // 只在X轴移动，并且与摄像机移动方向相同
                float xMovement = cameraDelta.x * factor;
                Vector3 objectMovement = new Vector3(xMovement, 0, 0);

                // 应用移动
                obj.transform.position += objectMovement;
            }
        }
    }

    void ValidateLists()
    {
        // 如果系数列表长度小于物体列表，补0
        while (movementFactors.Count < targetObjects.Count)
        {
            movementFactors.Add(0f);
        }

        // 如果系数列表长度大于物体列表，移除多余的
        if (movementFactors.Count > targetObjects.Count)
        {
            movementFactors.RemoveRange(targetObjects.Count, movementFactors.Count - targetObjects.Count);
        }
    }

    // 编辑器方法：用于添加新物体
    public void AddNewObject(GameObject newObject, float factor = 1f)
    {
        if (newObject == null) return;

        targetObjects.Add(newObject);
        movementFactors.Add(factor);
    }

    // 编辑器方法：用于移除物体
    public void RemoveObject(int index)
    {
        if (index >= 0 && index < targetObjects.Count)
        {
            targetObjects.RemoveAt(index);
            movementFactors.RemoveAt(index);
        }
    }

    // 编辑器方法：用于批量设置系数
    public void SetAllFactors(float factor)
    {
        for (int i = 0; i < movementFactors.Count; i++)
        {
            movementFactors[i] = factor;
        }
    }
}