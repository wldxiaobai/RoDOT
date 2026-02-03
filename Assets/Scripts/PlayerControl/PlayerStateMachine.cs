using System;
using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using static AttackHitInfo;

public class PlayerStateMachine : MonoBehaviour
{
    // ==== 配置项: Serializable fields ==== //
    [Header("动画设置")]
    [Tooltip("攻击动画参数名")]
    [SerializeField] private string attackAnimParam = "attack";
    [Tooltip("跳跃动画参数名")]
    [SerializeField] private string jumpAnimParam = "jump";
    [Tooltip("格挡动画名")]
    [SerializeField] private string blockAnimParam = "block";
    [Tooltip("行走动画参数名")]
    [SerializeField] private string walkAnimParam = "walking";
    [Tooltip("腾空动画参数名")]
    [SerializeField] private string floatAnimParam = "floating";
    [Tooltip("冲刺动画名")]
    [SerializeField] private string dashAnimName = "dash";

    [Header("按键绑定")]
    [Tooltip("左移按键")]
    [SerializeField] private KeyCode moveLeftKey = KeyCode.A;
    [Tooltip("右移按键")]
    [SerializeField] private KeyCode moveRightKey = KeyCode.D;
    [Tooltip("普攻按键")]
    [SerializeField] private KeyCode attackKey = KeyCode.J;
    [Tooltip("跳跃按键")]
    [SerializeField] private KeyCode jumpKey = KeyCode.K;
    [Tooltip("冲刺按键")]
    [SerializeField] private KeyCode dashKey = KeyCode.L;
    [Tooltip("格挡按键")]
    [SerializeField] private KeyCode blockKey = KeyCode.I;

    [Header("水平移动")]
    [Tooltip("移动速度")]
    [SerializeField] private float walkSpeed = 3f;
    [Tooltip("冲刺距离")]
    [SerializeField] private float dashDistance = 3f;
    [Tooltip("冲刺时长")]
    [SerializeField] private float dashDuration = 0.25f;
    [Tooltip("冲刺预输入时长")]
    [SerializeField] private float dashInputBuffer = 0.15f;
    [Tooltip("冲刺内置CD")]
    [SerializeField] private float dashCooldown = 0.3f;

    [Header("跳跃")]
    [Tooltip("跳跃预输入时长")]
    [SerializeField] private float jumpInputBuffer = 0.15f;
    [Tooltip("起跳速度")]
    [SerializeField] private float jumpVelocity = 7f;
    [Tooltip("强制上升时长（秒）")]
    [SerializeField] private float jumpHoldTime = 0.2f;
    [Tooltip("短跳速度折损系数")]
    [SerializeField] private float shortHopFactor = 0.25f;
    [Tooltip("重力加速度")]
    [SerializeField] private float gravity = 30f;

    [Header("攻击")]
    [Tooltip("攻击预输入时长")]
    [SerializeField] private float attackInputBuffer = 0.1f;
    [Tooltip("地面攻击持续时长")]
    [SerializeField] private float attackDuration = 0.5f;
    [Tooltip("浮空攻击持续时长")]
    [SerializeField] private float floatAttackDuration = 0.5f;

    [Header("格挡")]
    [Tooltip("格挡预输入时长")]
    [SerializeField] private float blockInputBuffer = 0.15f;
    [Tooltip("格挡判定时长")]
    [SerializeField] private float blockDuration = 0.4f;
    [Tooltip("闪白特效时长")]
    [SerializeField] private float flashDuration = 0.2f;
    [Tooltip("格挡成功无敌时间")]
    [SerializeField] private float blockSucceededDuration = 0.6f;
    [Tooltip("弹反成功动作时长")]
    [SerializeField] private float parrySucceededDuration = 0.8f;
    [Tooltip("弹反失败硬直时长")]
    [SerializeField] private float parryFailDuration = 0.8f;

    [Header("受伤")]
    [Tooltip("受伤击退距离")]
    [SerializeField] private float hurtKbDistance = 2f;
    [Tooltip("受伤击退僵直时长")]
    [SerializeField] private float hurtDuration = 0.3f;
    [Tooltip("无敌时间")]
    [SerializeField] private float invincibleDuration = 1f;



