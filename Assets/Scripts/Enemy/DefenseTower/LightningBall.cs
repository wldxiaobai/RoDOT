using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Rigidbody2D))]
public class LightningBall : MonoBehaviour
{
    [Header("蓄力设置")]
    [Tooltip("蓄力所需时间（秒）")]
    [SerializeField, Min(0f)] private float chargeDuration = 0.5f;

    [Header("发射设置")]
    [Tooltip("默认发射速度")]
    [SerializeField, Min(0f)] private float baseLaunchSpeed = 5f;
    [Tooltip("最大允许的发射持续时间，达到后销毁")]
    [SerializeField, Min(0f)] private float maxFlightTime = 2f;
    [Tooltip("电球产生音效")]
    [SerializeField] private AudioClip chargeSound;
    [Tooltip("电球发射音效")]
    [SerializeField] private AudioClip launchSound;
    [Tooltip("音量")]
    [Range(0f, 1f)]
    [SerializeField] private float soundVolume = 0.6f;

    private Rigidbody2D _rigidbody2D;
    private float _remainingChargeTime;
    private float _remainingFlightTime;
    private Vector2 _launchDirection = Vector2.right;
    private float _launchSpeed;
    private bool _hasLaunched;
    private SpriteRenderer _spriteRenderer;
    private Color _spriteBaseColor = Color.white;

    public void SetLaunchDirection(Vector2 direction)
    {
        if (direction == Vector2.zero)
        {
            return;
        }

        _launchDirection = direction.normalized;
    }

    public void SetLaunchSpeed(float speed)
    {
        if (speed <= 0f)
        {
            return;
        }

        _launchSpeed = speed;
    }

    public void ConfigureLaunch(Vector2 direction, float speed)
    {
        SetLaunchDirection(direction);
        SetLaunchSpeed(speed);
    }

    private AttackHitInfo _attackHitInfo;
    private bool _blockedResponseActive;
    private float _blockedRemainingTime;

    private void Awake()
    {
        _rigidbody2D = GetComponent<Rigidbody2D>();
        _spriteRenderer = GetComponent<SpriteRenderer>();
        if (_spriteRenderer != null)
        {
            _spriteBaseColor = _spriteRenderer.color;
            SetChargeAlpha(0f);
        }
        _remainingChargeTime = chargeDuration;
        _remainingFlightTime = Mathf.Max(0f, maxFlightTime);
        _launchSpeed = baseLaunchSpeed;
        _launchDirection = transform.right;
        _rigidbody2D.velocity = Vector2.zero;
        _attackHitInfo = GetComponent<AttackHitInfo>();
        if (_attackHitInfo != null)
        {
            _attackHitInfo.OnBlocked += HandleAttackBlocked;
        }
        if (chargeSound != null)
        {
            AudioManager.PlaySound(chargeSound, transform.position, soundVolume);
        }
    }

    private void Update()
    {
        if (_blockedResponseActive)
        {
            if (_blockedRemainingTime <= 0f)
            {
                Destroy(gameObject);
                return;
            }

            _blockedRemainingTime -= Time.deltaTime;
            return;
        }

        if (_hasLaunched)
        {
            if (_remainingFlightTime <= 0f)
            {
                Destroy(gameObject);
                return;
            }

            _remainingFlightTime -= Time.deltaTime;
            return;
        }

        _remainingChargeTime -= Time.deltaTime;
        UpdateChargeTransparency();
        if (_remainingChargeTime <= 0f)
        {
            BeginLaunch();
        }
    }

    private void FixedUpdate()
    {
        if (_hasLaunched && _rigidbody2D != null)
        {
            _rigidbody2D.velocity = _launchDirection * _launchSpeed;
        }
    }

    private void BeginLaunch()
    {
        if (_hasLaunched)
        {
            return;
        }

        _hasLaunched = true;
        _remainingFlightTime = Mathf.Max(0f, maxFlightTime);
        _rigidbody2D.velocity = _launchDirection * _launchSpeed;
        SetChargeAlpha(1f);
        if (launchSound != null)
        {
            AudioManager.PlaySound(launchSound, transform.position, soundVolume);
        }
    }

    private void UpdateChargeTransparency()
    {
        if (_spriteRenderer == null || chargeDuration <= 0f)
        {
            return;
        }

        var progress = Mathf.Clamp01(1f - (_remainingChargeTime / chargeDuration));
        var alpha = Mathf.Lerp(0f, 1f, progress);
        SetChargeAlpha(alpha);
    }

    private void SetChargeAlpha(float alpha)
    {
        if (_spriteRenderer == null)
        {
            return;
        }

        var color = _spriteBaseColor;
        color.a = Mathf.Clamp01(alpha);
        _spriteRenderer.color = color;
    }

    private void HandleAttackBlocked(GameObject victim)
    {
        if (_blockedResponseActive)
        {
            return;
        }

        _blockedResponseActive = true;
        var time = _attackHitInfo.StunDuration + _attackHitInfo.ParryWindow + 0.08f; // 额外添加一个小的缓冲时间
        _blockedRemainingTime = _attackHitInfo != null ? Mathf.Max(0f, time) : 0f;
        _hasLaunched = false;
        if (_rigidbody2D != null)
        {
            _rigidbody2D.velocity = Vector2.zero;
        }
    }

    private void OnDestroy()
    {
        if (_attackHitInfo != null)
        {
            _attackHitInfo.OnBlocked -= HandleAttackBlocked;
        }
    }
}
