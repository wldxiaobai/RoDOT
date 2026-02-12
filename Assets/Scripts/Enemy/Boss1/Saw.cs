using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Saw : MonoBehaviour
{
    [Header("电锯全局设置")]
    [Tooltip("地板所在高度，电锯落地后会停在这个高度")]
    [SerializeField] private float groundY = 0f;
    [Tooltip("门所在位置，电锯在某些动作结束后会移动到这个位置")]
    [SerializeField] private float doorX = 10f;
    [Tooltip("电锯AttackHitInfo引用")]
    [SerializeField] AttackHitInfo sawAttack;
    [Tooltip("电锯旋转速度（度/秒）")]
    [SerializeField] private float sawRotateSpeed = 720f;

    [Header("持续冒火设置（动作2/3/4期间）")]
    [Tooltip("持续冒火的时间间隔（秒）")]
    [SerializeField] private float continuousFireInterval = 0.05f;
    [Tooltip("持续冒火的火星速度")]
    [SerializeField] private float continuousFireSpeed = 5f;

    [Header("锋利度交互设置")]
    [Tooltip("被玩家攻击时，玩家回复锋利度")]
    [SerializeField] private float sharpnessRestoreOnHit = 5f;

    [Header("视效设置")]
    [Tooltip("闪烁特效时长")]
    [SerializeField] private float flashDuration = 0.2f;
    [Tooltip("闪烁颜色覆盖程度")]
    [Range(0f, 1f)]
    [SerializeField] private float colorCoverRate = 1f;
    [Tooltip("受击闪烁颜色")]
    [SerializeField] private Color hurtFlashColor = new(1f, 0.5f, 0.5f, 1f);

    [Header("音效设置")]
    [Tooltip("音量")]
    [Range(0f, 1f)] [SerializeField] private float volume = 0.4f;
    [Tooltip("被玩家攻击时的音效")] 
    [SerializeField] private AudioClip hurtByPlayerClip; 
    [Tooltip("被玩家格挡时的音效")] 
    [SerializeField] private AudioClip blockedByPlayerClip;
    [Tooltip("磨砺音效")]
    [SerializeField] private AudioClip grindClip;

    private float continuousFireTimer;

    private SpriteRenderer spriteRenderer;

    private GameObject player;

    private Material _material;

    private Coroutine _flashCoroutine;

    private Collider2D sawCollider;

    // ---------------韧性条---------------

    [Header("韧性条设置")]
    [Tooltip("最大韧性值")]
    [SerializeField] private float maxToughness = 100f;
    [Tooltip("被玩家攻击时韧性减少量")]
    [SerializeField] private float toughnessReduceOnHit = 20f;
    [Tooltip("攻击被玩家格挡时韧性减少量")]
    [SerializeField] private float toughnessReduceOnBlocked = 30f;
    [Tooltip("破韧后僵直持续时间（秒）")]
    [SerializeField] private float staggerHoldDuration = 3f;

    private AmountBar toughness;
    private bool isToughnessBreak;

    // ---------------韧性条结束---------------


    private bool IsNonStaggerActionPlaying =>
        leapSawSeq.IsPlaying ||
        grindSawSeq.IsPlaying ||
        forgeSawSeq.IsPlaying ||
        heavyForgeSawSeq.IsPlaying ||
        embedSawSeq.IsPlaying;

    /// <summary>
    /// 动作2/3/4是否正在播放（需要持续冒火的动作）。
    /// </summary>
    private bool IsFireEmittingActionPlaying =>
        grindSawSeq.IsPlaying ||
        forgeSawSeq.IsPlaying ||
        heavyForgeSawSeq.IsPlaying;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        sawCollider = GetComponent<Collider2D>();
        _material = spriteRenderer.material;
        toughness = new AmountBar(maxToughness);
        EnsureFirePoolInitialized();
        InitLeapSawActSeq();
        InitGrindSawActSeq();
        InitForgeSawActSeq();
        InitHeavyForgeSawActSeq();
        InitEmbedSawActSeq();
        InitStaggerSawActSeq();

        if (sawAttack != null)
        {
            sawAttack.OnBlocked += OnSawAttackBlocked;
        }

        // 初始状态下禁用攻击碰撞箱
        SetSawAttackActive(false);
    }

    private void OnDestroy()
    {
        if (sawAttack != null)
        {
            sawAttack.OnBlocked -= OnSawAttackBlocked;
        }

        if (fireSpawnRoutine != null)
        {
            StopCoroutine(fireSpawnRoutine);
            fireSpawnRoutine = null;
        }
        firePool.Clear();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            PlayLeapSaw();
        }
        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            PlayGrindSaw();
        }
        if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            PlayForgeSaw();
        }
        if (Input.GetKeyDown(KeyCode.Alpha4))
        {
            PlayHeavyForgeSaw();
        }
        if (Input.GetKeyDown(KeyCode.Alpha5))
        {
            PlayEmbedSaw();
        }
        if (Input.GetKeyDown(KeyCode.Alpha6))
        {
            PlayStaggerSaw();
        }

        // 非僵直动作运行时，电锯持续顺时针旋转
        if (IsNonStaggerActionPlaying)
        {
            transform.Rotate(0f, 0f, -sawRotateSpeed * Time.deltaTime);
        }

        // 动作2/3/4运行期间，电锯处于出现状态时持续冒火星
        if (IsFireEmittingActionPlaying && spriteRenderer != null && spriteRenderer.enabled)
        {
            continuousFireTimer -= Time.deltaTime;
            if (continuousFireTimer <= 0f)
            {
                Vector2 direction = Random.insideUnitCircle;
                if (direction.sqrMagnitude < 0.01f)
                {
                    direction = Vector2.right;
                }
                else
                {
                    direction.Normalize();
                }

                // 红温阶段火星速度翻倍
                float speed = isHeavyForgeFlushActive ? continuousFireSpeed * 2f : continuousFireSpeed;

                EnsureFirePoolInitialized();
                EmitFire(transform.position, speed, direction);
                continuousFireTimer = continuousFireInterval;
            }
        }
        else
        {
            // 条件不满足时重置计时器，下次进入时立刻发射第一颗火星
            continuousFireTimer = 0f;
        }
    }



    // ---------------动作1：跃锯---------------

    [Header("动作1：跃锯 设置")]
    [Tooltip("火星生成位置相对于玩家x坐标的随机偏移范围（±）")]
    [SerializeField] private float leapFireSpawnRangeX = 2f;
    [Tooltip("前摇生成火星的时间")]
    [SerializeField] private float fireSpawnPreDelay = 0.5f;
    [Tooltip("电锯跃起速度")]
    [SerializeField] private float sawJumpSpeed = 5f;
    [Tooltip("电锯跃起时受到的重力大小")]
    [SerializeField] private float sawGravityScale = 1f;
    [Tooltip("跃锯火星数量")]
    [SerializeField] private int leapFireAmount = 64;
    [Tooltip("跃锯火星飞散速度")]
    [SerializeField] private float leapFireSpeed = 10f;

    private readonly ActSeq leapSawSeq = new();

    /// <summary>本次跃锯动态计算的火星生成位置。</summary>
    private Vector2 leapFirePos;
    /// <summary>本次跃锯动态计算的电锯出现位置（火星正下方 embedDistance 处）。</summary>
    private Vector2 leapSawAppearPos;

    private void InitLeapSawActSeq()
    {
        var startCursor = leapSawSeq.Start;
        var endCursor = leapSawSeq.End;

        // 第一步：根据玩家位置计算火星生成点，生成火星并等待
        var spawnFireNode = leapSawSeq.CreateActionNode(() => LeapSaw_SpawnFires());
        startCursor.SetNext(spawnFireNode);

        // 第二步：电锯从火星正下方出现，做竖向抛物运动
        var leapNode = leapSawSeq.CreateActionNode(() => LeapSaw_JumpAndFall());
        spawnFireNode.SetNext(leapNode);

        leapNode.SetNext(endCursor);
    }

    /// <summary>
    /// 播放"跃锯"动作序列。
    /// </summary>
    public void PlayLeapSaw()
    {
        if (leapSawSeq.IsPlaying)
        {
            return;
        }

        leapSawSeq.Play(this);
    }

    private IEnumerator LeapSaw_SpawnFires()
    {
        // 动作开始时确保电锯不出现且攻击碰撞箱禁用
        SetSawAppearance(false);
        SetSawAttackActive(false);

        if (sawAttack != null)
        {
            sawAttack.Grade = AttackGrade.Light;
        }

        // 根据玩家位置动态计算火星生成点
        float playerX = 0f;
        if (GetPlayer() && player != null)
        {
            playerX = player.transform.position.x;
        }

        float spawnX = playerX + Random.Range(-leapFireSpawnRangeX, leapFireSpawnRangeX);
        leapFirePos = new Vector2(spawnX, groundY);

        // 电锯出现位置：火星正下方 embedDistance 处
        leapSawAppearPos = new Vector2(spawnX, groundY - embedDistance);

        // 将电锯移动到火星生成位置（但不显示电锯本体）
        transform.position = new Vector3(leapFirePos.x, leapFirePos.y, transform.position.z);

        // 在该位置生成火星
        SpawnFires(leapFireAmount, fireSpawnPreDelay, leapFireSpeed);

        // 等待火星生成完毕
        yield return new WaitForSeconds(fireSpawnPreDelay);
    }

    private IEnumerator LeapSaw_JumpAndFall()
    {
        // 瞬移到动态计算的出现位置并显示电锯
        transform.position = new Vector3(leapSawAppearPos.x, leapSawAppearPos.y, transform.position.z);
        SetSawAppearance(true);

        // 电锯出现，启用攻击碰撞箱
        SetSawAttackActive(true);

        // 竖向抛物运动：初始向上速度为 sawJumpSpeed，受 sawGravityScale 重力影响
        float velocityY = sawJumpSpeed;
        float gravity = sawGravityScale * Physics2D.gravity.y; // 通常 gravity.y 为负值
        bool hasRisen = false;

        while (true)
        {
            velocityY += gravity * Time.deltaTime;
            float newY = transform.position.y + velocityY * Time.deltaTime;

            // 检测是否已经上升过（避免初始帧就判定为下落）
            if (transform.position.y > leapSawAppearPos.y)
            {
                hasRisen = true;
            }

            // 已经上升过且 Y 坐标降回到出现点以下时停止
            if (hasRisen && newY <= leapSawAppearPos.y)
            {
                transform.position = new Vector3(transform.position.x, leapSawAppearPos.y, transform.position.z);
                break;
            }

            transform.position = new Vector3(transform.position.x, newY, transform.position.z);
            yield return null;
        }

        // 禁用攻击碰撞箱
        SetSawAttackActive(false);

        // 停止运动并隐藏电锯
        SetSawAppearance(false);
    }

    // ---------------动作1：跃锯结束---------------



    // ---------------动作2：磨砺---------------

    [Header("动作2：磨砺 设置")]
    [Tooltip("磨砺时火星生成持续时间")]
    [SerializeField] private float grindFireSpawnDuration = 1f;
    [Tooltip("磨砺后电锯出现，震屏的时长")]
    [SerializeField] private float grindScreenShakeDuration = 0.16f;
    [Tooltip("磨砺后电锯出现，震屏的强度")]
    [SerializeField] private float grindScreenShakeMagnitude = 0.5f;
    [Tooltip("向左突进的速度")]
    [SerializeField] private float grindDashSpeed = 5f;
    [Tooltip("向左突进的距离")]
    [SerializeField] private float grindDashDistance = 5f;
    [Tooltip("向左突进后的减速度")]
    [SerializeField] private float grindDashDeceleration = 10f;
    [Tooltip("磨砺火星数量")]
    [SerializeField] private int grindFireAmount = 64;
    [Tooltip("磨砺火星飞散速度")]
    [SerializeField] private float grindFireSpeed = 10f;

    private readonly ActSeq grindSawSeq = new();

    private void InitGrindSawActSeq()
    {
        var startCursor = grindSawSeq.Start;
        var endCursor = grindSawSeq.End;

        // 第一步：在门与地板交汇处生成火星
        var spawnFireNode = grindSawSeq.CreateActionNode(() => GrindSaw_SpawnFires());
        startCursor.SetNext(spawnFireNode);

        // 第二步：电锯从火星正右方出现后向左突进并减速停止
        var dashNode = grindSawSeq.CreateActionNode(() => GrindSaw_DashAndDecelerate());
        spawnFireNode.SetNext(dashNode);

        dashNode.SetNext(endCursor);
    }

    /// <summary>
    /// 播放"磨砺"动作序列。
    /// </summary>
    public void PlayGrindSaw()
    {
        if (grindSawSeq.IsPlaying)
        {
            return;
        }

        grindSawSeq.Play(this);
    }

    private IEnumerator GrindSaw_SpawnFires()
    {
        // 动作开始时确保电锯不出现且攻击碰撞箱禁用
        SetSawAppearance(false);
        SetSawAttackActive(false);

        if (sawAttack != null)
        {
            sawAttack.Grade = AttackGrade.Light;
        }

        CameraShakeManager.Instance.ShakeStraight(Vector2.left, grindScreenShakeDuration, grindScreenShakeMagnitude);
        AudioManager.PlaySound(grindClip, transform.position, volume);

        // 火星生成位置：门与地板交汇处 (doorX, groundY)
        float fireX = doorX;
        float fireY = groundY;

        // 将电锯移动到火星生成位置（但不显示电锯本体）
        transform.position = new Vector3(fireX, fireY, transform.position.z);

        // 生成火星
        SpawnFires(grindFireAmount, grindFireSpawnDuration, grindFireSpeed);

        // 等待火星生成完毕
        yield return new WaitForSeconds(grindFireSpawnDuration);
    }

    private IEnumerator GrindSaw_DashAndDecelerate()
    {
        // 电锯出现位置：火星正右方 embedDistance 处 (doorX + embedDistance, groundY)
        float appearX = doorX + embedDistance;
        float appearY = groundY;

        // 瞬移到出现位置并显示电锯
        transform.position = new Vector3(appearX, appearY, transform.position.z);
        SetSawAppearance(true);

        // 电锯出现，启用攻击碰撞箱
        SetSawAttackActive(true);

        float totalDistance = Mathf.Max(0f, grindDashDistance);
        float speed = Mathf.Max(0f, grindDashSpeed);
        float decel = Mathf.Max(0.001f, grindDashDeceleration);

        // 减速阶段所需距离：d_decel = v² / (2a)
        float decelDist = (speed * speed) / (2f * decel);

        // 匀速阶段距离
        float constDist = totalDistance - decelDist;

        // 若减速距离已超过总距离，则全程减速，重新计算等效减速度
        if (constDist < 0f)
        {
            constDist = 0f;
            decelDist = totalDistance;
            decel = (speed * speed) / (2f * Mathf.Max(0.001f, totalDistance));
        }

        float traveled = 0f;

        // 阶段1：匀速向左移动
        while (traveled < constDist)
        {
            float step = speed * Time.deltaTime;
            if (traveled + step > constDist)
            {
                step = constDist - traveled;
            }

            traveled += step;
            transform.position += Vector3.left * step;
            yield return null;
        }

        // 阶段2：减速直至速度为 0
        float currentSpeed = speed;
        while (currentSpeed > 0f && traveled < totalDistance)
        {
            currentSpeed -= decel * Time.deltaTime;
            if (currentSpeed < 0f)
            {
                currentSpeed = 0f;
            }

            float step = currentSpeed * Time.deltaTime;
            if (traveled + step > totalDistance)
            {
                step = totalDistance - traveled;
            }

            traveled += step;
            transform.position += Vector3.left * step;
            yield return null;
        }

        // 磨砺结束，禁用攻击碰撞箱（电锯不消失，保持 spriteRenderer.enabled = true）
        SetSawAttackActive(false);
    }

    // ---------------动作2：磨砺结束---------------



    // ---------------动作3：锻击---------------

    [Header("动作3：锻击 设置")]
    [Tooltip("锻击归位时，电锯位于玩家左方的距离")]
    [SerializeField] private float forgeResetDistanceFromPlayer = 5f;
    [Tooltip("锻击第一步 归位 的速度")]
    [SerializeField] private float forgeResetSpeed;
    [Tooltip("锻击第二步 启动 的加速度")]
    [SerializeField] private float forgeStartAcce;
    [Tooltip("锻击第三步 突进 的速度")]
    [SerializeField] private float forgeMaxSpeed;
    [Tooltip("锻击被玩家格挡成功时反向初速度")]
    [SerializeField] private float forgeOpposeSpeed;
    [Tooltip("锻击被玩家格挡成功后的减速度")]
    [SerializeField] private float forgeOpposeAcce;

    private readonly ActSeq forgeSawSeq = new();
    private bool forgeWasBlocked;

    private void InitForgeSawActSeq()
    {
        var startCursor = forgeSawSeq.Start;
        var endCursor = forgeSawSeq.End;

        // 第一步：归位，移动到玩家左方
        var resetNode = forgeSawSeq.CreateActionNode(() => ForgeSaw_Reset());
        startCursor.SetNext(resetNode);

        // 第二步：启动，向左初速度并向右加速至 forgeMaxSpeed（期间检测格挡）
        var startupNode = forgeSawSeq.CreateActionNode(() => ForgeSaw_Startup());
        resetNode.SetNext(startupNode);

        // 启动后分支：启动阶段是否被格挡
        var startupBranch = forgeSawSeq.CreateConditionalNode(() => forgeWasBlocked);
        startupNode.SetNext(startupBranch);

        // 被格挡 → 直接反向击退
        var knockbackNode = forgeSawSeq.CreateActionNode(() => ForgeSaw_Knockback());

        // 未被格挡 → 进入突进阶段
        var dashNode = forgeSawSeq.CreateActionNode(() => ForgeSaw_Dash());
        startupBranch.SetBranches(knockbackNode, dashNode);

        // 突进后分支：突进阶段是否被格挡
        var dashBranch = forgeSawSeq.CreateConditionalNode(() => forgeWasBlocked);
        dashNode.SetNext(dashBranch);

        dashBranch.SetBranches(knockbackNode, endCursor);
        knockbackNode.SetNext(endCursor);
    }

    /// <summary>
    /// 播放"锻击"动作序列。
    /// </summary>
    public void PlayForgeSaw()
    {
        if (forgeSawSeq.IsPlaying)
        {
            return;
        }

        forgeSawSeq.Play(this);
    }

    private IEnumerator ForgeSaw_Reset()
    {
        // 显示电锯
        SetSawAppearance(true);

        if (sawAttack != null)
        {
            sawAttack.Grade = AttackGrade.Light;
        }

        // 归位位置：玩家左方 forgeResetDistanceFromPlayer 处，y 为 groundY
        float playerX = transform.position.x;
        if (GetPlayer() && player != null)
        {
            playerX = player.transform.position.x;
        }

        Vector3 target = new Vector3(
            playerX - forgeResetDistanceFromPlayer,
            groundY,
            transform.position.z);

        while (true)
        {
            Vector3 diff = target - transform.position;
            float remaining = diff.magnitude;

            if (remaining <= 0.01f)
            {
                transform.position = target;
                break;
            }

            float step = forgeResetSpeed * Time.deltaTime;
            if (step >= remaining)
            {
                transform.position = target;
                break;
            }

            transform.position += diff.normalized * step;
            yield return null;
        }
    }

    private IEnumerator ForgeSaw_Startup()
    {
        forgeWasBlocked = false;

        // 清除先前的命中记录
        if (sawAttack != null)
        {
            sawAttack.ClearHitObjects();
        }

        // 注册格挡回调
        bool blocked = false;
        System.Action<GameObject> onBlocked = _ => blocked = true;

        if (sawAttack != null)
        {
            sawAttack.OnBlocked += onBlocked;
        }

        // 启动阶段，启用攻击碰撞箱
        SetSawAttackActive(true);

        try
        {
            // 初速度：forgeResetSpeed 大小，方向向左
            // 以 forgeStartAcce 不断向右加速，直到速度达到 forgeMaxSpeed（向右）
            float velocityX = -forgeResetSpeed;

            while (velocityX < forgeMaxSpeed)
            {
                if (blocked)
                {
                    forgeWasBlocked = true;
                    break;
                }

                velocityX += forgeStartAcce * Time.deltaTime;
                if (velocityX > forgeMaxSpeed)
                {
                    velocityX = forgeMaxSpeed;
                }

                transform.position += new Vector3(velocityX * Time.deltaTime, 0f, 0f);
                yield return null;
            }
        }
        finally
        {
            // 启动阶段结束，禁用攻击碰撞箱
            SetSawAttackActive(false);

            if (sawAttack != null)
            {
                sawAttack.OnBlocked -= onBlocked;
            }
        }
    }

    private IEnumerator ForgeSaw_Dash()
    {
        forgeWasBlocked = false;

        // 清除先前的命中记录
        if (sawAttack != null)
        {
            sawAttack.ClearHitObjects();
        }

        // 注册格挡回调
        bool blocked = false;
        System.Action<GameObject> onBlocked = _ =>
        {
            blocked = true;
        };

        if (sawAttack != null)
        {
            sawAttack.OnBlocked += onBlocked;
        }

        // 突进终点：门内侧 (doorX + embedDistance)
        float endX = doorX + embedDistance;

        // 突进阶段，启用攻击碰撞箱
        SetSawAttackActive(true);

        try
        {
            // 以 forgeMaxSpeed 向右移动，直到 x 坐标到达终点或被格挡
            while (transform.position.x < endX)
            {
                if (blocked)
                {
                    forgeWasBlocked = true;
                    break;
                }

                float step = forgeMaxSpeed * Time.deltaTime;
                float newX = transform.position.x + step;
                if (newX > endX)
                {
                    newX = endX;
                }

                transform.position = new Vector3(newX, transform.position.y, transform.position.z);
                yield return null;
            }
        }
        finally
        {
            // 突进阶段结束，禁用攻击碰撞箱
            SetSawAttackActive(false);

            if (sawAttack != null)
            {
                sawAttack.OnBlocked -= onBlocked;
            }
        }
    }

    private IEnumerator ForgeSaw_Knockback()
    {
        // 被格挡后反向（向左）击退，初速度为 forgeOpposeSpeed，减速度为 forgeOpposeAcce
        float speed = forgeOpposeSpeed;

        while (speed > 0f)
        {
            transform.position += Vector3.left * (speed * Time.deltaTime);

            speed -= forgeOpposeAcce * Time.deltaTime;
            if (speed < 0f)
            {
                speed = 0f;
            }

            yield return null;
        }
    }

    // ---------------动作3：锻击结束---------------



    // ---------------动作4：沉重锻击---------------

    [Header("动作4：沉重锻击 设置")]
    [Tooltip("沉重锻击归位时，电锯位于玩家左方的距离")]
    [SerializeField] private float heavyForgeResetDistanceFromPlayer = 5f;
    [Tooltip("沉重锻击第一步 归位 的速度")]
    [SerializeField] private float heavyForgeResetSpeed;
    [Tooltip("沉重锻击第二步 红温 的时间")]
    [SerializeField] private float heavyForgeFlushDuration;
    [Tooltip("沉重锻击第三步 启动 的加速度")]
    [SerializeField] private float heavyForgeStartAcce;
    [Tooltip("沉重锻击第四步 突进 的速度")]
    [SerializeField] private float heavyForgeMaxSpeed;
    [Tooltip("沉重锻击被玩家格挡成功时反向初速度")]
    [SerializeField] private float heavyForgeOpposeSpeed;
    [Tooltip("沉重锻击被玩家格挡成功后的减速度")]
    [SerializeField] private float heavyForgeOpposeAcce;

    private readonly ActSeq heavyForgeSawSeq = new();
    private bool heavyForgeWasBlocked;
    private bool isHeavyForgeFlushActive;
    private Color heavyForgeOriginalColor;

    private void InitHeavyForgeSawActSeq()
    {
        var startCursor = heavyForgeSawSeq.Start;
        var endCursor = heavyForgeSawSeq.End;

        // 第一步：归位
        var resetNode = heavyForgeSawSeq.CreateActionNode(() => HeavyForgeSaw_Reset());
        startCursor.SetNext(resetNode);

        // 第二步：红温，原地变红
        var flushNode = heavyForgeSawSeq.CreateActionNode(() => HeavyForgeSaw_Flush());
        resetNode.SetNext(flushNode);

        // 第三步：启动（检测格挡）
        var startupNode = heavyForgeSawSeq.CreateActionNode(() => HeavyForgeSaw_Startup());
        flushNode.SetNext(startupNode);

        // 启动后分支
        var startupBranch = heavyForgeSawSeq.CreateConditionalNode(() => heavyForgeWasBlocked);
        startupNode.SetNext(startupBranch);

        // 共享节点：僵持 → 击退 → 恢复颜色
        var stunNode = heavyForgeSawSeq.CreateActionNode(() => HeavyForgeSaw_Stun());
        var knockbackNode = heavyForgeSawSeq.CreateActionNode(() => HeavyForgeSaw_Knockback());
        var cleanupNode = heavyForgeSawSeq.CreateActionNode(() => HeavyForgeSaw_Cleanup());

        // 第四步：突进（检测格挡）
        var dashNode = heavyForgeSawSeq.CreateActionNode(() => HeavyForgeSaw_Dash());
        startupBranch.SetBranches(stunNode, dashNode);

        // 突进后分支
        var dashBranch = heavyForgeSawSeq.CreateConditionalNode(() => heavyForgeWasBlocked);
        dashNode.SetNext(dashBranch);

        dashBranch.SetBranches(stunNode, cleanupNode);

        stunNode.SetNext(knockbackNode);
        knockbackNode.SetNext(cleanupNode);
        cleanupNode.SetNext(endCursor);
    }

    /// <summary>
    /// 播放"沉重锻击"动作序列。
    /// </summary>
    public void PlayHeavyForgeSaw()
    {
        if (heavyForgeSawSeq.IsPlaying)
        {
            return;
        }

        heavyForgeSawSeq.Play(this);
    }

    private IEnumerator HeavyForgeSaw_Reset()
    {
        SetSawAppearance(true);

        if (sawAttack != null)
        {
            sawAttack.Grade = AttackGrade.Heavy;
        }

        // 归位位置：玩家左方 heavyForgeResetDistanceFromPlayer 处，y 为 groundY
        float playerX = transform.position.x;
        if (GetPlayer() && player != null)
        {
            playerX = player.transform.position.x;
        }

        Vector3 target = new Vector3(
            playerX - heavyForgeResetDistanceFromPlayer,
            groundY,
            transform.position.z);

        while (true)
        {
            Vector3 diff = target - transform.position;
            float remaining = diff.magnitude;

            if (remaining <= 0.01f)
            {
                transform.position = target;
                break;
            }

            float step = heavyForgeResetSpeed * Time.deltaTime;
            if (step >= remaining)
            {
                transform.position = target;
                break;
            }

            transform.position += diff.normalized * step;
            yield return null;
        }
    }

    private IEnumerator HeavyForgeSaw_Flush()
    {
        isHeavyForgeFlushActive = true;

        // 记录原始颜色，红温结束后用于恢复
        heavyForgeOriginalColor = spriteRenderer != null ? spriteRenderer.color : Color.white;

        float elapsed = 0f;
        while (elapsed < heavyForgeFlushDuration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / heavyForgeFlushDuration);
            if (spriteRenderer != null)
            {
                spriteRenderer.color = Color.Lerp(heavyForgeOriginalColor, Color.red, t);
            }
            yield return null;
        }

        if (spriteRenderer != null)
        {
            spriteRenderer.color = Color.red;
        }

        isHeavyForgeFlushActive = false;
    }

    private IEnumerator HeavyForgeSaw_Startup()
    {
        heavyForgeWasBlocked = false;

        if (sawAttack != null)
        {
            sawAttack.ClearHitObjects();
        }

        bool blocked = false;
        System.Action<GameObject> onBlocked = _ => blocked = true;

        if (sawAttack != null)
        {
            sawAttack.OnBlocked += onBlocked;
        }

        // 启动阶段，启用攻击碰撞箱
        SetSawAttackActive(true);

        try
        {
            float velocityX = -heavyForgeResetSpeed;

            while (velocityX < heavyForgeMaxSpeed)
            {
                if (blocked)
                {
                    heavyForgeWasBlocked = true;
                    break;
                }

                velocityX += heavyForgeStartAcce * Time.deltaTime;
                if (velocityX > heavyForgeMaxSpeed)
                {
                    velocityX = heavyForgeMaxSpeed;
                }

                transform.position += new Vector3(velocityX * Time.deltaTime, 0f, 0f);
                yield return null;
            }
        }
        finally
        {
            // 启动阶段结束，禁用攻击碰撞箱
            SetSawAttackActive(false);

            if (sawAttack != null)
            {
                sawAttack.OnBlocked -= onBlocked;
            }
        }
    }

    private IEnumerator HeavyForgeSaw_Dash()
    {
        heavyForgeWasBlocked = false;

        if (sawAttack != null)
        {
            sawAttack.ClearHitObjects();
        }

        bool blocked = false;
        System.Action<GameObject> onBlocked = _ => blocked = true;

        if (sawAttack != null)
        {
            sawAttack.OnBlocked += onBlocked;
        }

        // 突进终点：门内侧 (doorX + embedDistance)
        float endX = doorX + embedDistance;

        // 突进阶段，启用攻击碰撞箱
        SetSawAttackActive(true);

        try
        {
            while (transform.position.x < endX)
            {
                if (blocked)
                {
                    heavyForgeWasBlocked = true;
                    break;
                }

                float step = heavyForgeMaxSpeed * Time.deltaTime;
                float newX = transform.position.x + step;
                if (newX > endX)
                {
                    newX = endX;
                }

                transform.position = new Vector3(newX, transform.position.y, transform.position.z);
                yield return null;
            }
        }
        finally
        {
            // 突进阶段结束，禁用攻击碰撞箱
            SetSawAttackActive(false);

            if (sawAttack != null)
            {
                sawAttack.OnBlocked -= onBlocked;
            }
        }
    }

    private IEnumerator HeavyForgeSaw_Stun()
    {
        // 被格挡后原地僵持，等待 StunDuration + ParryWindow 超时或被弹反
        if (sawAttack == null)
        {
            yield break;
        }

        float stunTotal = sawAttack.StunDuration + sawAttack.ParryWindow;
        float elapsed = 0f;
        bool parryed = false;

        System.Action<GameObject> onParryed = _ => parryed = true;
        sawAttack.OnParryed += onParryed;

        try
        {
            while (elapsed < stunTotal)
            {
                if (parryed)
                {
                    break;
                }

                elapsed += Time.deltaTime;
                yield return null;
            }
        }
        finally
        {
            sawAttack.OnParryed -= onParryed;
        }
    }

    private IEnumerator HeavyForgeSaw_Knockback()
    {
        float speed = heavyForgeOpposeSpeed;

        while (speed > 0f)
        {
            transform.position += Vector3.left * (speed * Time.deltaTime);

            speed -= heavyForgeOpposeAcce * Time.deltaTime;
            if (speed < 0f)
            {
                speed = 0f;
            }

            yield return null;
        }
    }

    private IEnumerator HeavyForgeSaw_Cleanup()
    {
        // 恢复红温前的原始颜色
        if (spriteRenderer != null)
        {
            spriteRenderer.color = heavyForgeOriginalColor;
        }

        if(sawAttack != null) { sawAttack.Grade = AttackGrade.Light; }

        yield break;
    }

    // ---------------动作4：沉重锻击结束---------------



    // ---------------动作5：嵌入---------------

    [Header("动作5：嵌入 设置")]
    [Tooltip("嵌入时移动速度")]
    [SerializeField] private float embedSpeed;
    [Tooltip("嵌入动作移动距离")]
    [SerializeField] private float embedDistance;

    private readonly ActSeq embedSawSeq = new();

    private void InitEmbedSawActSeq()
    {
        var startCursor = embedSawSeq.Start;
        var endCursor = embedSawSeq.End;

        var embedNode = embedSawSeq.CreateActionNode(() => EmbedSaw_MoveDown());
        startCursor.SetNext(embedNode);
        embedNode.SetNext(endCursor);
    }

    /// <summary>
    /// 播放"嵌入"动作序列。
    /// </summary>
    public void PlayEmbedSaw()
    {
        if (embedSawSeq.IsPlaying)
        {
            return;
        }

        embedSawSeq.Play(this);
    }

    private IEnumerator EmbedSaw_MoveDown()
    {
        float traveled = 0f;
        float totalDistance = Mathf.Max(0f, embedDistance);
        float speed = Mathf.Max(0f, embedSpeed);

        while (traveled < totalDistance)
        {
            float step = speed * Time.deltaTime;
            if (traveled + step > totalDistance)
            {
                step = totalDistance - traveled;
            }

            traveled += step;
            transform.position += Vector3.down * step;
            yield return null;
        }

        // 移动完毕，隐藏电锯
        SetSawAppearance(false);
    }

    // ---------------动作5：嵌入结束---------------



    // ---------------动作6：僵直---------------

    [Header("动作6：僵直 设置")]
    [Tooltip("僵直时横向击退速度")]
    [SerializeField] private float staggerKnockbackSpeed = 5f;
    [Tooltip("僵直时横向击退距离")]
    [SerializeField] private float staggerKnockbackDistance = 1f;
    [Tooltip("僵直时震屏时间")]
    [SerializeField] private float staggerCameraShakeDuration = 0.5f; 
    [Tooltip("僵直时震屏强度")] 
    [SerializeField] private float staggerCameraShakeMagnitude = 0.3f;
    [Tooltip("僵直时冻结帧时长")]
    [SerializeField] private float staggerFreezeFrameDuration = 0.1f;

    private readonly ActSeq staggerSawSeq = new();

    private void InitStaggerSawActSeq()
    {
        var startCursor = staggerSawSeq.Start;
        var endCursor = staggerSawSeq.End;

        // 第一步：横向击退
        var knockbackNode = staggerSawSeq.CreateActionNode(() => StaggerSaw_HorizontalKnockback());
        startCursor.SetNext(knockbackNode);

        // 第二步：纵向坠落（如果在 groundY 之上）
        var fallNode = staggerSawSeq.CreateActionNode(() => StaggerSaw_FallToGround());
        knockbackNode.SetNext(fallNode);

        // 第三步：僵直持续阶段，等待一段时间后恢复韧性
        var holdNode = staggerSawSeq.CreateActionNode(() => StaggerSaw_Hold());
        fallNode.SetNext(holdNode);

        holdNode.SetNext(endCursor);
    }

    /// <summary>
    /// 播放"僵直"动作序列。
    /// </summary>
    public void PlayStaggerSaw()
    {
        if (staggerSawSeq.IsPlaying)
        {
            return;
        }

        staggerSawSeq.Play(this);
    }

    private IEnumerator StaggerSaw_HorizontalKnockback()
    {
        // 僵直时确保电锯可见
        SetSawAppearance(true);
        // 恢复红温前的原始颜色
        if (spriteRenderer != null)
        {
            spriteRenderer.color = heavyForgeOriginalColor;
        }

        // 更长的闪白时间以突出僵直状态
        FlashEffect(flashDuration * 2, hurtFlashColor);
        // 震屏，方向向左以强调被击退的感觉
        CameraShakeManager.Instance.ShakeStraight(Vector3.left, staggerCameraShakeDuration, staggerCameraShakeMagnitude);
        // 冻结帧
        FreezeFrameManager.Instance.TriggerFreezeFrame(staggerFreezeFrameDuration);

        // 根据玩家位置决定击退方向：远离玩家
        float dirX = 1f;
        if (GetPlayer() && player != null)
        {
            dirX = player.transform.position.x < transform.position.x ? 1f : -1f;
        }

        float traveled = 0f;
        float totalDistance = Mathf.Max(0f, staggerKnockbackDistance);
        float speed = Mathf.Max(0f, staggerKnockbackSpeed);

        while (traveled < totalDistance)
        {
            float step = speed * Time.deltaTime;
            if (traveled + step > totalDistance)
            {
                step = totalDistance - traveled;
            }

            traveled += step;
            transform.position += new Vector3(dirX * step, 0f, 0f);
            yield return null;
        }
    }

    private IEnumerator StaggerSaw_FallToGround()
    {
        // 如果当前 Y 坐标已在 groundY 或以下，无需坠落
        if (transform.position.y <= groundY)
        {
            yield break;
        }

        // 纵向初速度为 0，应用 sawGravityScale 做坠落
        float velocityY = 0f;
        float gravity = sawGravityScale * Physics2D.gravity.y;

        while (transform.position.y > groundY)
        {
            velocityY += gravity * Time.deltaTime;
            float newY = transform.position.y + velocityY * Time.deltaTime;

            if (newY <= groundY)
            {
                transform.position = new Vector3(transform.position.x, groundY, transform.position.z);
                break;
            }

            transform.position = new Vector3(transform.position.x, newY, transform.position.z);
            yield return null;
        }
    }

    private IEnumerator StaggerSaw_Hold()
    {
        yield return new WaitForSeconds(staggerHoldDuration);

        // 破韧触发的僵直结束后，恢复韧性至满值
        if (isToughnessBreak)
        {
            toughness.CurrentAmount = toughness.MaxAmount;
            isToughnessBreak = false;
        }
    }

    // ---------------动作6：僵直结束---------------



    // ---------------其他方法---------------

    /// <summary>
    /// 设置电锯的可见性，同时同步启用/禁用自身 Collider。
    /// 电锯不出现时不应接收碰撞检测。
    /// </summary>
    private void SetSawAppearance(bool visible)
    {
        if (spriteRenderer != null)
        {
            spriteRenderer.enabled = visible;
        }
        if (sawCollider != null)
        {
            sawCollider.enabled = visible;
        }
    }

    /// <summary>
    /// 设置电锯攻击碰撞箱的启用状态。
    /// </summary>
    private void SetSawAttackActive(bool active)
    {
        if (sawAttack != null)
        {
            sawAttack.AttackPosition = active ? Position.Hostile : Position.None;
            sawAttack.ClearHitObjects();
        }
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
            _material.SetFloat("_flashFactor", Mathf.Lerp(colorCoverRate, 0f, elapsed / duration));
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

    // ---------------其他方法结束---------------



    // ---------------韧性条逻辑---------------

    private void OnSawAttackBlocked(GameObject blocker)
    {
        if (!IsPlayerObject(blocker)) return;
        ReduceToughness(toughnessReduceOnBlocked);
        AudioManager.PlaySound(blockedByPlayerClip, transform.position, volume);
    }

    /// <summary>
    /// 检测来自玩家的攻击碰撞，减少韧性值。
    /// </summary>
    private void HandleIncomingPlayerAttack(GameObject other)
    {
        if (other.TryGetComponent<AttackHitInfo>(out var hitInfo))
        {
            if (hitInfo.GetHitResult(gameObject) != HitResult.None)
            {
                return;
            }

            if (hitInfo.AttackPosition == Position.Hostile ||
                hitInfo.AttackPosition == Position.None)
            {
                return;
            }

            hitInfo.RecordHitObject(gameObject);
            ReduceToughness(toughnessReduceOnHit);
            FlashEffect(flashDuration, hurtFlashColor);
            AudioManager.PlaySound(hurtByPlayerClip, transform.position, volume);
            Sharpness.Instance.IncreaseSharpness(sharpnessRestoreOnHit);
        }
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        HandleIncomingPlayerAttack(other.gameObject);
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        HandleIncomingPlayerAttack(collision.gameObject);
    }

    /// <summary>
    /// 减少韧性值。韧性归零时中断当前动作并进入僵直。
    /// </summary>
    public void ReduceToughness(float amount)
    {
        if (isToughnessBreak || staggerSawSeq.IsPlaying)
        {
            return;
        }

        toughness.Decrease(amount);

        if (toughness.CurrentAmount <= 0f)
        {
            TriggerToughnessBreak();
        }
    }

    private void TriggerToughnessBreak()
    {
        isToughnessBreak = true;
        StopAllSawActions();
        PlayStaggerSaw();
    }

    /// <summary>
    /// 强制中断所有非僵直动作序列。
    /// </summary>
    /// <summary>
    /// 强制中断所有非僵直动作序列。
    /// </summary>
    private void StopAllSawActions()
    {
        leapSawSeq.Stop();
        grindSawSeq.Stop();
        forgeSawSeq.Stop();
        heavyForgeSawSeq.Stop();
        embedSawSeq.Stop();

        // 强制中断时确保攻击碰撞箱关闭
        SetSawAttackActive(false);
    }

    // ---------------韧性条逻辑结束---------------



    // ---------------公共状态访问（供Boss1查询）---------------

    public bool IsLeapSawPlaying => leapSawSeq.IsPlaying;
    public bool IsGrindSawPlaying => grindSawSeq.IsPlaying;
    public bool IsForgeSawPlaying => forgeSawSeq.IsPlaying;
    public bool IsHeavyForgeSawPlaying => heavyForgeSawSeq.IsPlaying;
    public bool IsEmbedSawPlaying => embedSawSeq.IsPlaying;
    public bool IsStaggerSawPlaying => staggerSawSeq.IsPlaying;

    /// <summary>锻击是否因格挡触发了击退。</summary>
    public bool WasForgeBlocked => forgeWasBlocked;
    /// <summary>沉重锻击是否因格挡触发了击退。</summary>
    public bool WasHeavyForgeBlocked => heavyForgeWasBlocked;
    /// <summary>当前是否处于破韧僵直中。</summary>
    public bool IsToughnessBreaking => isToughnessBreak;

    // ---------------公共状态访问结束---------------



    // ---------------获取玩家方法---------------

    private bool GetPlayer()
    {
        if (GlobalPlayer.Instance != null && GlobalPlayer.Instance.Player != null)
        {
            player = GlobalPlayer.Instance.Player;
            return true;
        }
        else { return false; }
        // 在GlobalPlayer中检索不到玩家，则视为玩家已死亡，此时可以执行一些清理或重置逻辑
    }

    /// <summary>
    /// 判断指定对象是否属于玩家（玩家本体或其子物体）。
    /// 严格通过 GlobalPlayer 获取玩家引用。
    /// </summary>
    private bool IsPlayerObject(GameObject obj)
    {
        if (obj == null) return false;
        if (!GetPlayer() || player == null) return false;
        return obj.transform == player.transform || obj.transform.IsChildOf(player.transform);
    }

    // --------------获取玩家结束---------------



    // ---------------处理火星的生成逻辑---------------

    [Header("火星生成设置")]
    [Tooltip("火星预制体")]
    [SerializeField] GameObject firePerfab;
    [Tooltip("火星对象池初始容量")]
    [SerializeField] int initialFirePoolSize = 64;
    [Tooltip("火星存在时间（秒）")]
    [SerializeField] float fireLifetime = 3f;

    private readonly Queue<GameObject> firePool = new();
    private Coroutine fireSpawnRoutine;
    private bool firePoolInitialized;

    public void SpawnFires(int amount, float duration, float speed)
    {
        EnsureFirePoolInitialized();
        if (!firePoolInitialized || amount <= 0 || duration <= 0f || speed <= 0f)
        {
            return;
        }

        if (fireSpawnRoutine != null)
        {
            StopCoroutine(fireSpawnRoutine);
            fireSpawnRoutine = null;
        }

        fireSpawnRoutine = StartCoroutine(SpawnFiresRoutine(amount, duration, speed));
    }

    private IEnumerator SpawnFiresRoutine(int amount, float duration, float speed)
    {
        float interval = duration / amount;
        for (int i = 0; i < amount; i++)
        {
            Vector2 direction = Random.insideUnitCircle;
            if (direction.sqrMagnitude < 0.01f)
            {
                direction = Vector2.right;
            }
            else
            {
                direction.Normalize();
            }

            EmitFire(transform.position, speed, direction);

            if (i < amount - 1 && interval > 0f)
            {
                yield return new WaitForSeconds(interval);
            }
        }

        fireSpawnRoutine = null;
    }

    private void EmitFire(Vector3 position, float speed, Vector2 direction)
    {
        if (firePerfab == null)
        {
            return;
        }

        GameObject fire = GetFireInstance();
        fire.transform.SetParent(null);
        fire.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg));
        fire.SetActive(true);

        Vector2 normalizedDir = direction.sqrMagnitude > 0f ? direction.normalized : Vector2.right;
        if (fire.TryGetComponent<Rigidbody2D>(out var rb2d))
        {
            rb2d.velocity = normalizedDir * speed;
        }
        else if (fire.TryGetComponent<Rigidbody>(out var rb3d))
        {
            rb3d.velocity = new Vector3(normalizedDir.x, normalizedDir.y, 0f) * speed;
        }

        var poolMember = AttachPooledFire(fire);
        poolMember.ScheduleReturn(Mathf.Max(0f, fireLifetime));
    }

    private GameObject GetFireInstance()
    {
        if (firePool.Count > 0)
        {
            return firePool.Dequeue();
        }

        var instance = Instantiate(firePerfab);
        instance.SetActive(false);
        AttachPooledFire(instance);
        return instance;
    }

    private FirePoolMember AttachPooledFire(GameObject fire)
    {
        var member = fire.GetComponent<FirePoolMember>();
        if (member == null)
        {
            member = fire.AddComponent<FirePoolMember>();
        }

        member.Initialize(this);
        return member;
    }

    private void ReturnFire(GameObject fire)
    {
        if (fire == null)
        {
            return;
        }

        if (fire.TryGetComponent<Rigidbody2D>(out var rb2d))
        {
            rb2d.velocity = Vector2.zero;
        }
        else if (fire.TryGetComponent<Rigidbody>(out var rb3d))
        {
            rb3d.velocity = Vector3.zero;
        }

        fire.transform.SetParent(transform);
        fire.SetActive(false);
        firePool.Enqueue(fire);
    }

    private void EnsureFirePoolInitialized()
    {
        if (firePoolInitialized)
        {
            return;
        }

        if (firePerfab == null)
        {
            Debug.LogWarning($"{nameof(Saw)}: firePerfab 未设置，无法生成火星。", this);
            return;
        }

        InitializeFirePool();
        firePoolInitialized = true;
    }

    private void InitializeFirePool()
    {
        firePool.Clear();

        if (firePerfab == null)
        {
            return;
        }

        int count = Mathf.Max(0, initialFirePoolSize);
        for (int i = 0; i < count; i++)
        {
            var instance = Instantiate(firePerfab, transform);
            instance.SetActive(false);
            AttachPooledFire(instance);
            firePool.Enqueue(instance);
        }
    }

    private class FirePoolMember : MonoBehaviour
    {
        private Saw owner;
        private Coroutine returnRoutine;

        public void Initialize(Saw owner)
        {
            this.owner = owner;
        }

        public void ScheduleReturn(float delay)
        {
            if (returnRoutine != null)
            {
                StopCoroutine(returnRoutine);
                returnRoutine = null;
            }

            if (delay <= 0f)
            {
                owner?.ReturnFire(gameObject);
                return;
            }

            returnRoutine = StartCoroutine(ReturnAfterDelay(delay));
        }

        private IEnumerator ReturnAfterDelay(float delay)
        {
            yield return new WaitForSeconds(delay);
            owner?.ReturnFire(gameObject);
            returnRoutine = null;
        }

        private void OnDisable()
        {
            if (returnRoutine != null)
            {
                StopCoroutine(returnRoutine);
                returnRoutine = null;
            }
        }
    }

    // ---------------处理火星的生成逻辑结束---------------
}
