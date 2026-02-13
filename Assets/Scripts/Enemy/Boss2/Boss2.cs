using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static AttackHitInfo;

public class Boss2 : BaseEnemy
{
    private enum phase
    {
        inactive,
        phase1,
        phase2
    }

    private enum action
    {
        sword1,
        sword2,
        Lightning1,
        Lightning2,
        BigLightning,
        Laser
    }

    [Header("Boss设置")]
    [Tooltip("转阶段时血量百分比")]
    [Range(0f, 100f)]
    [SerializeField] private float phase2HealthThreshold = 50f;
    [Tooltip("阶段切换时间")]
    [SerializeField] private float phaseTransitionTime = 2f;
    [Tooltip("转阶段时身驱上移距离")]
    [SerializeField] private float phaseTransitionRiseDistance = 1f;

    [Header("身体部件 子物体引用")]
    [Tooltip("头壳子物体")]
    [SerializeField] private GameObject headShellObject;
    [Tooltip("脑罐子物体")]
    [SerializeField] private GameObject brainShellObject;
    [Tooltip("底座子物体")]
    [SerializeField] private GameObject baseShellObject;
    private SpriteRenderer headShell;
    private SpriteRenderer brainShell;
    private SpriteRenderer baseShell;
    private BossLightningFlashEffect baseShellFlashEffect;
    private BlockInPhase1 baseShellBlock;
    private Material headShellMaterial;
    private Material brainShellMaterial;
    private Material baseShellMaterial;
    private Collider2D brainShellCollider;
    private Coroutine flashCoroutine;

    [Header("攻击相关")]
    [Tooltip("飞剑预制体")]
    [SerializeField] private GameObject flyingSwordPrefab;
    [Tooltip("电球预制体")]
    [SerializeField] private GameObject lightningBallPrefab;
    [Tooltip("大电球预制体")]
    [SerializeField] private GameObject bigLightningPrefab;
    [Tooltip("激光预制体")]
    [SerializeField] private GameObject laserPrefab;
    [Tooltip("产生电球&激光时的位置偏移")]
    [SerializeField] private Vector2 summonOffset;

    [Header("视效设置")]
    [Tooltip("未激活颜色覆盖度")]
    [Range(0, 1)]
    [SerializeField] private float unactiveColorAlpha;
    [Tooltip("未激活时材质颜色")]
    [SerializeField] private Color unactiveColor;
    [Tooltip("受伤颜色覆盖度")]
    [Range(0, 1)]
    [SerializeField] private float lightningColorAlpha;
    [Tooltip("受伤时闪烁颜色")]
    [SerializeField] private Color lightningColor;
    [Tooltip("受伤时闪烁持续时间")]
    [SerializeField] private float lightningFlashDuration = 0.2f;

    [Header("音效设置")]
    [Tooltip("音量")]
    [Range(0, 1)] [SerializeField] private float soundVolume = 0.5f;
    [Tooltip("受击音效")]
    [SerializeField] private AudioClip hitSFX;
    [Tooltip("转阶段音效")]
    [SerializeField] private AudioClip phaseTransitionSFX;

    // ---------------飞剑1---------------

    [Header("飞剑1 设置")]
    [Tooltip("飞剑生成范围：min")]
    [SerializeField] private Vector2 flyingSword1_SpawnRangeMin;
    [Tooltip("飞剑生成范围：max")]
    [SerializeField] private Vector2 flyingSword1_SpawnRangeMax;
    [Tooltip("召唤3把飞剑的时间间隔")]
    [SerializeField] private float flyingSword1_SummonInterval;
    [Tooltip("后摇时间")]
    [SerializeField] private float flyingSword1_AfterAttackTime;

    // ---------------飞剑2---------------

    [Header("飞剑2 设置")]
    [Tooltip("同时召唤飞剑的数量")]
    [SerializeField] private int flyingSword2_SummonCount;
    [Tooltip("后摇时间")]
    [SerializeField] private float flyingSword2_AfterAttackTime;

