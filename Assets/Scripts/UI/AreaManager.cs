using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AreaManager : MonoBehaviour
{
    [System.Serializable]
    public class Area
    {
        public string areaName = "New Area";
        public List<Collider2D> subAreas = new List<Collider2D>(); // 区域的所有子区域
        public GameObject spriteContainer; // 包含所有贴图的物体
        [HideInInspector] public List<SpriteRenderer> targetSprites = new List<SpriteRenderer>(); // 自动收集的贴图
        [HideInInspector] public int playerCount = 0; // 玩家在该区域中的数量

        // 渐变参数
        [Range(0.1f, 5f)]
        public float fadeDuration = 1f;
        [Range(0f, 1f)]
        public float targetAlpha = 1f;

        // 当前透明度
        [HideInInspector] public float currentAlpha = 0f;
        [HideInInspector] public Coroutine fadeCoroutine;

        // 自动收集物体上的所有SpriteRenderer
        public void CollectSprites()
        {
            targetSprites.Clear();

            if (spriteContainer != null)
            {
                // 收集容器物体及其所有子物体上的SpriteRenderer
                SpriteRenderer[] allRenderers = spriteContainer.GetComponentsInChildren<SpriteRenderer>(true);
                targetSprites.AddRange(allRenderers);
            }
        }
    }

    public List<Area> areas = new List<Area>();
    public GameObject playerObject; // 直接拖拽玩家对象

    private Dictionary<Collider2D, Area> colliderToAreaMap = new Dictionary<Collider2D, Area>();
    private Dictionary<Area, HashSet<GameObject>> playersInArea = new Dictionary<Area, HashSet<GameObject>>();

    void Start()
    {
        CollectAllSprites();
        InitializeAreaMapping();
        InitializeSprites();
    }

    void CollectAllSprites()
    {
        foreach (var area in areas)
        {
            area.CollectSprites();
        }
    }

    void InitializeAreaMapping()
    {
        colliderToAreaMap.Clear();
        playersInArea.Clear();

        foreach (var area in areas)
        {
            playersInArea[area] = new HashSet<GameObject>();

            foreach (var collider in area.subAreas)
            {
                if (collider != null)
                {
                    colliderToAreaMap[collider] = area;

                    // 确保碰撞器设置为触发器
                    collider.isTrigger = true;
                }
            }
        }
    }

    void InitializeSprites()
    {
        foreach (var area in areas)
        {
            foreach (var sprite in area.targetSprites)
            {
                if (sprite != null)
                {
                    Color color = sprite.color;
                    color.a = 0f;
                    sprite.color = color;
                    area.currentAlpha = 0f;
                }
            }
        }
    }

    void OnTriggerEnter2D(Collider2D other)
    {
        if (playerObject == null || other.gameObject != playerObject) return;

        var c = GetComponent<Collider2D>();
        if (colliderToAreaMap.TryGetValue(c, out Area area))
        {
            GameObject player = other.gameObject;

            if (!playersInArea[area].Contains(player))
            {
                playersInArea[area].Add(player);
                area.playerCount = playersInArea[area].Count;

                if (area.playerCount == 1)
                {
                    if (area.fadeCoroutine != null)
                        StopCoroutine(area.fadeCoroutine);

                    area.fadeCoroutine = StartCoroutine(FadeArea(area, area.targetAlpha));
                }
            }
        }
    }

    void OnTriggerExit2D(Collider2D other)
    {
        if (playerObject == null || other.gameObject != playerObject) return;

        var c = GetComponent<Collider2D>();
        if (colliderToAreaMap.TryGetValue(c, out Area area))
        {
            GameObject player = other.gameObject;

            if (playersInArea[area].Contains(player))
            {
                playersInArea[area].Remove(player);
                area.playerCount = playersInArea[area].Count;

                if (area.playerCount == 0)
                {
                    if (area.fadeCoroutine != null)
                        StopCoroutine(area.fadeCoroutine);

                    area.fadeCoroutine = StartCoroutine(FadeArea(area, 0f));
                }
            }
        }
    }

    IEnumerator FadeArea(Area area, float targetAlpha)
    {
        float startAlpha = area.currentAlpha;
        float elapsedTime = 0f;

        while (elapsedTime < area.fadeDuration)
        {
            elapsedTime += Time.deltaTime;
            float t = Mathf.Clamp01(elapsedTime / area.fadeDuration);

            float newAlpha = Mathf.Lerp(startAlpha, targetAlpha, t);
            area.currentAlpha = newAlpha;

            // 更新所有贴图的透明度
            foreach (var sprite in area.targetSprites)
            {
                if (sprite != null)
                {
                    Color color = sprite.color;
                    color.a = newAlpha;
                    sprite.color = color;
                }
            }

            yield return null;
        }

        // 确保最终值准确
        area.currentAlpha = targetAlpha;
        foreach (var sprite in area.targetSprites)
        {
            if (sprite != null)
            {
                Color color = sprite.color;
                color.a = targetAlpha;
                sprite.color = color;
            }
        }

        area.fadeCoroutine = null;
    }

    // 编辑器辅助方法
    [ContextMenu("添加新区域")]
    public void AddNewArea()
    {
        areas.Add(new Area() { areaName = "区域 " + (areas.Count + 1) });
    }

    [ContextMenu("重新收集所有贴图")]
    public void RecollectAllSprites()
    {
        CollectAllSprites();
    }

    // 强制设置区域透明度（用于调试）
    public void SetAreaAlpha(string areaName, float alpha)
    {
        foreach (var area in areas)
        {
            if (area.areaName == areaName)
            {
                if (area.fadeCoroutine != null)
                    StopCoroutine(area.fadeCoroutine);

                StartCoroutine(FadeArea(area, Mathf.Clamp01(alpha)));
                break;
            }
        }
    }
}