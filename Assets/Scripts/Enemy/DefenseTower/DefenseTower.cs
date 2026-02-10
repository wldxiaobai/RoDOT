using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DefenseTower : BaseEnemy
{
    private const string IdleBehaviourName = "Idle";
    private const string ChargeBehaviourName = "Charge";
    private const string LaunchBehaviourName = "Launch";

    [Header("受击设置")]
    [Tooltip("受击闪烁持续时间")]
    [SerializeField] private float hitFlashDuration = 0.2f;
    [Tooltip("受击闪烁颜色")]
    [SerializeField] private Color hitFlashColor = new Color(1f, 0.5f, 0f);

    [Header("攻击设置")]
    [Tooltip("检索玩家的范围")]
    [SerializeField, Min(0f)] private float detectionRange = 5f;
    [Tooltip("充能速度 (单位：每秒) ")]
    [SerializeField, Min(0f)] private float chargePace = 0.5f;
    [Tooltip("攻击冷却时间（秒）")]
    [SerializeField] private float attackCooldown = 1f;
    [Tooltip("电球生成位置偏移")]
    [SerializeField] private Vector2 lightningBallSpawnOffset = new Vector2(0.5f, 0f);

    [Header("电球设置")]
    [Tooltip("电球预制体")]
    [SerializeField] private GameObject lightningBallPrefab;
    [Tooltip("充能条填充贴图资源")]
    [SerializeField] private SpriteRenderer chargeBar;

    private float chargeFillRate;
    private float chargeCooldownRemaining;
    private Transform playerTransform;
    private Material chargeBarMaterial;
    private Material towerMaterial;
    private SpriteRenderer _spriteRenderer;
    private Coroutine _flashCoroutine;

    protected override string DecideNextBehaviour()
    {
        ResolvePlayer();
        if (!IsPlayerInRange() || chargeCooldownRemaining > 0f)
        {
            return IdleBehaviourName;
        }

        if (chargeFillRate >= 1f)
        {
            return LaunchBehaviourName;
        }

        return ChargeBehaviourName;
    }

    private ActSeq BuildIdleSequence()
    {
        var seq = new ActSeq();
        var loop = seq.CreateDoWhileNode(IdleLoop, _ => !IsPlayerInRange() || chargeCooldownRemaining > 0f);
        seq.Start.SetNext(loop);
        loop.SetNext(seq.End);
        return seq;
    }

    private ActSeq BuildChargeSequence()
    {
        var seq = new ActSeq();
        var loop = seq.CreateDoWhileNode(ChargeLoop, _ => IsPlayerInRange() && chargeCooldownRemaining <= 0f && chargeFillRate < 1f);
        seq.Start.SetNext(loop);
        loop.SetNext(seq.End);
        return seq;
    }

    private ActSeq BuildLaunchSequence()
    {
        var seq = new ActSeq();
        var action = seq.CreateActionNode(LaunchAction);
        seq.Start.SetNext(action);
        action.SetNext(seq.End);
        return seq;
    }

    private IEnumerator IdleLoop(MonoBehaviour _)
    {
        ResolvePlayer();
        if (chargeCooldownRemaining > 0f)
        {
            chargeCooldownRemaining = Mathf.Max(0f, chargeCooldownRemaining - Time.deltaTime);
        }
        StartChargeDrain();
        UpdateChargeBar();
        yield return null;
    }

    private IEnumerator ChargeLoop(MonoBehaviour _)
    {
        ResolvePlayer();
        if (chargeCooldownRemaining > 0f)
        {
            chargeCooldownRemaining = Mathf.Max(0f, chargeCooldownRemaining - Time.deltaTime);
        }
        chargeFillRate = Mathf.Clamp01(chargeFillRate + chargePace * Time.deltaTime);
        UpdateChargeBar();
        yield return null;
    }

    private IEnumerator LaunchAction(MonoBehaviour _)
    {
        SpawnLightningBall();
        yield return null;
    }

    private void StartChargeDrain()
    {
        chargeFillRate = Mathf.Lerp(chargeFillRate, 0f, 10f * Time.deltaTime);
        if (chargeFillRate <= 0.001f)
        {
            chargeFillRate = 0f;
        }
    }

    private void UpdateChargeBar()
    {
        if (chargeBarMaterial == null)
        {
            return;
        }

        chargeBarMaterial.SetFloat("_FillAmount", Mathf.Clamp01(chargeFillRate));
    }

    private void ResolvePlayer()
    {
        if (GlobalPlayer.Instance?.Player != null && playerTransform != GlobalPlayer.Instance.Player.transform)
        {
            playerTransform = GlobalPlayer.Instance.Player.transform;
        }
    }

    private bool IsPlayerInRange()
    {
        if (playerTransform == null)
        {
            return false;
        }

        var delta = playerTransform.position - transform.position;
        if (delta.sqrMagnitude > detectionRange * detectionRange)
        {
            return false;
        }

        return IsPlayerInFront(delta);
    }

    private bool IsPlayerInFront(Vector3 delta)
    {
        if (_spriteRenderer == null)
        {
            return true;
        }

        var facingRight = _spriteRenderer.flipX;
        if (Mathf.Approximately(delta.x, 0f))
        {
            return true;
        }

        return facingRight ? delta.x >= 0f : delta.x <= 0f;
    }

    private void SpawnLightningBall()
    {
        if (lightningBallPrefab == null || playerTransform == null)
        {
            return;
        }

        var spawnPosition = (Vector3)lightningBallSpawnOffset + transform.position;
        var instance = Instantiate(lightningBallPrefab, spawnPosition, Quaternion.identity);
        var ball = instance.GetComponent<LightningBall>();
        if (ball != null)
        {
            var direction = playerTransform.position - spawnPosition;
            if (!Mathf.Approximately(direction.sqrMagnitude, 0f))
            {
                ball.SetLaunchDirection(direction);
            }
            var renderer = ball.GetComponent<SpriteRenderer>();
            if (renderer != null && chargeBar != null)
            {
                renderer.sortingOrder = chargeBar.sortingOrder + 1;
            }
        }

        chargeCooldownRemaining = attackCooldown;
        chargeFillRate = 0f;
        UpdateChargeBar();
    }

    protected override void EnemyInit()
    {
        base.EnemyInit();
        if (chargeBar != null)
        {
            chargeBarMaterial = chargeBar.material;
            UpdateChargeBar();
        }

        _spriteRenderer = GetComponent<SpriteRenderer>();
        if (_spriteRenderer != null)
        {
            towerMaterial = _spriteRenderer.material;
        }

        AddBehaviour(IdleBehaviourName, 0f, BuildIdleSequence());
        AddBehaviour(ChargeBehaviourName, 0f, BuildChargeSequence());
        AddBehaviour(LaunchBehaviourName, 0f, BuildLaunchSequence());
    }

    protected override void OnHitByPlayerAttack(HitInfo incoming)
    {
        base.OnHitByPlayerAttack(incoming);
        FlashEffect(hitFlashDuration, hitFlashColor);
    }

    // --- Visual Effects ---
    private void FlashEffect(float duration, Color color)
    {
        if (towerMaterial == null)
        {
            return;
        }

        _flashCoroutine = StartCoroutine(FlashEffectCoroutine(duration, color));
    }

    IEnumerator FlashEffectCoroutine(float duration, Color color)
    {
        if (towerMaterial == null)
        {
            yield break;
        }

        float elapsed = 0f;
        towerMaterial.SetColor("_flashColor", color);
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            towerMaterial.SetFloat("_flashFactor", Mathf.Lerp(1f, 0f, elapsed / duration));
            yield return null;
        }
    }

    private void StopFlashEffect()
    {
        if (_flashCoroutine != null)
        {
            StopCoroutine(_flashCoroutine);
            _flashCoroutine = null;
        }
        if (towerMaterial != null)
        {
            towerMaterial.SetFloat("_flashFactor", 0f);
        }
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.4f, 0f, 0.5f);
        Gizmos.DrawWireSphere(transform.position, detectionRange);
    }
}