    // ==== 内部状态数据 ==== //
    private const string GroundStateName = "Ground";
    private const string StandStateName = "Stand";
    private const string WalkStateName = "Walk";
    private const string AirStateName = "Air";
    private const string JumpSubStateName = "Jump";
    private const string FloatingSubStateName = "Floating";
    private const string AttackStateName = "Attack";
    private const string AttackGroundSubStateName = "AttackGround";
    private const string AttackFloatSubStateName = "FloatAttack";

    private Animator _animator;
    private HierarchicalStateMachine _stateMachine;
    private HierarchicalStateMachine _groundMovementState;
    private HierarchicalStateMachine _airMovementState;
    private HierarchicalStateMachine _attackStateMachine;
    private Vector2 _movementInput;
    private float _facingDirection = 1f;
    private Coroutine _dashCoroutine;
    private Coroutine _attackCoroutine;
    private Coroutine _floatAttackCoroutine;
    private Coroutine _hurtCoroutine;
    private ActSeq blockActionChain = new();
    private float _defaultAnimatorSpeed;
    private bool _pendingFloatAttack;

    private bool tryCatchInfo = false;
    private HitInfo incomingHitInfo = new();
    private HitInfo BlockedHitInfo = new();

    public string ActiveState
    {
        get
        {
            if (_stateMachine == null)
            {
                return string.Empty;
            }

            return _stateMachine.CurrentStateName;
        }
    }
    
    private SpriteRenderer _spriteRenderer;
    private Material _material;
    private Color _spriteBaseColor = Color.white;
    private bool _invincibleAlphaActive;

    private readonly HashSet<Collider2D> _groundContacts = new();
    private const float GroundNormalThreshold = 0.75f;

    private Vector2 vel;
    private bool _shortHopApplied;

    private bool grounded = false;
    private Timer jumpHoldTimer = new();
    private Timer dashCDTimer = new();
    private Timer invincibleTimer = new();

    private bool _skipJumpExitVelocityReset;
    private bool _preserveJumpHoldTimer;

    // ==== Unity 生命周期 ==== //
    private void Awake()
    {
        _animator = GetComponent<Animator>();
        _spriteRenderer = GetComponent<SpriteRenderer>();
        _material = _spriteRenderer.material;
        _defaultAnimatorSpeed = _animator != null ? _animator.speed : 1f;
        BuildStateMachine();
        BuildBlockActionChain();
    }

    // ==== Unity 事件 ==== //
    private void Start()
    {
        grounded = false;
        _stateMachine.Enter();
    }

    // ==== 主循环: 输入与状态更新 ==== //
    private void Update()
    {
        jumpHoldTimer.Update();
        dashCDTimer.Update();
        invincibleTimer.Update();
        vel = GetComponent<Rigidbody2D>().velocity;
        UpdateInput();
        _stateMachine.Stay();
        GetComponent<Rigidbody2D>().velocity = vel;
        InvincibleEffect();
    }

    private void InvincibleEffect()
    {
        _invincibleAlphaActive = invincibleTimer.IsRunning;
        ApplySpriteColor();
    }

