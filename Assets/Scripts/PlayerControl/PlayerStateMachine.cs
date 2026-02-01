using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static AttackHitInfo;

public class PlayerStateMachine : MonoBehaviour
{
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

    [Header("格挡")]
    [Tooltip("格挡预输入时长")]
    [SerializeField] private float blockInputBuffer = 0.15f;
    [Tooltip("格挡判定时长")]
    [SerializeField] private float blockDuration = 0.4f;
    [Tooltip("格挡成功动作时长")]
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



    private HierarchicalStateMachine _stateMachine;
    private Vector2 _movementInput;
    private float _facingDirection = 1f;
    private Coroutine _dashCoroutine;
    private Coroutine _attackCoroutine;
    private Coroutine _floatAttackCoroutine;
    private Coroutine _hurtCoroutine;
    private ActSeq blockActionChain = new();

    struct HitInfo
    {
        public bool IsValid => Source != null;
        public float Damage;
        public AttackGrade Grade;
        public float StunDuration;
        public float ParryWindow;
        public GameObject Source;

        public void Clear()
        {
            Damage = 0f;
            Grade = AttackGrade.Light;
            StunDuration = 0f;
            ParryWindow = 0f;
            Source = null;
        }
    }
    private bool tryCatchInfo = false;
    private HitInfo incomingHitInfo = new();
    private HitInfo BlockedHitInfo = new();

    private string ActiveState => _stateMachine?.CurrentStateName ?? string.Empty;

    private SpriteRenderer _spriteRenderer;
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

    private bool _shouldApplyJumpVelocity = true;
    private bool _skipJumpExitVelocityReset;
    private bool _preserveJumpHoldTimer;