    // ---------------电球1---------------

    [Header("电球1 设置")]
    [Tooltip("连续召唤4个电球时间间隔")]
    [SerializeField] private float lightningBall1_SummonInterval;
    [Tooltip("后摇时间")]
    [SerializeField] private float lightningBall1_AfterAttackTime;

    // ---------------电球2---------------

    [Header("电球2 设置")]
    [Tooltip("三叉发射电球的角度")]
    [SerializeField] float lightningBall2_SummonAngle;
    [Tooltip("3发电球时间间隔")]
    [SerializeField] float lightningBall2_SummonInterval;
    [Tooltip("后摇时间")]
    [SerializeField] private float lightningBall2_AfterAttackTime;

    // ---------------大电球---------------

    [Header("大电球 设置")]
    [Tooltip("后摇时间")]
    [SerializeField] private float bigLightning_AfterAttackTime;


    // ---------------激光---------------

    [Header("激光 设置")]
    [Tooltip("激光扫射角度范围：左端")]
    [SerializeField] private float laser_SweepAngleMin;
    [Tooltip("激光扫射角度范围：右端")]
    [SerializeField] private float laser_SweepAngleMax;
    [Tooltip("后摇时间")]
    [SerializeField] private float laser_AfterAttackTime;

    // ---------------行为名称---------------

    private const string Sword1BehaviourName = "Sword1";
    private const string Sword2BehaviourName = "Sword2";
    private const string Lightning1BehaviourName = "Lightning1";
    private const string Lightning2BehaviourName = "Lightning2";
    private const string BigLightningBehaviourName = "BigLightning";
    private const string LaserBehaviourName = "Laser";
    private const string PhaseTransitionBehaviourName = "PhaseTransition";

    private Transform playerTransform;
    private phase currentPhase = phase.inactive;

    // ---------------初始化---------------

    protected override void EnemyInit()
    {
        ResolvePlayerReference();
        InitializeVisuals();
        RegisterAllBehaviours();
    }