    // ==== 输入处理 ==== //
    private void UpdateInput()
    {
        float horizontal = 0f;
        var activeState = ActiveState;
        if(activeState != "Block")
        {
            if (Input.GetKey(moveLeftKey))
            {
                horizontal -= 1f;
            }

            if (Input.GetKey(moveRightKey))
            {
                horizontal += 1f;
            }

            if (activeState != "FloatAttack" && activeState != "Attack")
            {
                if (horizontal < 0f)
                {
                    _facingDirection = -1f;
                }
                else if (horizontal > 0f)
                {
                    _facingDirection = 1f;
                }
                _spriteRenderer.flipX = _facingDirection > 0f;
            }
        }

        _movementInput = horizontal != 0f ? new Vector2(horizontal, 0f) : Vector2.zero;

        // 站立 <=> 行走
        if (activeState == StandStateName || activeState == WalkStateName)
        {
            var goalState = horizontal != 0f ? WalkStateName : StandStateName;
            _animator.SetBool(walkAnimParam, horizontal != 0f);
            if (activeState != goalState)
            {
                SwitchToGroundSubState(goalState);
            }
        }

        // (站立 || 行走) => 跳跃
        _animator.SetBool(floatAnimParam, !grounded);

        if (!grounded && (activeState == StandStateName || activeState == WalkStateName))
        {
            SwitchToAirSubState(FloatingSubStateName);
        }

        if (InputPreInput.GetKeyDown(jumpKey, jumpInputBuffer) &&
            (activeState == StandStateName || activeState == WalkStateName))
        {
            InputPreInput.ConsumeBufferedKeyDown(jumpKey);
            SwitchToAirSubState(JumpSubStateName);
        }

        // (站立 || 行走) => 冲刺
        if (InputPreInput.GetKeyDown(dashKey, dashInputBuffer) &&
            !dashCDTimer.IsRunning &&
            Mathf.Abs(_facingDirection) > Mathf.Epsilon &&
            (activeState == StandStateName || activeState == WalkStateName))
        {
            _stateMachine.TransitionTo("Dash");
            InputPreInput.ConsumeBufferedKeyDown(dashKey);
        }

        // (站立 || 行走) => 格挡
        if (InputPreInput.GetKeyDown(blockKey, blockInputBuffer) && (activeState == StandStateName || activeState == WalkStateName))
        {
            _stateMachine.TransitionTo("Block");
            InputPreInput.ConsumeBufferedKeyDown(blockKey);
        }

        // (站立 || 行走) => 普攻
        if (InputPreInput.GetKeyDown(attackKey, attackInputBuffer) && (activeState == StandStateName || activeState == WalkStateName))
        {
            _pendingFloatAttack = false;
            _stateMachine.TransitionTo(AttackStateName);
            InputPreInput.ConsumeBufferedKeyDown(attackKey);
        }

        if (InputPreInput.GetKeyDown(attackKey, attackInputBuffer) && (activeState == JumpSubStateName || activeState == FloatingSubStateName))
        {
            _skipJumpExitVelocityReset = true;
            _preserveJumpHoldTimer = true;
            _pendingFloatAttack = true;
            InputPreInput.ConsumeBufferedKeyDown(attackKey);
            _stateMachine.TransitionTo(AttackStateName);
        }
    }

    // ==== 状态机结构 ==== //
    private void BuildStateMachine()
    {
        _stateMachine = new HierarchicalStateMachine("Player");

        var standState = new SimpleState(
            StandStateName,
            enter: () =>
            {
                SetSpriteColor(Color.white);
            },
            stay: StayStand);

        var walkState = new SimpleState(
            WalkStateName,
            enter: () =>
            {
                SetSpriteColor(Color.gray);
            },
            stay: StayWalk);

        _groundMovementState = new HierarchicalStateMachine(GroundStateName)
            .RegisterState(standState, true)
            .RegisterState(walkState);

        var groundState = new HierarchicalStateProxy(_groundMovementState);

        var floatingSubState = new SimpleState(
            FloatingSubStateName,
            stay: StayFloating);

        var jumpSubState = new SimpleState(
            JumpSubStateName,
            enter: StartJump,
            stay: StayJump);

        _airMovementState = new HierarchicalStateMachine(AirStateName)
            .RegisterState(floatingSubState, true)
            .RegisterState(jumpSubState);

        var airState = new HierarchicalStateProxy(
            _airMovementState,
            exit: ExitAirState);

        var dashState = new SimpleState(
            "Dash",
            enter: StartDash,
            stay: StayDash,
            exit: DashExit);

        _attackStateMachine = new HierarchicalStateMachine("Attack")
            .RegisterState(new SimpleState(
                AttackGroundSubStateName,
                enter: StartAttack,
                stay: StayAttack,
                exit: ExitAttack), true)
            .RegisterState(new SimpleState(
                AttackFloatSubStateName,
                enter: StartFloatAttack,
                stay: StayFloatAttack,
                exit: ExitFloatAttack));

        var attackState = new SimpleState(
            AttackStateName,
            enter: EnterAttackParentState,
            stay: () => _attackStateMachine?.Stay(),
            exit: () => _attackStateMachine?.Exit());

        var blockState = new SimpleState(
            "Block",
            enter: StartBlock,
            stay: StayBlock,
            exit: ExitBlock);

        var hurtState = new SimpleState(
            "Hurt",
            enter: StartHurt,
            stay: StayHurt,
            exit: ExitHurt);

        _stateMachine
            .RegisterState(groundState, true)
            .RegisterState(airState)
            .RegisterState(dashState)
            .RegisterState(attackState)
            .RegisterState(blockState)
            .RegisterState(hurtState);
    }

