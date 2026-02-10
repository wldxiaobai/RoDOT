using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GlobalPlayer : Globalizer<GlobalPlayer>
{
    [Header("玩家数据")]
    [Tooltip("玩家物体预制体")]
    [SerializeField] private GameObject PlayerPrefab;

    private GameObject playerInstance;

    public GameObject Player
    {
        get { return playerInstance; }
    }

    void Update()
    {
        if (playerInstance != null)
        {
            transform.position = playerInstance.transform.position;
        }
    }

    public void SpawnPlayer(Vector3 pos, Quaternion rotation)
    {
        if (playerInstance != null)
        {
            Destroy(playerInstance);
            Debug.Log("检测到已有玩家，已销毁旧实例并生成新玩家。");
        }

        playerInstance = Instantiate(PlayerPrefab, pos, rotation);
        Debug.Log("玩家成功生成在：" + $"({pos.x}, {pos.y}, {pos.z})");
    }

    public void AcceptPlayerObject(GameObject player)
    {
        if (player == null)
        {
            Debug.LogWarning("传入的玩家对象为空，无法绑定GlobalPlayer。");
            return;
        }

        if (playerInstance != null && playerInstance != player)
        {
            Debug.Log("GlobalPlayer当前已经引用一个玩家实例，正在切换为新的引用。");
        }

        Destroy(playerInstance);
        playerInstance = player;
    }

    public void ClearPlayerReference(GameObject player)
    {
        if (playerInstance != player)
        {
            return;
        }

        playerInstance = null;
    }
}
