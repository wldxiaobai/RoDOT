using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static AttackHitInfo;

public class BaseEnemy : MonoBehaviour
{
    [Header("AI相关设置")]
    [Tooltip("是否需要入场动作")]
    [SerializeField] private bool needEntrance = false;
    [Tooltip("死亡后销毁自身")]
    [SerializeField] private bool destroyOnDeath = true;

    [Header("敌怪属性")]
    [Tooltip("最大生命值")]
    [SerializeField] private int maxHP = 100;
    [Tooltip("敌怪名字")]
    [SerializeField] private string enemyName = "Enemy";

    public bool NeedEntrance => needEntrance;
    public int MaxHP => maxHP;
    private int currentHP;
    public int CurrentHP => currentHP;
    public bool DestroyOnDeath => destroyOnDeath;

    private Animator _animator;
    private float _defaultAnimatorSpeed;
    private Timer InvincibleTimer = new();
    protected Animator Animator => _animator != null ? _animator : _animator = GetComponent<Animator>();
    private Transform _lastHitSource;
    private Vector2 _lastHitDirection = Vector2.right;
    protected Transform LastHitSource => _lastHitSource;
    protected Vector2 LastHitDirection => _lastHitDirection;

    public struct EnemyBehaviour
    {
        public float preSkillDelay;
        public float[] AIFactor;
        public ActSeq skillBody;

        public EnemyBehaviour(float preSkillDelay)
        {
            this.preSkillDelay = preSkillDelay;
            this.AIFactor = new float[4];
            this.skillBody = null;
        }

        public void SetBehaviour(ActSeq behav)
        {
            if (behav == null)
            {
                skillBody = null;
                return;
            }

            behav.CloneTo(ref this.skillBody);
        }

        public EnemyBehaviour Invoke(MonoBehaviour mono)
        {
            if (CanUse) skillBody.Play(mono);
            return this;
        }

        public bool CanUse => skillBody != null && !skillBody.IsPlaying;
    }

    private const string EntranceStateName = "Entrance";
    private const string DecisionStateName = "Decision";
    private const string BehaviourStateName = "Behaviour";
    private const string DeathStateName = "Death";
    private const string BlankBehaviourName = "Blank";

    private ActSeq entrance = new();
    private Dictionary<string, EnemyBehaviour> behaviourDictionary = new();
    private ActSeq death = new();
    private EnemyBehaviour currentBehaviour;
    private HierarchicalStateMachine enemyStateMachine;
    private HitInfo incomingHitInfo;
    protected string CurrentEnemyStateName => enemyStateMachine?.CurrentStateName ?? string.Empty;

    private bool hasEntranceSequence;
    private bool hasDeathSequence;
    private bool entranceSequencePlaying;
    private bool behaviourSequenceStarted;
    private bool behaviourDelayActive;
    private float behaviourDelayRemaining;
    private bool deathSequenceStarted;
    private bool deathSequenceFinished;
    private string currentBehaviourName = BlankBehaviourName;

    private bool ShouldPlayEntrance => NeedEntrance && hasEntranceSequence;

    public void AddBehaviour(string skillName, float preSkillDelay, ActSeq skillBody = null)
    {
        var newBehaviour = new EnemyBehaviour(preSkillDelay);
        if (skillBody != null)
        {
            newBehaviour.SetBehaviour(skillBody);
        }
        if (behaviourDictionary.ContainsKey(skillName))
        {
        }
        behaviourDictionary[skillName] = newBehaviour;
    }

    public EnemyBehaviour GetBehaviour(string skillName)
    {
        if (behaviourDictionary.TryGetValue(skillName, out var behaviour))
        {
            return behaviour;
        }
        else
        {
            return new EnemyBehaviour(0f);
        }
    }

    public void SetEntrance(ActSeq skillBody)
    {
        if (skillBody == null)
        {
            entrance.Clear();
            hasEntranceSequence = false;
            return;
        }

        skillBody.CloneTo(ref entrance);
        hasEntranceSequence = true;
    }

    public void SetDeath(ActSeq skillBody)
    {
        if (skillBody == null)
        {
            death.Clear();
            hasDeathSequence = false;
            return;
        }

        skillBody.CloneTo(ref death);
        hasDeathSequence = true;
    }