    // ==== 基础移动状态 ==== //
    private void StayStand()
    {
        ApplyHorizontalMovement();
        if (!grounded)
        {
            SwitchToAirSubState(FloatingSubStateName);
        }
    }

    private void StayWalk()
    {
        ApplyHorizontalMovement();
        if (!grounded)
        {
            SwitchToAirSubState(FloatingSubStateName);
        }
    }

    private void ApplyHorizontalMovement()
    {
        vel.x = _movementInput.x * walkSpeed;
    }

    // ==== 跳跃逻辑 ==== //
    private void StartJump()
    {
        vel.y = jumpVelocity;
        _animator.SetTrigger(jumpAnimParam);
        jumpHoldTimer.StartTimer(jumpHoldTime);
        _shortHopApplied = false;
    }

    private void StayJump()
    {
        ApplyHorizontalMovement();
        UpdateJumpVerticalMotion();

        if ((vel.y <= 0f || !Input.GetKey(jumpKey)) && !_airMovementState.CurrentStateName.Equals(FloatingSubStateName))
        {
            _airMovementState.TransitionTo(FloatingSubStateName);
            return;
        }

        if (grounded && vel.y <= 0f)
        {
            var nextState = _movementInput == Vector2.zero ? StandStateName : WalkStateName;
            SwitchToGroundSubState(nextState);
        }
    }

    private void StayFloating()
    {
        ApplyHorizontalMovement();
        UpdateJumpVerticalMotion();

        if (grounded && vel.y <= 0f)
        {
            var nextState = _movementInput == Vector2.zero ? StandStateName : WalkStateName;
            SwitchToGroundSubState(nextState);
        }
    }

    private void ExitAirState()
    {
        if (!_skipJumpExitVelocityReset)
        {
            vel.y = 0f;
        }

        if (!_preserveJumpHoldTimer)
        {
            jumpHoldTimer.StopTimer();
        }

        _shortHopApplied = false;
        _skipJumpExitVelocityReset = false;
        _preserveJumpHoldTimer = false;
    }

    private void SwitchToGroundSubState(string targetSubState)
    {
        if (string.IsNullOrWhiteSpace(targetSubState) ||
            _groundMovementState == null ||
            _stateMachine == null)
        {
            return;
        }

        if (_stateMachine.CurrentStateName != GroundStateName)
        {
            _stateMachine.TransitionTo(GroundStateName);
        }

        _groundMovementState.TransitionTo(targetSubState);
    }

    private void SwitchToAirSubState(string targetSubState)
    {
        if (string.IsNullOrWhiteSpace(targetSubState) ||
            _airMovementState == null ||
            _stateMachine == null)
        {
            return;
        }

        if (_stateMachine.CurrentStateName == AirStateName &&
            _airMovementState.CurrentStateName == targetSubState)
        {
            return;
        }

        if (_stateMachine.CurrentStateName != AirStateName)
        {
            _stateMachine.TransitionTo(AirStateName);
        }

        _airMovementState.TransitionTo(targetSubState);
    }

    private void UpdateJumpVerticalMotion()
    {
        if (!jumpHoldTimer.IsRunning &&
            !Input.GetKey(jumpKey) &&
            vel.y > 0f &&
            !_shortHopApplied)
        {
            vel.y *= shortHopFactor;
            _shortHopApplied = true;
        }

        vel.y -= gravity * Time.deltaTime;
    }

