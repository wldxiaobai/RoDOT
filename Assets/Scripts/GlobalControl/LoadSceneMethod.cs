using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

public class LoadSceneMethod : MonoBehaviour
{
    [Header("独立场景加载脚本：在下方填写目标场景的名字")]
    [SerializeField] private string sceneName;

    public void LoadCertainScene()
    {
        if (SceneLoader.Instance != null)
        {
            StartCoroutine(SceneLoader.Instance.LoadSceneAsync(sceneName));
        }
        else
        {
            Debug.LogError("场景加载器(SceneLoader)实例未找到，现采用替代方法加载地图。");
            UnityEngine.SceneManagement.SceneManager.LoadScene(sceneName);
        }
    }
}
