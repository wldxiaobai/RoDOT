using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class SceneLoader : Globalizer<SceneLoader>
{
    [SerializeField] private Image fadeImage; // 黑色遮罩图片
    [SerializeField] private float fadeDuration = 1f;

    public Action<string> OnSceneLoad;

    private void Start()
    {
        // 确保开始时遮罩是透明的
        Color color = fadeImage.color;
        color.a = 0f;
        fadeImage.color = color;
        fadeImage.enabled = false; // 初始禁用遮罩，只有在加载场景时才启用
    }

    public void LoadScene(string sceneName)
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene(sceneName);
        OnSceneLoad?.Invoke(sceneName);
    }

    public IEnumerator LoadSceneAsync(string sceneName)
    {
        fadeImage.enabled = true;
        Debug.Log("开始加载场景: " + sceneName);
        yield return StartCoroutine(Fade(0f, 1f)); // 淡入黑色
        yield return StartCoroutine(LoadSceneCoroutine(sceneName));
        OnSceneLoad?.Invoke(sceneName);
        yield return StartCoroutine(Fade(1f, 0f)); // 淡出黑色
        fadeImage.enabled = false;
        Debug.Log("场景: " + sceneName + " 加载完成!");
    }

    IEnumerator Fade(float startAlpha, float endAlpha)
    {
        float elapsedTime = 0f;
        Color color = fadeImage.color;

        while (elapsedTime < fadeDuration)
        {
            elapsedTime += Time.deltaTime;
            color.a = Mathf.Lerp(startAlpha, endAlpha, elapsedTime / fadeDuration);
            fadeImage.color = color;
            yield return null;
        }

        color.a = endAlpha;
        fadeImage.color = color;
    }

    IEnumerator LoadSceneCoroutine(string sceneName)
    {
        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName);

        while (!asyncLoad.isDone)
        {
            // 获取加载进度 (0-0.9)
            float progress = Mathf.Clamp01(asyncLoad.progress / 0.9f);
            Debug.Log($"Loading: {progress * 100}%");
            yield return null;
        }
    }
}