    // ==== 冲刺逻辑 ==== //
    private void StartDash()
    {
        SetSpriteColor(Color.blue);
        _animator.Play(dashAnimName);

        if (_dashCoroutine != null)
        {
            StopCoroutine(_dashCoroutine);
        }

        _dashCoroutine = StartCoroutine(DashRoutine());
    }
    private IEnumerator DashRoutine()
    {
        var step = Vector2.right * _facingDirection * dashDistance;
        yield return this.MoveByStep(step, dashDuration, 0.3f);
        _dashCoroutine = null;
        var nextState = _movementInput == Vector2.zero ? StandStateName : WalkStateName;
        SwitchToGroundSubState(nextState);
    }

    private void StayDash()
    {
        // 冲刺期间由协程控制位移，不需要每帧额外逻辑。
    }

    private void DashExit()
    {
        if (_dashCoroutine != null)
        {
            StopCoroutine(_dashCoroutine);
            _dashCoroutine = null;
        }

        dashCDTimer.StartTimer(dashCooldown);
    }

    // ==== 受伤逻辑 ==== //
    private void StartHurt()
    {
        vel = Vector2.zero;
        GetComponent<Rigidbody2D>().velocity = Vector2.zero;
        SetSpriteColor(Color.red);

        if (_hurtCoroutine != null)
        {
            StopCoroutine(_hurtCoroutine);
        }

        var KbDir = (Vector2)(transform.position - incomingHitInfo.Source.transform.position).normalized;

        _hurtCoroutine = StartCoroutine(HurtRoutine(KbDir));
    }
    private IEnumerator HurtRoutine(Vector2 KbDir)
    {
        var step = KbDir * hurtKbDistance;
        yield return this.MoveByStep(step, hurtDuration, 0.8f);
        _hurtCoroutine = null;
        var nextState = _movementInput == Vector2.zero ? StandStateName : WalkStateName;
        SwitchToGroundSubState(nextState);
    }

    private void StayHurt()
    {
    }

    private void ExitHurt()
    {
        if (_hurtCoroutine != null)
        {
            StopCoroutine(_hurtCoroutine);
            _hurtCoroutine = null;
        }
    }

    // ==== 攻击逻辑 ==== //
    private void EnterAttackParentState()
    {
        if (_attackStateMachine == null)
        {
            return;
        }

        var targetSubState = _pendingFloatAttack ? AttackFloatSubStateName : AttackGroundSubStateName;
        _attackStateMachine.TransitionTo(targetSubState);
    }

    private void StartAttack()
    {
        AdjustAnimatorSpeedForClip(attackAnimParam, attackDuration);
        _animator.Play(attackAnimParam, 0, 0);
        vel = Vector2.zero;
        if (_attackCoroutine != null)
        {
            StopCoroutine(_attackCoroutine);
        }

        _attackCoroutine = StartCoroutine(AttackRoutine());
    }

    private void StayAttack()
    {
        // 无需每帧逻辑。
    }

    private IEnumerator AttackRoutine()
    {
        yield return new WaitForSeconds(attackDuration);
        _attackCoroutine = null;
        var nextState = _movementInput == Vector2.zero ? StandStateName : WalkStateName;
        SwitchToGroundSubState(nextState);
    }

    private void ExitAttack()
    {
        ResetAnimatorSpeed();
        if (_attackCoroutine != null)
        {
            StopCoroutine(_attackCoroutine);
            _attackCoroutine = null;
        }

        SetSpriteColor(Color.white);
    }

    // ==== 浮空攻击 ==== //
    private void StartFloatAttack()
    {
        AdjustAnimatorSpeedForClip(attackAnimParam, floatAttackDuration);
        _animator.Play(attackAnimParam, 0, 0);
        if (_floatAttackCoroutine != null)
        {
            StopCoroutine(_floatAttackCoroutine);
        }

        _floatAttackCoroutine = StartCoroutine(FloatAttackRoutine());
    }

    private void StayFloatAttack()
    {
        if (grounded)
        {
            vel.x = 0f;
            return;
        }

        ApplyHorizontalMovement();
        UpdateJumpVerticalMotion();
    }