    private void Awake()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();
        BuildStateMachine();
        BuildBlockActionChain();
    }

    private void Start()
    {
        grounded = false;
        _stateMachine.Enter();
    }

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

    private void UpdateInput()
    {
        float horizontal = 0f;
        if (Input.GetKey(moveLeftKey))
        {
            horizontal -= 1f;
        }

        if (Input.GetKey(moveRightKey))
        {
            horizontal += 1f;
        }

        var activeState = ActiveState;

        if (activeState != "FloatAttack")
        {
            if (horizontal < 0f)
            {
                _facingDirection = -1f;
            }
            else if (horizontal > 0f)
            {
                _facingDirection = 1f;
            }
        }

        _movementInput = horizontal != 0f ? new Vector2(horizontal, 0f) : Vector2.zero;

        // 站立 <=> 行走
        if (activeState == "Stand" || activeState == "Walk")
        {
            var goalState = horizontal != 0f ? "Walk" : "Stand";
            if (activeState != goalState)
            {
                _stateMachine.TransitionTo(goalState);
            }
        }

        // (站立 || 行走) => 跳跃
        if (InputPreInput.GetKeyDown(jumpKey, jumpInputBuffer) && (activeState == "Stand" || activeState == "Walk"))
        {
            _shouldApplyJumpVelocity = true;
            InputPreInput.ConsumeBufferedKeyDown(jumpKey);
            _stateMachine.TransitionTo("Jump");
        }

        // (站立 || 行走) => 冲刺
        if (InputPreInput.GetKeyDown(dashKey, dashInputBuffer) &&
            !dashCDTimer.IsRunning &&
            Mathf.Abs(_facingDirection) > Mathf.Epsilon &&
            (activeState == "Stand" || activeState == "Walk"))
        {
            _stateMachine.TransitionTo("Dash");
            InputPreInput.ConsumeBufferedKeyDown(dashKey);
        }

        // (站立 || 行走) => 格挡
        if (InputPreInput.GetKeyDown(blockKey, blockInputBuffer) && (activeState == "Stand" || activeState == "Walk"))
        {
            _stateMachine.TransitionTo("Block");
            InputPreInput.ConsumeBufferedKeyDown(blockKey);
        }

        // (站立 || 行走) => 普攻
        if (InputPreInput.GetKeyDown(attackKey, 0.1f) && (activeState == "Stand" || activeState == "Walk"))
        {
            _stateMachine.TransitionTo("Attack");
            InputPreInput.ConsumeBufferedKeyDown(attackKey);
        }

        // 跳跃 => 浮空普攻
        if (InputPreInput.GetKeyDown(attackKey, 0.1f) && activeState == "Jump")
        {
            _skipJumpExitVelocityReset = true;
            _preserveJumpHoldTimer = true;
            InputPreInput.ConsumeBufferedKeyDown(attackKey);
            _stateMachine.TransitionTo("FloatAttack");
        }
    }

    private void BuildStateMachine()
    {
        _stateMachine = new HierarchicalStateMachine("Player");

        var standState = new SimpleState(
            "Stand",
            enter: () =>
            {
                SetSpriteColor(Color.white);
            },
            stay: StayStand);

        var walkState = new SimpleState(
            "Walk",
            enter: () =>
            {
                SetSpriteColor(Color.gray);
            },
            stay: StayWalk);

        var jumpState = new SimpleState(
            "Jump",
            enter: StartJump,
            stay: StayJump,
            exit: ExitJump);

        var dashState = new SimpleState(
            "Dash",
            enter: StartDash,
            stay: StayDash,
            exit: DashExit);

        var attackState = new SimpleState(
            "Attack",
            enter: StartAttack,
            stay: StayAttack,
            exit: ExitAttack);

        var floatAttackState = new SimpleState(
            "FloatAttack",
            enter: StartFloatAttack,
            stay: StayFloatAttack,
            exit: ExitFloatAttack);

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
            .RegisterState(standState, true)
            .RegisterState(walkState)
            .RegisterState(jumpState)
            .RegisterState(dashState)
            .RegisterState(attackState)
            .RegisterState(floatAttackState)
            .RegisterState(blockState)
            .RegisterState(hurtState);
    }

    private void StayStand()
    {
        ApplyHorizontalMovement();
        if (!grounded)
        {
            _shouldApplyJumpVelocity = false;
            _stateMachine.TransitionTo("Jump");
        }
    }

    private void StayWalk()
    {
        ApplyHorizontalMovement();
        if (!grounded)
        {
            _shouldApplyJumpVelocity = false;
            _stateMachine.TransitionTo("Jump");
        }
    }

    private void ApplyHorizontalMovement()
    {
        vel.x = _movementInput.x * walkSpeed;
    }

    private void StartJump()
    {
        SetSpriteColor(Color.magenta);
        if (_shouldApplyJumpVelocity)
        {
            vel.y = jumpVelocity;
            jumpHoldTimer.StartTimer(jumpHoldTime);
            _shortHopApplied = false;
        }
        else
        {
            jumpHoldTimer.StopTimer();
            _shortHopApplied = false;
        }

        grounded = false;
        _shouldApplyJumpVelocity = true;
    }

    private void StayJump()
    {
        ApplyHorizontalMovement();
        UpdateJumpVerticalMotion();

        if (grounded && vel.y <= 0f)
        {
            var nextState = _movementInput == Vector2.zero ? "Stand" : "Walk";
            _stateMachine.TransitionTo(nextState);
        }
    }

    private void ExitJump()
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

    private void StartDash()
    {
        SetSpriteColor(Color.blue);

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
        _stateMachine.TransitionTo("Stand");
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
        _stateMachine.TransitionTo("Stand");
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

    private void StartAttack()
    {
        SetSpriteColor(Color.green);
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
        yield return this.Wait(0.4f);
        _attackCoroutine = null;
        _stateMachine.TransitionTo("Stand");
    }

    private void ExitAttack()
    {
        if (_attackCoroutine != null)
        {
            StopCoroutine(_attackCoroutine);
            _attackCoroutine = null;
        }

        SetSpriteColor(Color.white);
    }

    private void StartFloatAttack()
    {
        SetSpriteColor(Color.green);
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
        yield return this.Wait(0.4f);
        _floatAttackCoroutine = null; 
        if (grounded)
        {
            _stateMachine.TransitionTo("Stand");
        }
        else {
            _shouldApplyJumpVelocity = false;
            _stateMachine.TransitionTo("Jump");
        }
    }

    private void ExitFloatAttack()
    {
        if (_floatAttackCoroutine != null)
        {
            StopCoroutine(_floatAttackCoroutine);
            _floatAttackCoroutine = null;
        }

        SetSpriteColor(Color.white);
    }

    private void StartBlock()
    {
        if(blockActionChain.IsPlaying)
            blockActionChain.Stop();
        vel.x = 0f;
        blockActionChain.Play(this);
    }

    private void StayBlock()
    {
        if(!blockActionChain.IsPlaying)
            _stateMachine.TransitionTo("Stand");
    }

    private void ExitBlock()
    {
        if(blockActionChain.IsPlaying)
            blockActionChain.Stop();
        SetSpriteColor(Color.white);
        BlockedHitInfo.Clear();
    }

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
            SetSpriteColor(new Color(1f, 0.5f, 0f, 1f));
            tryCatchInfo = true;
            float elapsed = 0f;
            while (elapsed < blockDuration)
            {
                if (BlockedHitInfo.IsValid)
                {
                    yield break;
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
            SetSpriteColor(Color.cyan);
            invincibleTimer.StartTimer(blockSucceededDuration + 0.1f);
            tryCatchInfo = false;
            float elapsed = 0f;
            while (elapsed < blockSucceededDuration)
            {
                elapsed += Time.deltaTime;
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
            var incoming = new HitInfo
            {
                Damage = hitInfo.Damage,
                Grade = hitInfo.Grade,
                StunDuration = hitInfo.StunDuration,
                ParryWindow = hitInfo.ParryWindow,
                Source = hitInfo.Source
            };
            if (tryCatchInfo)
            {
                Debug.Log("格挡状态下收到攻击");
                BlockedHitInfo = incoming;
                incomingHitInfo.Clear();
            }
            else if (!invincibleTimer.IsRunning)
            {
                Debug.Log("常态下收到攻击");
                incomingHitInfo = incoming;
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
            TryEnterJumpFromFall();
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

    private void TryEnterJumpFromFall()
    {
        var state = ActiveState;
        if (!grounded && (state == "Stand" || state == "Walk"))
        {
            _shouldApplyJumpVelocity = false;
            _stateMachine.TransitionTo("Jump");
        }
    }

    // 用变色暂时代替动画效果。
    private void SetSpriteColor(Color color)
    {
        _spriteBaseColor = color;
        ApplySpriteColor();
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
