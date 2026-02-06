using System.Collections;
using UnityEngine;

public class Ex_Enemy : BaseEnemy
{
    [Header("样例敌怪设置")]
    [SerializeField, Min(0.1f)] private float patrolRadius = 4f;
    [SerializeField, Min(0.1f)] private float patrolMoveDuration = 2f;
    [SerializeField, Min(0.1f)] private float approachStepDistance = 1f;
    [SerializeField, Min(0.05f)] private float approachStepDuration = 0.25f;
    [SerializeField, Min(0.1f)] private float attackRange = 1.5f;
    [SerializeField, Min(0f)] private float attackHoldTime = 0.35f;
    [SerializeField, Min(0.1f)] private float retreatDistance = 2.5f;
    [SerializeField, Min(0.05f)] private float retreatDuration = 0.35f;
    [SerializeField, Min(0.1f)] private float deathFadeDuration = 0.55f;

    private const string PatrolBehaviourName = "Patrol";
    private const string ApproachBehaviourName = "Approach";
    private const string AttackBehaviourName = "Attack";
    private const string RetreatBehaviourName = "Retreat";

    private readonly Color patrolColor = Color.white;
    private readonly Color approachColor = Color.gray;
    private readonly Color attackColor = Color.red;
    private readonly Color retreatColor = Color.yellow;

    private Vector2 patrolCenter;
    private bool behavioursInitialized;
    private PlayerStateMachine cachedPlayer;
    private SpriteRenderer spriteRenderer;
    private string lastBehaviour = PatrolBehaviourName;

    private Transform PlayerTransform => (cachedPlayer ??= FindObjectOfType<PlayerStateMachine>())?.transform;
    private bool HasPlayer => PlayerTransform != null;

    private void OnEnable()
    {
        spriteRenderer ??= GetComponent<SpriteRenderer>();
        patrolCenter = transform.position;
        LogDebug("OnEnable; patrol center reset.");

        if (!behavioursInitialized)
        {
            RegisterBehaviours();
            behavioursInitialized = true;
        }
    }

    private void RegisterBehaviours()
    {
        LogDebug("RegisterBehaviour called.");
        AddBehaviour(PatrolBehaviourName, 0f, BuildPatrolSequence());
        AddBehaviour(ApproachBehaviourName, 0f, BuildApproachSequence());
        AddBehaviour(AttackBehaviourName, 0f, BuildAttackSequence());
        AddBehaviour(RetreatBehaviourName, 0f, BuildRetreatSequence());
        SetDeath(BuildDeathSequence());
    }

    protected override string DecideNextBehaviour()
    {
        if (!IsPlayerInPatrolRange())
        {
            lastBehaviour = PatrolBehaviourName;
            LogDebug("Player out of patrol range; returning to patrol.");
            return PatrolBehaviourName;
        }

        switch (lastBehaviour)
        {
            case PatrolBehaviourName:
                lastBehaviour = ApproachBehaviourName;
                LogDebug("Player detected during patrol; switching to approach.");
                return ApproachBehaviourName;
            case ApproachBehaviourName:
                lastBehaviour = AttackBehaviourName;
                LogDebug("Approach finished; switching to attack.");
                return AttackBehaviourName;
            case AttackBehaviourName:
                lastBehaviour = RetreatBehaviourName;
                LogDebug("Attack finished; switching to retreat.");
                return RetreatBehaviourName;
            case RetreatBehaviourName:
                lastBehaviour = ApproachBehaviourName;
                LogDebug("Retreat finished; looping back to approach.");
                return ApproachBehaviourName;
            default:
                lastBehaviour = ApproachBehaviourName;
                LogDebug("未识别当前行为，强制切换到逼近。");
                return ApproachBehaviourName;
        }
    }

    private ActSeq BuildPatrolSequence()
    {
        var sequence = new ActSeq();
        var patrolNode = sequence.CreateActionNode(PatrolRoutine);
        sequence.Start.SetNext(patrolNode);
        patrolNode.SetNext(sequence.End);
        return sequence;
    }

    private ActSeq BuildApproachSequence()
    {
        var sequence = new ActSeq();
        var approachNode = sequence.CreateActionNode(ApproachRoutine);
        sequence.Start.SetNext(approachNode);
        approachNode.SetNext(sequence.End);
        return sequence;
    }

    private ActSeq BuildAttackSequence()
    {
        var sequence = new ActSeq();
        var attackNode = sequence.CreateActionNode(AttackRoutine);
        sequence.Start.SetNext(attackNode);
        attackNode.SetNext(sequence.End);
        return sequence;
    }

    private ActSeq BuildRetreatSequence()
    {
        var sequence = new ActSeq();
        var retreatNode = sequence.CreateActionNode(RetreatRoutine);
        sequence.Start.SetNext(retreatNode);
        retreatNode.SetNext(sequence.End);
        return sequence;
    }

    private ActSeq BuildDeathSequence()
    {
        var sequence = new ActSeq();
        var deathNode = sequence.CreateActionNode(DeathRoutine);
        sequence.Start.SetNext(deathNode);
        deathNode.SetNext(sequence.End);
        return sequence;
    }