    protected void LateUpdate()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            // 测试用：按空格生成一个激光
            var obj = Instantiate(laserPrefab, transform.position + (Vector3)summonOffset, Quaternion.identity);
        }
    }

    private void InitializeVisuals()
    {
        headShell = headShellObject.GetComponent<SpriteRenderer>();
        brainShell = brainShellObject.GetComponent<SpriteRenderer>();
        baseShell = baseShellObject.GetComponent<SpriteRenderer>();
        headShellMaterial = headShell.material;
        brainShellMaterial = brainShell.material;
        baseShellMaterial = baseShell.material;
        baseShellFlashEffect = baseShellObject.GetComponent<BossLightningFlashEffect>();
        baseShellFlashEffect.EnableFlash = false;
        baseShellBlock = baseShellObject.GetComponent<BlockInPhase1>();
        baseShellBlock.IsActived = true;
        brainShellCollider = brainShellObject.GetComponent<Collider2D>();
        brainShellCollider.enabled = false;

        SetColorCover(headShellMaterial, unactiveColor, unactiveColorAlpha);
        SetColorCover(brainShellMaterial, unactiveColor, unactiveColorAlpha);
        SetColorCover(baseShellMaterial, unactiveColor, unactiveColorAlpha);
    }

    private void ResolvePlayerReference()
    {
        if (GlobalPlayer.IsInitialized && GlobalPlayer.Instance.Player != null)
        {
            playerTransform = GlobalPlayer.Instance.Player.transform;
        }
    }

    private void RegisterAllBehaviours()
    {
        AddBehaviour(Sword1BehaviourName, 0f, BuildSword1Sequence());
        AddBehaviour(Sword2BehaviourName, 0f, BuildSword2Sequence());
        AddBehaviour(Lightning1BehaviourName, 0f, BuildLightning1Sequence());
        AddBehaviour(Lightning2BehaviourName, 0f, BuildLightning2Sequence());
        AddBehaviour(BigLightningBehaviourName, 0f, BuildBigLightningSequence());
        AddBehaviour(LaserBehaviourName, 0f, BuildLaserSequence());
        AddBehaviour(PhaseTransitionBehaviourName, 0f, BuildPhaseTransitionSequence());
    }

    // ---------------构建行为序列---------------

    private ActSeq BuildSword1Sequence()
    {
        var seq = new ActSeq();
        var action = seq.CreateActionNode(Sword1Routine);
        seq.Start.SetNext(action);
        action.SetNext(seq.End);
        return seq;
    }

    private ActSeq BuildSword2Sequence()
    {
        var seq = new ActSeq();
        var action = seq.CreateActionNode(Sword2Routine);
        seq.Start.SetNext(action);
        action.SetNext(seq.End);
        return seq;
    }

    private ActSeq BuildLightning1Sequence()
    {
        var seq = new ActSeq();
        var action = seq.CreateActionNode(Lightning1Routine);
        seq.Start.SetNext(action);
        action.SetNext(seq.End);
        return seq;
    }

    private ActSeq BuildLightning2Sequence()
    {
        var seq = new ActSeq();
        var action = seq.CreateActionNode(Lightning2Routine);
        seq.Start.SetNext(action);
        action.SetNext(seq.End);
        return seq;
    }

    private ActSeq BuildBigLightningSequence()
    {
        var seq = new ActSeq();
        var action = seq.CreateActionNode(BigLightningRoutine);
        seq.Start.SetNext(action);
        action.SetNext(seq.End);
        return seq;
    }

    private ActSeq BuildLaserSequence()
    {
        var seq = new ActSeq();
        var action = seq.CreateActionNode(LaserRoutine);
        seq.Start.SetNext(action);
        action.SetNext(seq.End);
        return seq;
    }

    private ActSeq BuildPhaseTransitionSequence()
    {
        var seq = new ActSeq();
        var action = seq.CreateActionNode(PhaseTransitionRoutine);
        seq.Start.SetNext(action);
        action.SetNext(seq.End);
        return seq;
    }

    // ---------------行为协程---------------

    private IEnumerator Sword1Routine(MonoBehaviour _)
    {
        for (int i = 0; i < 3; i++)
        {
            SpawnFlyingSwordInRange();
            if (i < 2)
            {
                yield return new WaitForSeconds(flyingSword1_SummonInterval);
            }
        }
        yield return new WaitForSeconds(flyingSword1_AfterAttackTime);
    }

    private IEnumerator Sword2Routine(MonoBehaviour _)
    {
        for (int i = 0; i < flyingSword2_SummonCount; i++)
        {
            SpawnFlyingSwordInRange();
        }
        yield return new WaitForSeconds(flyingSword2_AfterAttackTime);
    }

    private IEnumerator Lightning1Routine(MonoBehaviour _)
    {
        for (int i = 0; i < 4; i++)
        {
            SpawnLightningBall(ball =>
            {
                Vector2 dir = GetDirectionToPlayer(ball.transform.position);
                ball.SetLaunchDirection(dir);
            });
            if (i < 3)
            {
                yield return new WaitForSeconds(lightningBall1_SummonInterval);
            }
        }
        yield return new WaitForSeconds(lightningBall1_AfterAttackTime);
    }

    private IEnumerator Lightning2Routine(MonoBehaviour _)
    {
        float[] angleOffsets = { -lightningBall2_SummonAngle, 0f, lightningBall2_SummonAngle };
        for (int i = 0; i < 3; i++)
        {
            float offset = angleOffsets[i];
            SpawnLightningBall(ball =>
            {
                Vector2 dir = GetDirectionToPlayer(ball.transform.position);
                Vector2 rotated = RotateVector2(dir, offset);
                ball.SetLaunchDirection(rotated);
            });
            if (i < 2)
            {
                yield return new WaitForSeconds(lightningBall2_SummonInterval);
            }
        }
        yield return new WaitForSeconds(lightningBall2_AfterAttackTime);
    }

    private IEnumerator BigLightningRoutine(MonoBehaviour _)
    {
        SpawnBigLightningBall(ball =>
        {
            Vector2 dir = GetDirectionToPlayer(ball.transform.position);
            ball.SetLaunchDirection(dir);
        });
        yield return new WaitForSeconds(bigLightning_AfterAttackTime);
    }

    private IEnumerator LaserRoutine(MonoBehaviour _)
    {
        Vector3 spawnPos = transform.position + (Vector3)summonOffset;
        var obj = Instantiate(laserPrefab, spawnPos, Quaternion.identity);
        var laser = obj.GetComponent<Laser>();

        // 等待一帧，让 Laser.Start() 执行 actSeq.Play，使 IsActive 生效
        yield return null;

        bool sweepLeftToRight = UnityEngine.Random.value > 0.5f;
        float startAngle = sweepLeftToRight ? laser_SweepAngleMin : laser_SweepAngleMax;
        float endAngle = sweepLeftToRight ? laser_SweepAngleMax : laser_SweepAngleMin;

        float totalDuration = laser != null ? laser.TotalDuration : 1f;
        float elapsed = 0f;

        obj.transform.rotation = Quaternion.Euler(0f, 0f, startAngle);

        while (laser != null && laser.IsActive)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / totalDuration);
            float angle = Mathf.Lerp(startAngle, endAngle, t);
            obj.transform.rotation = Quaternion.Euler(0f, 0f, angle);
            yield return null;
        }

        if (obj != null)
        {
            Destroy(obj);
        }

        yield return new WaitForSeconds(laser_AfterAttackTime);
    }

    private IEnumerator PhaseTransitionRoutine(MonoBehaviour _)
    {
        StartInvincibleTimer(phaseTransitionTime + 1f);
        AudioManager.PlaySound(phaseTransitionSFX, transform.position, soundVolume);

        Vector3 headStart = headShellObject.transform.localPosition;
        Vector3 brainStart = brainShellObject.transform.localPosition;
        Vector3 headEnd = headStart + Vector3.up * phaseTransitionRiseDistance;
        Vector3 brainEnd = brainStart + Vector3.up * phaseTransitionRiseDistance;

        float elapsed = 0f;
        while (elapsed < phaseTransitionTime)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / phaseTransitionTime);
            headShellObject.transform.localPosition = Vector3.Lerp(headStart, headEnd, t);
            brainShellObject.transform.localPosition = Vector3.Lerp(brainStart, brainEnd, t);
            yield return null;
        }

        headShellObject.transform.localPosition = headEnd;
        brainShellObject.transform.localPosition = brainEnd;

        StopInvincibleTimer();
        baseShellFlashEffect.EnableFlash = false;
        brainShellCollider.enabled = true;
        baseShellBlock.IsActived = false;
        currentPhase = phase.phase2;
    }

    // ---------------决策逻辑---------------

    protected override string DecideNextBehaviour()
    {
        switch (currentPhase)
        {
            case phase.inactive:
                return null;

            case phase.phase1:
                if ((float)CurrentHP / MaxHP * 100f <= phase2HealthThreshold)
                {
                    return PhaseTransitionBehaviourName;
                }
                return UnityEngine.Random.value > 0.5f
                    ? Sword1BehaviourName
                    : Sword2BehaviourName;

            case phase.phase2:
                return UnityEngine.Random.Range(0, 4) switch
                {
                    0 => Lightning1BehaviourName,
                    1 => Lightning2BehaviourName,
                    2 => BigLightningBehaviourName,
                    _ => LaserBehaviourName,
                };

            default:
                return null;
        }
    }

    protected override void OnHitByPlayerAttack(HitInfo incoming)
    {
        base.OnHitByPlayerAttack(incoming);
        if (currentPhase == phase.inactive)
        {
            currentPhase = phase.phase1;
            SetColorCover(headShellMaterial, unactiveColor, 0f);
            SetColorCover(brainShellMaterial, unactiveColor, 0f);
            SetColorCover(baseShellMaterial, unactiveColor, 0f);
            baseShellFlashEffect.EnableFlash = true;
        }

        if (currentPhase == phase.phase1)
        {
            if (flashCoroutine != null) StopCoroutine(flashCoroutine);
            if ((float)CurrentHP / MaxHP * 100f <= phase2HealthThreshold)
            {
                baseShellFlashEffect.EnableFlash = false;
                flashCoroutine = StartCoroutine(FlashRoutine(1f, lightningFlashDuration * 2f, headShellMaterial, brainShellMaterial, baseShellMaterial));
                ForceRedecide();
            }
            else
            {
                flashCoroutine = StartCoroutine(FlashRoutine(headShellMaterial));
            }
        }
        else if (currentPhase == phase.phase2)
        {
            if (flashCoroutine != null) StopCoroutine(flashCoroutine);
            flashCoroutine = StartCoroutine(FlashRoutine(headShellMaterial, brainShellMaterial, baseShellMaterial));
        }
    }

    // ---------------辅助方法---------------

    private void SpawnFlyingSwordInRange()
    {
        Vector2 spawnPos = new Vector2(
            UnityEngine.Random.Range(flyingSword1_SpawnRangeMin.x, flyingSword1_SpawnRangeMax.x),
            UnityEngine.Random.Range(flyingSword1_SpawnRangeMin.y, flyingSword1_SpawnRangeMax.y));
        Instantiate(flyingSwordPrefab, spawnPos, Quaternion.identity);
    }

    private void SpawnLightningBall(Action<LightningBall> onChargeCompleted)
    {
        Vector3 spawnPos = transform.position + (Vector3)summonOffset;
        var obj = Instantiate(lightningBallPrefab, spawnPos, Quaternion.identity);
        var ball = obj.GetComponent<LightningBall>();
        if (ball != null && onChargeCompleted != null)
        {
            ball.ChargeCompleted += b => onChargeCompleted(b);
        }
    }

    private void SpawnBigLightningBall(Action<LightningBall> onChargeCompleted)
    {
        Vector3 spawnPos = transform.position + (Vector3)summonOffset;
        var obj = Instantiate(bigLightningPrefab, spawnPos, Quaternion.identity);
        var ball = obj.GetComponent<LightningBall>();
        if (ball != null && onChargeCompleted != null)
        {
            ball.ChargeCompleted += b => onChargeCompleted(b);
        }
    }

    private Vector2 GetDirectionToPlayer(Vector3 fromPosition)
    {
        ResolvePlayerReference();
        if (playerTransform == null)
        {
            return Vector2.right;
        }
        Vector2 delta = (Vector2)playerTransform.position - (Vector2)fromPosition;
        return delta.sqrMagnitude > 0f ? delta.normalized : Vector2.right;
    }

    private static Vector2 RotateVector2(Vector2 v, float angleDegrees)
    {
        float rad = angleDegrees * Mathf.Deg2Rad;
        float cos = Mathf.Cos(rad);
        float sin = Mathf.Sin(rad);
        return new Vector2(v.x * cos - v.y * sin, v.x * sin + v.y * cos);
    }

    private IEnumerator FlashRoutine(params Material[] materials)
    {
        AudioManager.PlaySound(hitSFX, transform.position, soundVolume);
        return FlashRoutine(lightningColorAlpha, lightningFlashDuration, materials);
    }

    private IEnumerator FlashRoutine(float startAlpha, float duration, params Material[] materials)
    {
        foreach (var mat in materials)
        {
            SetColorCover(mat, lightningColor, startAlpha);
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            float alpha = Mathf.Lerp(startAlpha, 0f, t);
            foreach (var mat in materials)
            {
                SetColorCover(mat, lightningColor, alpha);
            }
            yield return null;
        }

        foreach (var mat in materials)
        {
            SetColorCover(mat, lightningColor, 0f);
        }
    }

    private void SetColorCover(Material material, Color color, float coverrate)
    {
        material.SetColor("_flashColor", color);
        material.SetFloat("_flashFactor", coverrate);
    }
}