    private IEnumerator FloatAttackRoutine()
    {
        yield return new WaitForSeconds(floatAttackDuration);
        _floatAttackCoroutine = null;
        if (grounded)
        {
            var nextState = _movementInput == Vector2.zero ? StandStateName : WalkStateName;
            SwitchToGroundSubState(nextState);
        }
        else
        {
            SwitchToAirSubState(FloatingSubStateName);
        }
    }

    private void ExitFloatAttack()
    {
        ResetAnimatorSpeed();
        if (_floatAttackCoroutine != null)
        {
            StopCoroutine(_floatAttackCoroutine);
            _floatAttackCoroutine = null;
        }

        SetSpriteColor(Color.white);
    }

    private void AdjustAnimatorSpeedForClip(string clipName, float desiredDuration)
    {
        if (_animator == null)
        {
            return;
        }

        var clip = GetAnimationClipByName(clipName);
        if (clip == null || desiredDuration <= 0f)
        {
            _animator.speed = _defaultAnimatorSpeed;
            return;
        }

        _animator.speed = clip.length / desiredDuration;
    }

    private AnimationClip GetAnimationClipByName(string clipName)
    {
        if (_animator == null || _animator.runtimeAnimatorController == null)
        {
            return null;
        }

        foreach (var clip in _animator.runtimeAnimatorController.animationClips)
        {
            if (clip.name == clipName)
            {
                return clip;
            }
        }

        return null;
    }

    private void ResetAnimatorSpeed()
    {
        if (_animator == null)
        {
            return;
        }

        _animator.speed = _defaultAnimatorSpeed;
    }

    // ==== 格挡流程 ==== //
    private void StartBlock()
    {
        if(blockActionChain.IsPlaying)
            blockActionChain.Stop();
        vel.x = 0f;
        blockActionChain.Play(this);
    }

    private void StayBlock()
    {
        if (!blockActionChain.IsPlaying)
        {
            var nextState = _movementInput == Vector2.zero ? StandStateName : WalkStateName;
            SwitchToGroundSubState(nextState);
        }
    }

    private void ExitBlock()
    {
        if(blockActionChain.IsPlaying)
            blockActionChain.Stop();
        BlockedHitInfo.Clear();
    }