    private IEnumerator PatrolRoutine()
    {
        ApplyColor(patrolColor);
        LogDebug("PatrolRoutine 启动。");
        var leftPoint = patrolCenter + Vector2.left * patrolRadius;
        var rightPoint = patrolCenter + Vector2.right * patrolRadius;
        var target = transform.position.x <= patrolCenter.x ? rightPoint : leftPoint;

        while (!IsPlayerInPatrolRange())
        {
            var startPosition = (Vector2)transform.position;
            float elapsed = 0f;
            bool reachedTarget = true;

            while (elapsed < patrolMoveDuration)
            {
                if (IsPlayerInPatrolRange())
                {
                    reachedTarget = false;
                    break;
                }

                float t = Mathf.Clamp01(elapsed / patrolMoveDuration);
                transform.position = Vector2.Lerp(startPosition, target, t);
                elapsed += Time.deltaTime;
                yield return null;
            }

            if (!reachedTarget)
            {
                LogDebug("PatrolRoutine 跳出：玩家触发警戒范围。");
                yield break;
            }

            transform.position = target;
            target = target == rightPoint ? leftPoint : rightPoint;
        }

        LogDebug("PatrolRoutine 结束：玩家进入范围准备切换行为。");
    }

    private IEnumerator ApproachRoutine()
    {
        ApplyColor(approachColor);
        LogDebug("ApproachRoutine 启动。");
        while (HasPlayer && !IsPlayerWithinAttackRange())
        {
            var start = (Vector2)transform.position;
            if (!HasPlayer)
            {
                LogDebug("ApproachRoutine 提前退出：找不到玩家。");
                yield break;
            }

            var direction = ((Vector2)PlayerTransform.position - start).normalized;
            if (direction == Vector2.zero)
            {
                LogDebug("ApproachRoutine 提前退出：玩家位置与自己重合。");
                yield break;
            }

            var target = start + direction * approachStepDistance;
            float elapsed = 0f;

            while (elapsed < approachStepDuration)
            {
                if (!HasPlayer || IsPlayerWithinAttackRange() || !IsPlayerInPatrolRange())
                {
                    LogDebug("ApproachRoutine 退出：玩家进入攻击范围或失联。");
                    yield break;
                }

                float t = Mathf.Clamp01(elapsed / approachStepDuration);
                transform.position = Vector2.Lerp(start, target, t);
                elapsed += Time.deltaTime;
                yield return null;
            }

            transform.position = target;
        }

        LogDebug("ApproachRoutine 完成：玩家已经在攻击范围。");
    }

    private IEnumerator AttackRoutine()
    {
        ApplyColor(attackColor);
        LogDebug("AttackRoutine 启动。");
        float elapsed = 0f;
        while (elapsed < attackHoldTime)
        {
            elapsed += Time.deltaTime;
            yield return null;
        }
        LogDebug("AttackRoutine 结束：攻击动作完成。");
    }

    private IEnumerator RetreatRoutine()
    {
        ApplyColor(retreatColor);
        LogDebug("RetreatRoutine 启动。");
        var start = (Vector2)transform.position;
        Vector2 direction = HasPlayer ? (start - (Vector2)PlayerTransform.position).normalized : Vector2.left;
        if (direction == Vector2.zero)
        {
            direction = Vector2.left;
            LogDebug("RetreatRoutine 方向计算失败，使用默认向左。");
        }

        var target = start + direction * retreatDistance;
        float elapsed = 0f;

        while (elapsed < retreatDuration)
        {
            float t = Mathf.Clamp01(elapsed / retreatDuration);
            transform.position = Vector2.Lerp(start, target, t);
            elapsed += Time.deltaTime;
            yield return null;
        }

        transform.position = target;
        LogDebug("RetreatRoutine 结束：完成后撤。");
    }

    private IEnumerator DeathRoutine()
    {
        LogDebug("DeathRoutine 启动；开始淡出。");
        var renderer = spriteRenderer ??= GetComponent<SpriteRenderer>();
        Color startColor = renderer != null ? renderer.color : Color.white;
        float elapsed = 0f;

        while (elapsed < deathFadeDuration)
        {
            if (renderer != null)
            {
                renderer.color = Color.Lerp(startColor, Color.clear, elapsed / deathFadeDuration);
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        if (renderer != null)
        {
            renderer.color = Color.clear;
        }

        LogDebug("DeathRoutine 完成；颜色设为透明。");
    }

    private void ApplyColor(Color color)
    {
        var renderer = spriteRenderer ??= GetComponent<SpriteRenderer>();
        if (renderer != null)
        {
            renderer.color = color;
        }
    }

    private bool IsPlayerInPatrolRange()
    {
        if (!HasPlayer)
        {
            return false;
        }

        return Vector2.Distance(PlayerTransform.position, patrolCenter) <= patrolRadius;
    }

    private bool IsPlayerWithinAttackRange()
    {
        if (!HasPlayer)
        {
            return false;
        }

        return Vector2.Distance(PlayerTransform.position, transform.position) <= attackRange;
    }

    private void LogDebug(string message)
    {
        Debug.Log($"[Ex_Enemy-{name}] {message}");
    }
}
