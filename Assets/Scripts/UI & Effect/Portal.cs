using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Portal : LoadSceneMethod
{
    [Header("备用寻找玩家的Tag")]
    [SerializeField] private string playerTag = "Player";
    [Header("Portal 生成点")]
    [Tooltip("如果不指定，Portal 会使用当前玩家的位置作为入口")]
    [SerializeField] private Vector3 spawnPoint;

    private GameObject player;
    private Collider2D portalCollider;

    private void Start()
    {
        portalCollider = GetComponent<Collider2D>();
        if(portalCollider == null)
        {
            Debug.LogError("Portal: No Collider2D component found on the portal object.");
        }
        portalCollider.enabled = true;
    }

    private void UpdatePlayer()
    {
        if(GlobalPlayer.Instance != null && GlobalPlayer.Instance.Player != null)
        {
            player = GlobalPlayer.Instance.Player;
        }
        else if(GameObject.FindGameObjectWithTag(playerTag) != null)
        {
            player = GameObject.FindGameObjectWithTag(playerTag);
        }
        else
        {
            Debug.LogWarning("Portal: Player object not found. Please ensure the player has the correct tag or is assigned in GlobalPlayer.");
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        UpdatePlayer();
        if (collision.gameObject == player)
        {
            if (SaveManeger.IsInitialized)
            {
                var targetPosition = spawnPoint != null ? (Vector2)spawnPoint : (Vector2)player.transform.position;
                SaveManeger.Instance.SetPortalSpawnPosition(targetPosition);
            }
            LoadCertainScene();
        }
    }
}