    // ==== 格挡动作链构建 ==== //
    private void BuildBlockActionChain()
    {
        var startCursor = blockActionChain.Start;
        var endCursor = blockActionChain.End;

        // 格挡检测
        var BlockCheck = blockActionChain.CreateActionNode(BlockCheckAction);

        // 没受到攻击？
        var NoHitBranch = blockActionChain.CreateConditionalNode(
            _ => !BlockedHitInfo.IsValid);


        // 检测直到松开按键或受到攻击
        var WaitForReleaseOrHit = blockActionChain.CreateActionNode(WaitForReleaseOrHitAction);

        // 松开按键？
        var ReleasedBranch1 = blockActionChain.CreateConditionalNode(
            _ => !Input.GetKey(blockKey));


        // 是轻攻击？
        var LightHitBranch = blockActionChain.CreateConditionalNode(
            _ => BlockedHitInfo.Grade == AttackGrade.Light);


        // 免伤20%
        var LightHitAction = blockActionChain.CreateActionNode(LightHitActionHandler);

        // 触发受伤
        var GetHit = blockActionChain.CreateActionNode(GetHitAction);

        // 是重攻击？
        var HeavyHitBranch = blockActionChain.CreateConditionalNode(
            _ => BlockedHitInfo.Grade == AttackGrade.Heavy);

        // 格挡成功动作
        var SuccessfulBlock = blockActionChain.CreateActionNode(SuccessfulBlockAction);

        // 检测直到松开按键或僵持时间到
        var WaitForReleaseOrStun = blockActionChain.CreateActionNode(WaitForReleaseOrStunAction);

        // 松开按键？
        var ReleasedBranch2 = blockActionChain.CreateConditionalNode(
            _ => !Input.GetKey(blockKey));


        // 弹反失败硬直
        var ParryFailStun = blockActionChain.CreateActionNode(ParryFailStunAction);

        // 弹反检测
        var ParryCheck = blockActionChain.CreateActionNode(ParryCheckAction);

        // 松开按键？
        var ReleasedBranch3 = blockActionChain.CreateConditionalNode(
            _ => Input.GetKeyUp(blockKey));


        // 弹反成功动作
        var SuccessfulParry = blockActionChain.CreateActionNode(SuccessfulParryAction);

        // 构建连接
        startCursor.SetNext(BlockCheck);
        BlockCheck.SetNext(NoHitBranch);
        NoHitBranch.SetBranches(WaitForReleaseOrHit, HeavyHitBranch);
        WaitForReleaseOrHit.SetNext(ReleasedBranch1);
        ReleasedBranch1.SetBranches(endCursor, LightHitBranch);
        LightHitBranch.SetBranches(LightHitAction, GetHit);
        LightHitAction.SetNext(GetHit);
        GetHit.SetNext(endCursor);
        HeavyHitBranch.SetBranches(WaitForReleaseOrStun, SuccessfulBlock);
        SuccessfulBlock.SetNext(endCursor);
        WaitForReleaseOrStun.SetNext(ReleasedBranch2);
        ReleasedBranch2.SetBranches(ParryFailStun, ParryCheck);
        ParryFailStun.SetNext(endCursor);
        ParryCheck.SetNext(ReleasedBranch3);
        ReleasedBranch3.SetBranches(SuccessfulParry, ParryFailStun);
        SuccessfulParry.SetNext(endCursor);

        IEnumerator BlockCheckAction(MonoBehaviour _)
        {
            AdjustAnimatorSpeedForClip(blockAnimParam, blockDuration);
            _animator.Play(blockAnimParam);
            tryCatchInfo = true;
            float elapsed = 0f;
            while (elapsed < blockDuration)
            {
                if (BlockedHitInfo.IsValid)
                {
                    invincibleTimer.StartTimer(blockSucceededDuration);
                    tryCatchInfo = false;
                }
                elapsed += Time.deltaTime;
                yield return null;
            }
        }

        IEnumerator WaitForReleaseOrHitAction(MonoBehaviour _)
        {
            SetSpriteColor(new Color(0.5f, 0.25f, 0f, 1f));
            while (Input.GetKey(blockKey) && !BlockedHitInfo.IsValid)
            {
                yield return null;
            }
            tryCatchInfo = false;
        }

        IEnumerator LightHitActionHandler(MonoBehaviour _)
        {
            incomingHitInfo = BlockedHitInfo;
            incomingHitInfo.Damage *= 0.8f;
            BlockedHitInfo.Clear();
            yield break;
        }

        IEnumerator GetHitAction(MonoBehaviour _)
        {
            if (!incomingHitInfo.IsValid && BlockedHitInfo.IsValid)
            {
                incomingHitInfo = BlockedHitInfo;
                BlockedHitInfo.Clear();
            }
            ProcessIncomingHit();
            yield break;
        }

        IEnumerator SuccessfulBlockAction(MonoBehaviour _)
        {
            StartCoroutine(BlockedEffect(this));
            yield break;
        }

        IEnumerator BlockedEffect(MonoBehaviour _)
        {
            float elapsed = 0f;
            while (elapsed < flashDuration)
            {
                elapsed += Time.deltaTime;
                _material.SetFloat("_flashFactor", Mathf.Lerp(1f, 0f, elapsed / flashDuration));
                yield return null;
            }
        }

        IEnumerator WaitForReleaseOrStunAction(MonoBehaviour _)
        {
            SetSpriteColor(Color.yellow);
            invincibleTimer.StartTimer(BlockedHitInfo.StunDuration + BlockedHitInfo.ParryWindow + 0.1f);
            tryCatchInfo = false;
            float elapsed = 0f;
            while (elapsed < BlockedHitInfo.StunDuration)
            {
                if (!Input.GetKey(blockKey))
                {
                    yield break;
                }
                elapsed += Time.deltaTime;
                yield return null;
            }
        }

        IEnumerator ParryFailStunAction(MonoBehaviour _)
        {
            SetSpriteColor(new Color(0.5f, 0.25f, 0f, 1f));
            float elapsed = 0f;
            while (elapsed < parryFailDuration)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }
            invincibleTimer.StartTimer(0.1f);
        }

