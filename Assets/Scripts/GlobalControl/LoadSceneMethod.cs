using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class LoadSceneMethod : MonoBehaviour
{
    [Header("独立场景加载脚本：在下方填写场景名字")]
    [SerializeField] private string sceneName;

    public void LoadCertainScene()
    {
        UnityEngine.SceneManagement.SceneManager.LoadScene(sceneName);
    }
}
