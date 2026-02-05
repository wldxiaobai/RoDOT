#nullable enable
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 召唤型站桩Boss（简化版）
/// </summary>
public class SummonerBoss : MonoBehaviour
{
    [Header("Boss属性")]
    [SerializeField] private float health = 1000f;
    [SerializeField] private Transform playerTransform = null!;
    [SerializeField] private SwordPoolManager swordPool = null!;

    [Header("飞剑生成区域")]
    [SerializeField] private Vector2 bossAreaSize = new(6f, 4f);

    [Header("动作参数")]
    [SerializeField] private float actionCooldown = 1f;

    private bool isActive = true;
    private float currentHealth;
    private Coroutine? actionRoutine;

    private void Start()
    {
        currentHealth = health;

        if (swordPool != null && playerTransform != null)
        {
            swordPool.SetPlayerTransform(playerTransform);
        }

        StartBossActions();
    }

    private void StartBossActions()
    {
        if (actionRoutine != null)
        {
            StopCoroutine(actionRoutine);
        }
        actionRoutine = StartCoroutine(BossActionLoop());
    }

    private IEnumerator BossActionLoop()
    {
        while (isActive)
        {
            // 随机选择动作：40%动作1，40%动作2，20%动作3
            float random = Random.value;

            if (random < 0.4f)
            {
                yield return StartCoroutine(ExecuteAction1());
            }
            else if (random < 0.8f)
            {
                yield return StartCoroutine(ExecuteAction2());
            }
            else
            {
                yield return StartCoroutine(ExecuteAction3());
            }

            // 动作间隔
            yield return new WaitForSeconds(actionCooldown);
        }
    }

    private IEnumerator ExecuteAction1()
    {
        yield return StartCoroutine(SummonSwords(3, 1.5f));
    }

    private IEnumerator ExecuteAction2()
    {
        yield return StartCoroutine(SummonSwords(2, 0.6f));
    }

    private IEnumerator ExecuteAction3()
    {
        // 石碑逻辑暂时占位
        yield return new WaitForSeconds(3f);
    }

    private IEnumerator SummonSwords(int swordsPerSide, float spawnInterval)
    {
        // 对称生成飞剑
        for (int i = 0; i < swordsPerSide; i++)
        {
            // 左侧位置
            float yOffset = (i - (swordsPerSide - 1) * 0.5f) * 1.5f;
            Vector3 leftPos = transform.position + new Vector3(-3f, 3f + yOffset, 0);
            SummonSword(leftPos);

            // 右侧对称位置
            Vector3 rightPos = transform.position + new Vector3(3f, 3f + yOffset, 0);
            SummonSword(rightPos);

            yield return new WaitForSeconds(spawnInterval);
        }
    }

    private void SummonSword(Vector3 initialPosition)
    {
        if (swordPool == null) return;

        var sword = swordPool.GetSword();
        if (sword != null)
        {
            sword.transform.position = initialPosition;
            sword.SetTargetPosition(GetRandomBossAreaPosition());
        }
    }

    private Vector3 GetRandomBossAreaPosition()
    {
        return transform.position + new Vector3(
            Random.Range(-bossAreaSize.x * 0.5f, bossAreaSize.x * 0.5f),
            Random.Range(-bossAreaSize.y * 0.5f, bossAreaSize.y * 0.5f),
            0
        );
    }

    public void TakeDamage(float damage)
    {
        if (!isActive) return;

        currentHealth -= damage;

        if (currentHealth <= 0)
        {
            Die();
        }
    }

    private void Die()
    {
        isActive = false;

        if (actionRoutine != null)
        {
            StopCoroutine(actionRoutine);
            actionRoutine = null;
        }

        gameObject.SetActive(false);
    }
}