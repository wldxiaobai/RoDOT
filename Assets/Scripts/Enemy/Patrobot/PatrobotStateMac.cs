using System;
using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class PatrobotStateMac : BaseEnemy
{
    // --- Behaviour State Names ---
    private const string PatrolBehaviourName = "Patrol";
    private const string ChaseBehaviourName = "Chase";
    private const string AttackBehaviourName = "Attack";
    private const string IdleBehaviourName = "Idle";
    private const string HurtBehaviourName = "Hurt";

    // --- Serialized Animation Parameters ---
    [Header("动画参数")]
    [Tooltip("行走动画参数名")]
    [SerializeField] private string walkAnimParam = "IsWalking";
    [Tooltip("攻击动画名")]
    [SerializeField] private string attackAnimName = "attack";
    [Tooltip("受击动画名")]
    [SerializeField] private string hitAnimName = "hurt";
    [Tooltip("结束受击动画参数名")]
    [SerializeField] private string hitAnimEndParam = "StopHurt";

    // --- Visual Effect Configuration ---
    [Header("视效设置")]
    [Tooltip("闪烁特效时长")]
    [SerializeField] private float flashDuration = 0.2f;
    [Tooltip("受伤闪烁颜色")]
    [SerializeField] private Color hurtFlashColor = new(1f, 0.5f, 0.5f, 1f);
    [Tooltip("被格挡闪烁颜色")]
    [SerializeField] private Color blockFlashColor = new(1f, 1f, 1f, 1f);

    // --- Patrol Configuration ---
    [Header("巡逻设置")]
    [Tooltip("巡逻中心相对于初始位置的偏移")]
    [SerializeField] private Vector2 patrolCenterOffset = Vector2.zero;
    [Tooltip("巡逻时半径")]
    [SerializeField] private float patrolRange = 3f;
    [Tooltip("巡逻移动速度")]
    [SerializeField] private float patrolSpeed = 2f;

    // --- Chase and Detection Configuration ---
    [Header("追击与检测")]
    [Tooltip("追击移动速度")]
    [SerializeField] private float chaseSpeed = 3.5f;
    [Tooltip("玩家检测半径")]
    [SerializeField] private float detectionRange = 5f;
    [Tooltip("追击丢失玩家时的范围系数（>=1）")]
    [SerializeField] private float detectionLossMultiplier = 1.25f;

    // --- Attack and Idle Configuration ---
    [Header("攻击与待机")]
    [Tooltip("攻击持续时间")]
    [SerializeField] private float attackDuration = 0.7f;
    [Tooltip("攻击后待机时长")]
    [SerializeField] private float idleDuration = 0.4f;
    [Tooltip("判定攻击范围的碰撞体（可选）")]
    [SerializeField] private Collider2D attackRangeCollider;
    [Tooltip("未配置攻击碰撞体的近战距离")]
    [SerializeField] private float fallbackAttackRange = 1.2f;
    [Tooltip("攻击消息接收器")]
    [SerializeField] private AttackHitInfo attackMessageReceiver;

    // --- Block and Hurt Configuration ---
    [Header("被格挡与受伤")]
    [Tooltip("被格挡时后退距离")]
    [SerializeField] private float blockRetreatDistance = 0.5f;
    [Tooltip("被格挡时后退时长")]
    [SerializeField] private float blockRetreatDuration = 0.3f;
    [Tooltip("受伤后后退持续时间")]
    [SerializeField] private float hurtRetreatDuration = 0.5f;
    [Tooltip("受伤后后退移动速度")]
    [SerializeField] private float hurtRetreatSpeed = 2.6f;
    [Tooltip("受伤后短暂待机时间")]
    [SerializeField] private float hurtIdleAfterRetreat = 0.25f;

    // --- Runtime State ---
    private bool behavioursPrepared;
    private float _desiredVelocityX;
    private float _idleRemaining;
    private float _hurtRetreatRemaining;
    private bool _blockChaseDuringHurtIdle;
    private float _postHurtIdleDuration;
    private bool _blockChaseAfterHurt;
    private Coroutine _blockRetreatCoroutine;
    private float _currentDirection = 1f;
    private Vector2 _patrolCenter;
    private bool _patrolCenterInitialized;
    private string _currentBehaviourName = PatrolBehaviourName;
    private Coroutine _flashCoroutine;
    private Material _material;

    // --- Gizmo Colors ---
    private static readonly Color PatrolRangeColor = new(0.3f, 0.65f, 1f, 1f);
    private static readonly Color PatrolCenterColor = new(0.5f, 0.85f, 1f, 1f);
    private static readonly Color DetectionRangeColor = new(1f, 0.85f, 0.2f, 1f);
    private static readonly Color DetectionLossRangeColor = new(1f, 0.45f, 0.1f, 1f);
    private static readonly Color AttackFallbackRangeColor = new(1f, 0.2f, 0.2f, 1f);
    private static readonly Color AttackColliderOverlayColor = new(0.8f, 0f, 0f, 1f);

    // --- Behaviour Accessors ---
    private string CurrentBehaviourName => string.IsNullOrWhiteSpace(_currentBehaviourName)
        ? PatrolBehaviourName
        : _currentBehaviourName;

    private bool IsCurrentBehaviour(string behaviourName)
    {
        if (string.IsNullOrWhiteSpace(behaviourName))
        {
            return false;
        }

        return string.Equals(CurrentBehaviourName, behaviourName, StringComparison.Ordinal);
    }

    // --- Cached Components ---
    private Rigidbody2D _rigidbody;
    private SpriteRenderer _spriteRenderer;
    private Transform _playerTransform;
    private Collider2D _playerCollider;

    private Rigidbody2D Rigidbody => _rigidbody != null ? _rigidbody : _rigidbody = GetComponent<Rigidbody2D>();
    private SpriteRenderer SpriteRenderer => _spriteRenderer != null ? _spriteRenderer : _spriteRenderer = GetComponent<SpriteRenderer>();

    // --- Unity Lifecycle ---
    private void OnEnable()
    {
        if (attackMessageReceiver != null)
        {
            attackMessageReceiver.OnBlocked += HandleAttackBlocked;
        }
    }

    private void OnDisable()
    {
        if (attackMessageReceiver != null)
        {
            attackMessageReceiver.OnBlocked -= HandleAttackBlocked;
        }
    }

    protected override void EnemyInit()
    {
        _material = GetComponent<SpriteRenderer>().material;
        _postHurtIdleDuration = hurtIdleAfterRetreat;
        _blockChaseAfterHurt = true;
    }

    private void FixedUpdate()
    {
        if (Rigidbody == null)
        {
            return;
        }

        var velocity = Rigidbody.velocity;
        velocity.x = _desiredVelocityX;
        Rigidbody.velocity = velocity;
    }

    private void LateUpdate()
    {
        ResolvePlayerReference();
        var running = IsCurrentBehaviour(PatrolBehaviourName) || IsCurrentBehaviour(ChaseBehaviourName);
        if (Animator != null && !string.IsNullOrWhiteSpace(walkAnimParam))
        {
            Animator.SetBool(walkAnimParam, running);
        }
    }

    // --- Behaviour Management ---
    protected override string DecideNextBehaviour()
    {
        EnsureBehaviours();
        return CurrentBehaviourName;
    }

    private void EnsureBehaviours()
    {
        if (behavioursPrepared)
        {
            return;
        }

        behavioursPrepared = true;

        AddBehaviour(PatrolBehaviourName, 0f, BuildPatrolSequence());
        AddBehaviour(ChaseBehaviourName, 0f, BuildChaseSequence());
        AddBehaviour(AttackBehaviourName, 0f, BuildAttackSequence());
        AddBehaviour(IdleBehaviourName, 0f, BuildIdleSequence());
        AddBehaviour(HurtBehaviourName, 0f, BuildHurtSequence());
    }

    // --- Behaviour Sequences ---
    private ActSeq BuildPatrolSequence()
    {
        var seq = new ActSeq();
        var loop = seq.CreateDoWhileNode(PatrolLoop, _ => IsCurrentBehaviour(PatrolBehaviourName) && !ShouldStartChase());
        seq.Start.SetNext(loop);
        loop.SetNext(seq.End);
        return seq;
    }

    private ActSeq BuildChaseSequence()
    {
        var seq = new ActSeq();
        var loop = seq.CreateDoWhileNode(ChaseLoop, _ => IsCurrentBehaviour(ChaseBehaviourName));
        seq.Start.SetNext(loop);
        loop.SetNext(seq.End);
        return seq;
    }

    private ActSeq BuildAttackSequence()
    {
        var seq = new ActSeq();
        var action = seq.CreateActionNode(AttackRoutine);
        seq.Start.SetNext(action);
        action.SetNext(seq.End);
        return seq;
    }

    private ActSeq BuildIdleSequence()
    {
        var seq = new ActSeq();
        var action = seq.CreateActionNode(IdleRoutine);
        seq.Start.SetNext(action);
        action.SetNext(seq.End);
        return seq;
    }

    private ActSeq BuildHurtSequence()
    {
        var seq = new ActSeq();
        var action = seq.CreateActionNode(HurtRoutine);
        seq.Start.SetNext(action);
        action.SetNext(seq.End);
        return seq;
    }

    // --- Patrol Loop ---
    private IEnumerator PatrolLoop(MonoBehaviour _)
    {
        EnsurePatrolCenter();
        ResolvePlayerReference();

        if (patrolRange > 0f)
        {
            var offset = transform.position.x - _patrolCenter.x;
            if (offset >= patrolRange)
            {
                _currentDirection = -1f;
            }
            else if (offset <= -patrolRange)
            {
                _currentDirection = 1f;
            }
        }

        _desiredVelocityX = _currentDirection * Mathf.Max(0f, patrolSpeed);
        ApplyFacing(_currentDirection);

        if (ShouldStartChase())
        {
            SetBehaviourState(ChaseBehaviourName);
        }
        else
        {
            SetBehaviourState(PatrolBehaviourName);
        }

        yield return null;
    }

    // --- Chase Loop ---
    private IEnumerator ChaseLoop(MonoBehaviour _)
    {
        ResolvePlayerReference();
        if (_playerTransform == null)
        {
            SetBehaviourState(PatrolBehaviourName);
            yield break;
        }

        var deltaX = _playerTransform.position.x - transform.position.x;
        if (!Mathf.Approximately(deltaX, 0f))
        {
            _currentDirection = Mathf.Sign(deltaX);
        }

        _desiredVelocityX = _currentDirection * Mathf.Max(0f, chaseSpeed);
        ApplyFacing(_currentDirection);

        if (IsPlayerWithinAttackRange())
        {
            SetBehaviourState(AttackBehaviourName);
        }
        else if (!IsPlayerWithinRange(Mathf.Max(0f, detectionRange) * detectionLossMultiplier))
        {
            SetBehaviourState(PatrolBehaviourName);
        }
        else
        {
            SetBehaviourState(ChaseBehaviourName);
        }

        yield return null;
    }

    // --- Attack Routine ---
    private IEnumerator AttackRoutine()
    {
        SetBehaviourState(AttackBehaviourName);
        _desiredVelocityX = 0f;
        if (Animator != null && !string.IsNullOrWhiteSpace(attackAnimName))
        {
            Animator.Play(attackAnimName);
        }

        var timer = Mathf.Max(0f, attackDuration);
        while (timer > 0f && IsCurrentBehaviour(AttackBehaviourName))
        {
            timer -= Time.deltaTime;
            yield return null;
        }

        if (!IsCurrentBehaviour(HurtBehaviourName) && _blockRetreatCoroutine == null)
        {
            _idleRemaining = Mathf.Max(0f, idleDuration);
            _blockChaseDuringHurtIdle = true;
            if (_idleRemaining > 0f)
            {
                SetBehaviourState(IdleBehaviourName);
            }
            else
            {
                SetBehaviourState(PatrolBehaviourName);
            }
        }
    }

    // --- Hurt Routine ---
    private IEnumerator HurtRoutine(MonoBehaviour _)
    {
        Debug.Log($"[{name}] 进入受伤状态");
        Animator?.Play(hitAnimName);
        ResolvePlayerReference();
        var flashColor = hurtFlashColor;
        FlashEffect(Mathf.Max(0f, flashDuration), flashColor);

        while (IsCurrentBehaviour(HurtBehaviourName))
        {
            var danger = LastHitSource ?? _playerTransform;
            var lookDir = danger != null ? danger.position.x - transform.position.x : _currentDirection;
            if (!Mathf.Approximately(lookDir, 0f))
            {
                _currentDirection = Mathf.Sign(lookDir);
            }
            ApplyFacing(lookDir);

            if (_hurtRetreatRemaining > 0f)
            {
                var retreatDir = danger != null
                    ? Mathf.Sign(transform.position.x - danger.position.x)
                    : -_currentDirection;
                if (Mathf.Approximately(retreatDir, 0f))
                {
                    retreatDir = -_currentDirection;
                }

                _desiredVelocityX = retreatDir * Mathf.Max(0f, hurtRetreatSpeed);
                _hurtRetreatRemaining -= Time.deltaTime;

                if (_hurtRetreatRemaining <= 0f)
                {
                    _desiredVelocityX = 0f;
                    var idleTime = Mathf.Max(0f, _postHurtIdleDuration);
                    if (idleTime > 0f)
                    {
                        _idleRemaining = idleTime;
                        _blockChaseDuringHurtIdle = _blockChaseAfterHurt;
                        SetBehaviourState(IdleBehaviourName);
                    }
                    else
                    {
                        SetBehaviourState(PatrolBehaviourName);
                    }
                }
            }
            else
            {
                SetBehaviourState(PatrolBehaviourName);
            }

            yield return null;
        }

        Animator?.SetTrigger(hitAnimEndParam);
        yield return null;
    }

    // --- Idle Routine ---
    private IEnumerator IdleRoutine(MonoBehaviour _)
    {
        while (IsCurrentBehaviour(IdleBehaviourName) && _idleRemaining > 0f)
        {
            ResolvePlayerReference();
            if (!IsCurrentBehaviour(IdleBehaviourName))
            {
                break;
            }

            _desiredVelocityX = 0f;
            _idleRemaining -= Time.deltaTime;

            if (_blockChaseDuringHurtIdle && _idleRemaining <= 0f)
            {
                _blockChaseDuringHurtIdle = false;
            }

            if (!_blockChaseDuringHurtIdle && ShouldStartChase())
            {
                SetBehaviourState(ChaseBehaviourName);
            }
            else if (_idleRemaining <= 0f)
            {
                SetBehaviourState(PatrolBehaviourName);
            }

            yield return null;
        }
    }

    // --- Block Response ---
    private float DetermineBlockRetreatDirection(GameObject attacker)
    {
        var direction = -_currentDirection;
        if (attacker != null)
        {
            var deltaX = transform.position.x - attacker.transform.position.x;
            if (!Mathf.Approximately(deltaX, 0f))
            {
                direction = Mathf.Sign(deltaX);
            }
        }

        if (Mathf.Approximately(direction, 0f))
        {
            direction = -_currentDirection;
        }

        return direction;
    }

    private IEnumerator RunBlockRetreat(float retreatDirection)
    {
        var duration = Mathf.Max(0f, blockRetreatDuration);
        var start = Rigidbody.position;
        var target = start + Vector2.right * retreatDirection * blockRetreatDistance;
        _currentDirection = retreatDirection;
        ApplyFacing(-retreatDirection);

        if (duration <= 0f)
        {
            Rigidbody.MovePosition(target);
            _blockRetreatCoroutine = null;
            FinishBlockRetreat();
            yield break;
        }

        var elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            var t = Mathf.Clamp01(elapsed / duration);
            var next = Vector2.Lerp(start, target, t);
            Rigidbody.MovePosition(next);
            yield return null;
        }

        _blockRetreatCoroutine = null;
        FinishBlockRetreat();
    }

    private void FinishBlockRetreat()
    {
        _idleRemaining = Mathf.Max(0f, idleDuration);
        _blockChaseDuringHurtIdle = true;
        _desiredVelocityX = 0f;
        if (_idleRemaining > 0f)
        {
            SetBehaviourState(IdleBehaviourName);
        }
        else
        {
            SetBehaviourState(PatrolBehaviourName);
        }
    }

    // --- Player Tracking ---
    private void AssignPlayer(Transform player)
    {
        _playerTransform = player;
        if (player != null)
        {
            _playerCollider = player.GetComponent<Collider2D>();
        }
    }

    private bool ShouldStartChase()
    {
        return IsPlayerWithinRange(Mathf.Max(0f, detectionRange));
    }

    private bool IsPlayerWithinAttackRange()
    {
        if (_playerTransform == null)
        {
            return false;
        }

        if (attackRangeCollider != null)
        {
            if (attackRangeCollider.OverlapPoint(_playerTransform.position))
            {
                return true;
            }

            if (_playerCollider != null && attackRangeCollider.IsTouching(_playerCollider))
            {
                return true;
            }
        }

        var fallback = Mathf.Max(0f, fallbackAttackRange);
        if (fallback <= 0f)
        {
            return false;
        }

        var delta = _playerTransform.position - transform.position;
        return delta.sqrMagnitude <= fallback * fallback;
    }

    private bool IsPlayerWithinRange(float range)
    {
        if (_playerTransform == null || range <= 0f)
        {
            return false;
        }

        var delta = _playerTransform.position - transform.position;
        return delta.sqrMagnitude <= range * range;
    }

    private void ApplyFacing(float direction)
    {
        if (SpriteRenderer == null || Mathf.Approximately(direction, 0f))
        {
            return;
        }

        SpriteRenderer.flipX = direction > 0f;
    }

    private void SetBehaviourState(string newBehaviour)
    {
        var normalized = string.IsNullOrWhiteSpace(newBehaviour) ? PatrolBehaviourName : newBehaviour;
        if (string.Equals(_currentBehaviourName, normalized, StringComparison.Ordinal))
        {
            return;
        }

        var previous = _currentBehaviourName;
        _currentBehaviourName = normalized;
        Debug.Log($"[{name}] PatrobotState {previous} → {_currentBehaviourName} (BaseState: {CurrentEnemyStateName})");
    }

    private void EnsurePatrolCenter()
    {
        if (_patrolCenterInitialized)
        {
            return;
        }

        _patrolCenter = (Vector2)transform.position + patrolCenterOffset;
        _patrolCenterInitialized = true;
    }

    // --- Player Reference ---
    private void ResolvePlayerReference()
    {
        var player = GlobalPlayer.Instance?.Player;
        if (player == null)
        {
            if (_playerTransform != null)
            {
                _playerTransform = null;
                _playerCollider = null;
            }
            return;
        }

        if (_playerTransform == player.transform)
        {
            return;
        }

        AssignPlayer(player.transform);
    }

    // --- Damage Handling ---
    protected override void OnHitByPlayerAttack(HitInfo incoming)
    {
        base.OnHitByPlayerAttack(incoming);
        EnsureBehaviours();
        _postHurtIdleDuration = Mathf.Max(0f, hurtIdleAfterRetreat);
        _blockChaseAfterHurt = true;

        _hurtRetreatRemaining = Mathf.Max(0f, hurtRetreatDuration);
        StartInvincibleTimer(_hurtRetreatRemaining);
        SetBehaviourState(HurtBehaviourName);
    }

    // --- Blocked Attack Handling ---
    private void HandleAttackBlocked(GameObject attacker)
    {
        EnsureBehaviours();
        if (_blockRetreatCoroutine != null)
        {
            StopCoroutine(_blockRetreatCoroutine);
            _blockRetreatCoroutine = null;
        }

        FlashEffect(Mathf.Max(0f, flashDuration), blockFlashColor);
        var retreatDirection = DetermineBlockRetreatDirection(attacker);
        _blockRetreatCoroutine = StartCoroutine(RunBlockRetreat(retreatDirection));
    }

    // --- Visual Effects ---
    private void FlashEffect(float duration, Color color)
    {
        _flashCoroutine = StartCoroutine(FlashEffectCoroutine(duration, color));
    }

    IEnumerator FlashEffectCoroutine(float duration, Color color)
    {
        float elapsed = 0f;
        _material.SetColor("_flashColor", color);
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            _material.SetFloat("_flashFactor", Mathf.Lerp(1f, 0f, elapsed / duration));
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
        _material.SetFloat("_flashFactor", 0f);
    }

    // --- Debug Visualization ---
    private void OnDrawGizmos()
    {
        DrawPatrolGizmos();
        DrawDetectionGizmos();
        DrawAttackGizmos();
    }

    private void DrawPatrolGizmos()
    {
        var center = (Vector2)transform.position + patrolCenterOffset;
        if (patrolRange > 0f)
        {
            Gizmos.color = PatrolRangeColor;
            Gizmos.DrawWireSphere(center, patrolRange);
        }

        Gizmos.color = PatrolCenterColor;
        DrawCross(center, 0.15f);
    }

    private void DrawDetectionGizmos()
    {
        if (detectionRange <= 0f)
        {
            return;
        }

        var center = transform.position;
        Gizmos.color = DetectionRangeColor;
        Gizmos.DrawWireSphere(center, detectionRange);

        var lossRange = detectionRange * detectionLossMultiplier;
        if (lossRange > detectionRange)
        {
            Gizmos.color = DetectionLossRangeColor;
            Gizmos.DrawWireSphere(center, lossRange);
        }
    }

    private void DrawAttackGizmos()
    {
        var fallback = Mathf.Max(0f, fallbackAttackRange);
        if (fallback > 0f)
        {
            Gizmos.color = AttackFallbackRangeColor;
            Gizmos.DrawWireSphere(transform.position, fallback);
        }

        if (attackRangeCollider != null)
        {
            Gizmos.color = AttackColliderOverlayColor;
            var bounds = attackRangeCollider.bounds;
            Gizmos.DrawWireCube(bounds.center, bounds.size);
        }
    }

    private void DrawCross(Vector3 center, float size)
    {
        var half = size * 0.5f;
        Gizmos.DrawLine(center + Vector3.left * half, center + Vector3.right * half);
        Gizmos.DrawLine(center + Vector3.up * half, center + Vector3.down * half);
    }

    // --- Validation ---
    private void OnValidate()
    {
        patrolRange = Mathf.Max(0f, patrolRange);
        patrolSpeed = Mathf.Max(0f, patrolSpeed);
        chaseSpeed = Mathf.Max(0f, chaseSpeed);
        detectionRange = Mathf.Max(0f, detectionRange);
        detectionLossMultiplier = Mathf.Max(1f, detectionLossMultiplier);
        attackDuration = Mathf.Max(0f, attackDuration);
        idleDuration = Mathf.Max(0f, idleDuration);
        fallbackAttackRange = Mathf.Max(0f, fallbackAttackRange);
        hurtRetreatDuration = Mathf.Max(0f, hurtRetreatDuration);
        hurtRetreatSpeed = Mathf.Max(0f, hurtRetreatSpeed);
        hurtIdleAfterRetreat = Mathf.Max(0f, hurtIdleAfterRetreat);
    }
}