        IEnumerator ParryCheckAction(MonoBehaviour _)
        {
            SetSpriteColor(new Color(1f, 0.5f, 0f, 1f));
            float elapsed = 0f;
            while (elapsed < BlockedHitInfo.ParryWindow)
            {
                if (Input.GetKeyUp(blockKey))
                {
                    yield break;
                }
                elapsed += Time.deltaTime;
                yield return null;
            }
        }

        IEnumerator SuccessfulParryAction(MonoBehaviour _)
        {
            SetSpriteColor(Color.cyan);
            invincibleTimer.StartTimer(parrySucceededDuration + 0.4f);
            float elapsed = 0f;
            while (elapsed < parrySucceededDuration)
            {
                elapsed += Time.deltaTime;
                yield return null;
            }
        }

    }

    // ==== 碰撞与击中判断 ==== //
    private void TryRegisterGroundContact(Collision2D collision)
    {
        if (!collision.gameObject.CompareTag("Block"))
        {
            return;
        }

        foreach (var contact in collision.contacts)
        {
            if (contact.normal.y >= GroundNormalThreshold)
            {
                if (_groundContacts.Add(collision.collider))
                {
                    grounded = true;
                }

                return;
            }
        }
    }

    private void HandleIncomingAttack(GameObject other)
    {
        if (other.TryGetComponent<AttackHitInfo>(out var hitInfo))
        {
            if (hitInfo.used) return;
            var incoming = hitInfo.GetHitInfo();
            if (tryCatchInfo)
            {
                Debug.Log("格挡状态下收到攻击");
                BlockedHitInfo = incoming;
                hitInfo.used = true;
                incomingHitInfo.Clear();
            }
            else if (!invincibleTimer.IsRunning)
            {
                Debug.Log("常态下收到攻击");
                incomingHitInfo = incoming;
                hitInfo.used = true;
                BlockedHitInfo.Clear();
                ProcessIncomingHit();
            }
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        TryRegisterGroundContact(collision);
        HandleIncomingAttack(collision.gameObject);
    }

    private void OnCollisionStay2D(Collision2D collision)
    {
        TryRegisterGroundContact(collision);
        HandleIncomingAttack(collision.gameObject);
    }

    private void OnCollisionExit2D(Collision2D collision)
    {
        if (_groundContacts.Remove(collision.collider) && _groundContacts.Count == 0)
        {
            grounded = false;
            var state = ActiveState;
            if (!grounded && (state == StandStateName || state == WalkStateName))
            {
                SwitchToAirSubState(FloatingSubStateName);
            }
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        HandleIncomingAttack(other.gameObject);
    }

    private void OnTriggerStay2D(Collider2D other)
    {
        HandleIncomingAttack(other.gameObject);
    }

    // ==== 视觉与状态工具 ==== //
    // 用变色暂时代替动画效果。
    private void SetSpriteColor(Color color)
    {
        // 弃用
    }

    private void ApplySpriteColor()
    {
        if (_spriteRenderer == null)
        {
            return;
        }

        var alpha = _invincibleAlphaActive ? 0.7f : 1f;
        _spriteRenderer.color = new Color(_spriteBaseColor.r, _spriteBaseColor.g, _spriteBaseColor.b, alpha);
    }

    // 处理受伤逻辑。
    private void ProcessIncomingHit()
    {
        if (incomingHitInfo.IsValid && ActiveState != "Hurt")
        {
            string gradeStr = incomingHitInfo.Grade == AttackGrade.Light ? "轻击" : "重击";
            Debug.Log($"玩家受到{incomingHitInfo.Damage}点{gradeStr}伤害");
            PlayerHealth.Instance.TakeDamage((int)incomingHitInfo.Damage);
            _stateMachine.TransitionTo("Hurt");
            invincibleTimer.StartTimer(invincibleDuration);
        }
    }
}
