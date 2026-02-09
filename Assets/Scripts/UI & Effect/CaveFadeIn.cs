using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CaveFadeIn : MonoBehaviour
{
    [Header("淡入淡出设置")]
    [Tooltip("淡入淡出持续时间")]
    [SerializeField] private float fadeDuration = 0.4f;

    private SpriteRenderer spriteRenderer;
    private Color originColor;
    private Coroutine fadeInCo;
    private Coroutine fadeOutCo;
    private List<GameObject> objectsInCave = new();

    private bool displayed = false;

    void Start()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        originColor = spriteRenderer.color;
        originColor.a = 0f; // 初始为完全透明
        spriteRenderer.color = originColor;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        var obj = other.gameObject;
        if (obj.layer == 8 || obj.layer == 9)
        {
            objectsInCave.Add(obj);
        }
        if (objectsInCave.Count > 0 && !displayed)
        {
            displayed = true;
            if (fadeOutCo != null)
            {
                StopCoroutine(fadeOutCo);
            }
            fadeInCo = StartCoroutine(FadeInCoroutine());
        }
    }

    private void OnTriggerExit2D(Collider2D other)
    {
        var obj = other.gameObject;
        if (objectsInCave.Contains(obj))
        {
            objectsInCave.Remove(obj);
        }
        if (objectsInCave.Count == 0 && displayed)
        {
            displayed = false;
            if(fadeInCo != null)
            {
                StopCoroutine(fadeInCo);
            }
            fadeOutCo = StartCoroutine(FadeOutCoroutine());
        }
    }

    IEnumerator FadeInCoroutine()
    {
        float elapsedTime = 0f;
        float originAlpha = originColor.a; // 记录初始透明度
        while (elapsedTime < fadeDuration)
        {
            float alpha = Mathf.Lerp(originAlpha, 1f, elapsedTime / fadeDuration);
            originColor.a = alpha;
            spriteRenderer.color = originColor;
            elapsedTime += Time.deltaTime;
            yield return null;
        }
        fadeInCo = null;
    }

    IEnumerator FadeOutCoroutine()
    {
        float elapsedTime = 0f;
        float originAlpha = originColor.a; // 记录初始透明度
        while (elapsedTime < fadeDuration)
        {
            float alpha = Mathf.Lerp(originAlpha, 0f, elapsedTime / fadeDuration);
            originColor.a = alpha;
            spriteRenderer.color = originColor;
            elapsedTime += Time.deltaTime;
            yield return null;
        }
    }
}