    private void Awake()
    {
        currentHP = maxHP;
        ActSeq defaultBehav = new();
        IEnumerator BlankBehav()
        {
            yield return null;
        }
        var blank = defaultBehav.CreateActionNode(BlankBehav);
        defaultBehav.Start.SetNext(blank);
        blank.SetNext(defaultBehav.End);
        AddBehaviour(BlankBehaviourName, 0f, defaultBehav);
        BuildStateMachine();
        _defaultAnimatorSpeed = _animator != null ? _animator.speed : 1f;
        EnemyInit();
    }

    protected virtual void EnemyInit()
    {
        // 子类可以重写此方法进行初始化
    }

    private void Start()
    {
        enemyStateMachine.Enter();
    }

    private void Update()
    {
        if (enemyStateMachine == null)
        {
            return;
        }

        if (currentHP <= 0)
        {
            ForceDeath();
        }

        enemyStateMachine.Stay();
        InvincibleTimer?.Update();
    }

    private void BuildStateMachine()
    {
        enemyStateMachine = new HierarchicalStateMachine(enemyName);

        var entranceState = new SimpleState(
            EntranceStateName,
            enter: OnEntranceEnter,
            stay: OnEntranceStay,
            exit: OnEntranceExit);

        var decisionState = new SimpleState(
            DecisionStateName,
            enter: OnDecisionEnter);

        var behaviourState = new SimpleState(
            BehaviourStateName,
            enter: OnBehaviourEnter,
            stay: OnBehaviourStay,
            exit: OnBehaviourExit);

        var deathState = new SimpleState(
            DeathStateName,
            enter: OnDeathEnter,
            stay: OnDeathStay);

        enemyStateMachine
            .RegisterState(entranceState, needEntrance)
            .RegisterState(decisionState, !needEntrance)
            .RegisterState(behaviourState)
            .RegisterState(deathState);
    }

    private void OnEntranceEnter()
    {
        entranceSequencePlaying = false;
        if (!ShouldPlayEntrance)
        {
            enemyStateMachine.TransitionTo(DecisionStateName);
            return;
        }

        entrance.Play(this);
        entranceSequencePlaying = true;
    }

    private void OnEntranceStay()
    {
        if (entranceSequencePlaying && !entrance.IsPlaying)
        {
            entranceSequencePlaying = false;
            enemyStateMachine.TransitionTo(DecisionStateName);
        }
    }

    private void OnEntranceExit()
    {
        if (entrance.IsPlaying)
        {
            entrance.Stop();
        }
        entranceSequencePlaying = false;
    }

    private void OnDecisionEnter()
    {
        PrepareNextBehaviour();
        enemyStateMachine.TransitionTo(BehaviourStateName);
    }

    private void PrepareNextBehaviour()
    {
        var requestedBehaviour = DecideNextBehaviour();
        if (string.IsNullOrWhiteSpace(requestedBehaviour))
        {
            requestedBehaviour = BlankBehaviourName;
        }

        currentBehaviour = GetBehaviour(requestedBehaviour);
        if (currentBehaviour.skillBody == null)
        {
            currentBehaviour = GetBehaviour(BlankBehaviourName);
            requestedBehaviour = BlankBehaviourName;
        }

        currentBehaviourName = requestedBehaviour;
        behaviourDelayRemaining = Mathf.Max(0f, currentBehaviour.preSkillDelay);
        behaviourDelayActive = behaviourDelayRemaining > 0f;
        behaviourSequenceStarted = false;
    }

    private void OnBehaviourEnter()
    {
        if (currentBehaviour.skillBody == null)
        {
            currentBehaviour = GetBehaviour(BlankBehaviourName);
            currentBehaviourName = BlankBehaviourName;
        }

        behaviourDelayRemaining = Mathf.Max(0f, currentBehaviour.preSkillDelay);
        behaviourDelayActive = behaviourDelayRemaining > 0f;
        behaviourSequenceStarted = false;

        if (!behaviourDelayActive)
        {
            StartBehaviourSequence();
        }
    }

