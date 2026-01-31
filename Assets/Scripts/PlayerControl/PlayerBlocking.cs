using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerBlocking : MonoBehaviour
{
    [Header("格挡设置")]
    [SerializeField] private float blockWindow = 0.3f; // 格挡判定时间
    [SerializeField] private float blockCooldown = 0.6f; // 格挡内置冷却
    [SerializeField] private LayerMask attackLayer; // 攻击所在的图层

    [Header("状态指示")]
    [SerializeField] private bool isBlocking = false;
    [SerializeField] private bool isOnCooldown = false;

    [Header("格挡效果")]
    [SerializeField] private GameObject blockEffectPrefab; // 格挡特效预制体
    [SerializeField] private Transform blockEffectSpawnPoint; // 特效生成位置

    private float blockTimer = 0f;
    private float cooldownTimer = 0f;

    // 用于存储检测到的攻击物体
    private List<GameObject> blockedAttacks = new List<GameObject>();

    void Update()
    {
        HandleBlockInput();
        UpdateTimers();
    }

    private void HandleBlockInput()
    {
        // 如果不在冷却中且按下空格键
        if (Input.GetKeyDown(KeyCode.Space) && !isOnCooldown)
        {
            StartBlocking();
        }
    }

    private void StartBlocking()
    {
        isBlocking = true;
        blockTimer = blockWindow;

        // 清空已格挡的列表
        blockedAttacks.Clear();

        Debug.Log("开始格挡");
    }

    private void UpdateTimers()
    {
        // 更新格挡计时器
        if (isBlocking)
        {
            blockTimer -= Time.deltaTime;
            if (blockTimer <= 0f)
            {
                EndBlocking();
            }
        }

        // 更新冷却计时器
        if (isOnCooldown)
        {
            cooldownTimer -= Time.deltaTime;
            if (cooldownTimer <= 0f)
            {
                isOnCooldown = false;
            }
        }
    }

    private void EndBlocking()
    {
        isBlocking = false;
        Debug.Log("结束格挡");

        // 如果没有格挡成功，开始冷却
        if (blockedAttacks.Count == 0 && !isOnCooldown)
        {
            StartCooldown();
        }
    }

    private void StartCooldown()
    {
        isOnCooldown = true;
        cooldownTimer = blockCooldown;
        Debug.Log("格挡冷却开始");
    }

    // 处理攻击检测
    private void OnTriggerEnter2D(Collider2D other)
    {
        if (!isBlocking) return;

        if (other.CompareTag("Attack1"))
        {
            HandleBlockedAttack(other.gameObject);
        }
    }

    private void HandleBlockedAttack(GameObject attackObject)
    {
        // 避免重复格挡同一个攻击
        if (blockedAttacks.Contains(attackObject)) return;

        // 添加到已格挡列表
        blockedAttacks.Add(attackObject);

        // 触发格挡成功事件
        OnBlockSuccess(attackObject);

        Debug.Log($"成功格挡: {attackObject.name}");
    }

    private void OnBlockSuccess(GameObject attackObject)
    {
        // 这里可以添加格挡成功后的逻辑

    }
}