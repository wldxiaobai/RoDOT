using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class Globalizer<T> : MonoBehaviour where T : Globalizer<T> // 派生类以自身作为类型参数
{
    private static T _instance;                // 静态字段：保存当前 T 类型的唯一实例引用（对全局共享，跨场景有效）
    public static T Instance => _instance;     // 公共只读访问器，通过 T.Instance 获取当前单例；未初始化时为 null
    public static bool IsInitialized => _instance != null; // 是否存在单例

    protected virtual void Awake()
    {
        if (_instance != null && _instance != (T)this)
        {
            Destroy(gameObject); // 销毁重复的 GameObject，避免出现多个副本
            return;
        }

        _instance = (T)this;             // 将当前组件注册为该类型的唯一实例
        DontDestroyOnLoad(gameObject);   // 将承载该组件的 GameObject 设为跨场景常驻
    }

    protected virtual void OnDestroy()
    {
        if (_instance == (T)this) // 仅在自己是当前单例时清理（防止误清理其他仍存活的实例引用）
            _instance = null;     // 清空静态引用，便于下次重新创建或正确判定未初始化
    }
}
