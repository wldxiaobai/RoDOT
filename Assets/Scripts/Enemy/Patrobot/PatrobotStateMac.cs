using System;
using System.Collections;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class PatrobotStateMac : BaseEnemy
{
    private const string PatrolBehaviourName = "Patrol";
    private const string ChaseBehaviourName = "Chase";
    private const string AttackBehaviourName = "Attack";
    private const string IdleBehaviourName = "Idle";
    private const string HurtBehaviourName = "Hurt";

    [Header("动画参数")]
    [Tooltip("行走动画参数名")]
    [SerializeField] private string walkAnimParam = "IsWalking";
    [Tooltip("攻击动画名")]
    [SerializeField] private string attackAnimName = "attack";
    [Tooltip("受击动画名")]
    [SerializeField] private string hitAnimName = "hurt";
    [Tooltip("结束受击动画参数名")]
    [SerializeField] private string hitAnimEndParam = "StopHurt";

    [Header("视效设置")]
    [Tooltip("闪烁特效时长")]
    [SerializeField] private float flashDuration = 0.2f;
    [Tooltip("受伤闪烁颜色")]
    [SerializeField] private Color hurtFlashColor = new(1f, 0.5f, 0.5f, 1f);

    [Header("巡逻设置")]
    [Tooltip("巡逻中心相对于初始位置的偏移")]
    [SerializeField] private Vector2 patrolCenterOffset = Vector2.zero;
    [Tooltip("巡逻时半径")]
    [SerializeField] private float patrolRange = 3f;
    [Tooltip("巡逻移动速度")]
    [SerializeField] private float patrolSpeed = 2f;

    [Header("追击与检测")]
    [Tooltip("追击移动速度")]
    [SerializeField] private float chaseSpeed = 3.5f;
    [Tooltip("玩家检测半径")]
    [SerializeField] private float detectionRange = 5f;
    [Tooltip("追击丢失玩家时的范围系数（>=1）")]
    [SerializeField] private float detectionLossMultiplier = 1.25f;
    [Tooltip("备用查找玩家的标签")]
    [SerializeField] private string playerTag = "Player";

    [Header("攻击与待机")]
    [Tooltip("攻击持续时间")]
    [SerializeField] private float attackDuration = 0.7f;
    [Tooltip("攻击后待机时长")]
    [SerializeField] private float idleDuration = 0.4f;
    [Tooltip("判定攻击范围的碰撞体（可选）")]
    [SerializeField] private Collider2D attackRangeCollider;
    [Tooltip("未配置攻击碰撞体的近战距离")]
    [SerializeField] private float fallbackAttackRange = 1.2f;

    [Header("受伤设置")]
    [Tooltip("受伤后后退持续时间")]
    [SerializeField] private float hurtRetreatDuration = 0.5f;
    [Tooltip("受伤后后退移动速度")]
    [SerializeField] private float hurtRetreatSpeed = 2.6f;
    [Tooltip("受伤后短暂待机时间")]
    [SerializeField] private float hurtIdleAfterRetreat = 0.25f;

    private bool behavioursPrepared;
    private float _desiredVelocityX;
    private float _idleRemaining;
    private float _hurtRetreatRemaining;
    private bool _blockChaseDuringHurtIdle;
    private float _currentDirection = 1f;
    private Vector2 _patrolCenter;
    private bool _patrolCenterInitialized;
    private string _currentBehaviourName = PatrolBehaviourName;
    private Coroutine _flashCoroutine;
    private Material _material;

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

    private Rigidbody2D _rigidbody;
    private SpriteRenderer _spriteRenderer;
    private Transform _playerTransform;
    private Collider2D _playerCollider;

    private Rigidbody2D Rigidbody => _rigidbody != null ? _rigidbody : _rigidbody = GetComponent<Rigidbody2D>();
    private SpriteRenderer SpriteRenderer => _spriteRenderer != null ? _spriteRenderer : _spriteRenderer = GetComponent<SpriteRenderer>();

    protected override void EnemyInit()
    {
        _material = GetComponent<SpriteRenderer>().material;
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
        var running = IsCurrentBehaviour(PatrolBehaviourName) || IsCurrentBehaviour(ChaseBehaviourName);
        if (Animator != null && !string.IsNullOrWhiteSpace(walkAnimParam))
        {
            Animator.SetBool(walkAnimParam, running);
        }
    }

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

        if (!IsCurrentBehaviour(HurtBehaviourName))
        {
            _idleRemaining = Mathf.Max(0f, idleDuration);
            _blockChaseDuringHurtIdle = false;
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

    private IEnumerator HurtRoutine(MonoBehaviour _)
    {
        Debug.Log($"[{name}] 进入受伤状态");
        Animator?.Play(hitAnimName);
        ResolvePlayerReference();
        FlashEffect(Mathf.Max(0f, flashDuration), hurtFlashColor);

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
                    var idleTime = Mathf.Max(0f, hurtIdleAfterRetreat);
                    if (idleTime > 0f)
                    {
                        _idleRemaining = idleTime;
                        _blockChaseDuringHurtIdle = true;
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

            if (_blockChaseDuringHurtIdle)
            {
                if (_idleRemaining <= 0f)
                {
                    _blockChaseDuringHurtIdle = false;
                    SetBehaviourState(PatrolBehaviourName);
                }
            }
            else if (ShouldStartChase())
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

    private void ResolvePlayerReference()
    {
        if (_playerTransform != null)
        {
            return;
        }

        var playerState = FindObjectOfType<PlayerStateMachine>();
        if (playerState != null)
        {
            AssignPlayer(playerState.transform);
            return;
        }

        if (!string.IsNullOrWhiteSpace(playerTag))
        {
            var candidate = GameObject.FindGameObjectWithTag(playerTag);
            if (candidate != null)
            {
                AssignPlayer(candidate.transform);
            }
        }
    }

    protected override void OnHitByPlayerAttack(HitInfo incoming)
    {
        base.OnHitByPlayerAttack(incoming);
        EnsureBehaviours();

        _hurtRetreatRemaining = Mathf.Max(0f, hurtRetreatDuration);
        StartInvincibleTimer(_hurtRetreatRemaining);
        SetBehaviourState(HurtBehaviourName);
    }

    // 闪烁特效
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
