using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AreaManager : MonoBehaviour
{
    [System.Serializable]
    public class AreaData
    {
        public string areaName = "New Area"; // 区域名称（可选，方便识别）
        public List<Collider2D> triggerColliders = new List<Collider2D>(); // 多个触发器碰撞箱
        public SpriteRenderer targetSprite; // 对应的贴图
        [Range(0.1f, 5f)] public float fadeDuration = 1f; // 淡入淡出时间

        [HideInInspector] public int activeTriggers = 0; // 当前激活的触发器数量
        [HideInInspector] public Coroutine fadeCoroutine; // 当前运行的协程

        // 检查碰撞箱是否属于此区域
        public bool ContainsCollider(Collider2D collider)
        {
            return triggerColliders.Contains(collider);
        }
    }

    public List<AreaData> areaList = new List<AreaData>(); // 存储所有区域数据

    // 字典：碰撞箱 -> 区域数据，用于快速查找
    private Dictionary<Collider2D, AreaData> colliderToAreaMap = new Dictionary<Collider2D, AreaData>();

    void Start()
    {
        // 初始化映射关系
        InitializeAreaMapping();
    }

    void InitializeAreaMapping()
    {
        colliderToAreaMap.Clear();

        foreach (var area in areaList)
        {
            if (area.targetSprite != null && area.triggerColliders.Count > 0)
            {
                // 初始时设置贴图为完全透明
                Color spriteColor = area.targetSprite.color;
                spriteColor.a = 0f;
                area.targetSprite.color = spriteColor;

                // 重置激活计数器
                area.activeTriggers = 0;

                // 将每个碰撞箱映射到区域
                foreach (var collider in area.triggerColliders)
                {
                    if (collider != null)
                    {
                        // 确保触发器是启用的
                        collider.isTrigger = true;
                        colliderToAreaMap[collider] = area;
                    }
                    else
                    {
                        Debug.LogWarning($"区域 '{area.areaName}' 中存在空的碰撞箱引用！");
                    }
                }
            }
            else
            {
                if (area.targetSprite == null)
                    Debug.LogWarning($"区域 '{area.areaName}' 的TargetSprite未设置！");
                if (area.triggerColliders.Count == 0)
                    Debug.LogWarning($"区域 '{area.areaName}' 没有设置任何触发器碰撞箱！");
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (colliderToAreaMap.TryGetValue(other, out AreaData areaData))
        {
            // 增加激活的触发器数量
            areaData.activeTriggers++;

            // 如果是第一个激活的触发器，开始淡入
            if (areaData.activeTriggers == 1)
            {
                StartFade(areaData, true);
            }
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        if (colliderToAreaMap.TryGetValue(other, out AreaData areaData))
        {
            // 减少激活的触发器数量
            areaData.activeTriggers = Mathf.Max(0, areaData.activeTriggers - 1);

            // 如果没有激活的触发器了，开始淡出
            if (areaData.activeTriggers == 0)
            {
                StartFade(areaData, false);
            }
        }
    }

    void StartFade(AreaData areaData, bool fadeIn)
    {
        // 如果已经有协程在运行，先停止它
        if (areaData.fadeCoroutine != null)
        {
            StopCoroutine(areaData.fadeCoroutine);
        }

        // 开始新的淡入淡出协程
        float targetAlpha = fadeIn ? 1f : 0f;
        areaData.fadeCoroutine = StartCoroutine(FadeSprite(areaData, targetAlpha));
    }

    private IEnumerator FadeSprite(AreaData areaData, float targetAlpha)
    {
        SpriteRenderer sprite = areaData.targetSprite;
        float duration = areaData.fadeDuration;
        float elapsedTime = 0f;

        float startAlpha = sprite.color.a;

        while (elapsedTime < duration)
        {
            elapsedTime += Time.deltaTime;
            float t = Mathf.Clamp01(elapsedTime / duration);

            Color currentColor = sprite.color;
            currentColor.a = Mathf.Lerp(startAlpha, targetAlpha, t);
            sprite.color = currentColor;

            yield return null;
        }

        // 确保最终颜色正确
        Color finalColor = sprite.color;
        finalColor.a = targetAlpha;
        sprite.color = finalColor;

        areaData.fadeCoroutine = null;
    }

    // 重置所有区域的透明度
    public void ResetAllAreas()
    {
        foreach (var area in areaList)
        {
            if (area.fadeCoroutine != null)
            {
                StopCoroutine(area.fadeCoroutine);
                area.fadeCoroutine = null;
            }

            if (area.targetSprite != null)
            {
                Color color = area.targetSprite.color;
                color.a = 0f;
                area.targetSprite.color = color;
            }

            area.activeTriggers = 0;
        }
    }

    // 手动触发区域淡入（用于测试或脚本控制）
    public void ActivateArea(string areaName)
    {
        foreach (var area in areaList)
        {
            if (area.areaName == areaName)
            {
                area.activeTriggers = 1;
                StartFade(area, true);
                break;
            }
        }
    }

    // 手动触发区域淡出（用于测试或脚本控制）
    public void DeactivateArea(string areaName)
    {
        foreach (var area in areaList)
        {
            if (area.areaName == areaName)
            {
                area.activeTriggers = 0;
                StartFade(area, false);
                break;
            }
        }
    }

    // 编辑器辅助方法
#if UNITY_EDITOR
    public void AddNewArea()
    {
        areaList.Add(new AreaData());
    }

    public void RemoveArea(int index)
    {
        if (index >= 0 && index < areaList.Count)
        {
            areaList.RemoveAt(index);
        }
    }

    // 在Inspector中修改后重新初始化映射
    void OnValidate()
    {
        // 注意：OnValidate在编辑模式下运行，我们只在编辑器状态下重新初始化
        if (!Application.isPlaying)
        {
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this != null)
                {
                    InitializeAreaMapping();
                }
            };
        }
    }
#endif
}