    private void OnBehaviourStay()
    {
        if (behaviourSequenceStarted)
        {
            if (currentBehaviour.skillBody == null || currentBehaviour.skillBody.EndedTrigger)
            {
                if(currentBehaviour.skillBody == null)
                {
                    Debug.Log($"未注册 {currentBehaviourName}");
                }
                else
                enemyStateMachine.TransitionTo(DecisionStateName);
            }
            return;
        }

        if (behaviourDelayActive)
        {
            behaviourDelayRemaining -= Time.deltaTime;
            if (behaviourDelayRemaining <= 0f)
            {
                behaviourDelayActive = false;
                StartBehaviourSequence();
            }
        }
    }

    private void OnBehaviourExit()
    {
        if (currentBehaviour.skillBody != null && currentBehaviour.skillBody.IsPlaying)
        {
            currentBehaviour.skillBody.Stop();
        }
        behaviourSequenceStarted = false;
        behaviourDelayActive = false;
    }

    private void OnDeathEnter()
    {
        deathSequenceStarted = false;
        deathSequenceFinished = false;

        if (hasDeathSequence)
        {
            death.Play(this);
            deathSequenceStarted = true;
        }
        else
        {
            deathSequenceFinished = true;
            FinalizeDeath();
        }
    }

    private void OnDeathStay()
    {
        if (deathSequenceFinished)
        {
            return;
        }

        if (deathSequenceStarted && death.IsPlaying)
        {
            return;
        }

        deathSequenceFinished = true;
        FinalizeDeath();
    }

    private void StartBehaviourSequence()
    {
        if (currentBehaviour.skillBody == null)
        {
            behaviourSequenceStarted = false;
            return;
        }

        currentBehaviour.skillBody.Play(this);
        behaviourSequenceStarted = true;
    }

    private void FinalizeDeath()
    {
        if (destroyOnDeath)
        {
            Destroy(gameObject);
        }
    }

    private void ForceDeath()
    {
        currentHP = Mathf.Min(0, currentHP);
        if (enemyStateMachine == null)
        {
            return;
        }

        if (enemyStateMachine.CurrentStateName == DeathStateName)
        {
            return;
        }

        enemyStateMachine.TransitionTo(DeathStateName);
        Debug.Log($"[{enemyName}] HP dropped to 0. Transitioning to {DeathStateName}.");
    }

    protected virtual string DecideNextBehaviour()
    {
        return BlankBehaviourName;
    }


    private void HandleIncomingAttack(GameObject other)
    {
        if(other.TryGetComponent<AttackHitInfo>(out var hitInfo))
        {
            if (hitInfo.GetHitResult(gameObject) != HitResult.None || 
                hitInfo.AttackPosition == Position.Hostile ||
                hitInfo.AttackPosition == Position.None ||
                InvincibleTimer.IsRunning) 
                return;
            var incoming = hitInfo.GetHitInfo();

            if (incoming.IsValid)
            {
                currentHP -= Mathf.RoundToInt(incoming.Damage);
                hitInfo.RecordHitObject(gameObject);
                OnHitByPlayerAttack(incoming);
            }
        }
    }

    protected virtual void OnHitByPlayerAttack(HitInfo incoming)
    {
        _lastHitSource = incoming.Source?.transform;
        if (_lastHitSource != null)
        {
            var delta = _lastHitSource.position - transform.position;
            _lastHitDirection = delta.sqrMagnitude > 0f ? delta.normalized : Vector2.right;
        }
    }

    protected void StartInvincibleTimer(float duration)
    {
        InvincibleTimer.StartTimer(duration);
    }

    protected bool IsInvincible => InvincibleTimer.IsRunning;

    protected void StopInvincibleTimer()
    {
        InvincibleTimer.StopTimer();
    }

    protected void ForceRedecide()
    {
        if (enemyStateMachine == null) return;
        if (enemyStateMachine.CurrentStateName == DeathStateName) return;
        enemyStateMachine.TransitionTo(DecisionStateName);
    }

    public void OnCollisionEnter2D(Collision2D collision)
    {
        HandleIncomingAttack(collision.gameObject);
    }
    public void OnTriggerEnter2D(Collider2D collider)
    {
        HandleIncomingAttack(collider.gameObject);
    }

    // 动画速度调整工具
    protected void AdjustAnimatorSpeedForClip(string clipName, float desiredDuration)
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

    protected AnimationClip GetAnimationClipByName(string clipName)
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

    protected void ResetAnimatorSpeed()
    {
        if (_animator == null)
        {
            return;
        }

        _animator.speed = _defaultAnimatorSpeed;
    }
}
