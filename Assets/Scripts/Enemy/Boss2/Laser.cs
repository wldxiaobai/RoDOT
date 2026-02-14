using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Laser : MonoBehaviour
{
    [Header("动作设置")]
    [Tooltip("激光出现用时")]
    [SerializeField] private float appearTime = 0.5f;
    [Tooltip("激光宽度参数")]
    [SerializeField] private float width = 0.5f;
    [Tooltip("激光持续时间")]
    [SerializeField] private float durationTime = 2f;
    [Tooltip("激光消失用时")]
    [SerializeField] private float disappearTime = 0.5f;

    [Header("音效设置")]
    [Tooltip("音量")]
    [Range(0f, 1f)] [SerializeField] private float volume = 1f;
    [Tooltip("激光出现音效")]
    [SerializeField] private AudioClip appearSfx;
    [Tooltip("激光爆发音效")]
    [SerializeField] private AudioClip burstSfx;
    [Tooltip("激光持续音效")]
    [SerializeField] private AudioClip durationSfx;
    [Tooltip("激光消失音效")]
    [SerializeField] private AudioClip disappearSfx;
    [Tooltip("激光音效淡出时间")]
    [SerializeField] private float sfxFadeOutTime = 0.5f;

    [Header("子物体设置")]
    [Tooltip("激光主体")]
    [SerializeField] private GameObject laserBody;
    [Tooltip("激光头")]
    [SerializeField] private GameObject laserHead;

    private readonly ActSeq actSeq = new();
    private SoundHandle? currentSfxHandle = null;

    public bool IsActive => actSeq.IsPlaying;
    public float TotalDuration => appearTime + durationTime + disappearTime;

    private void Awake()
    {
        laserBody.SetActive(false);
        laserHead.SetActive(false);
        BuildSequence();
    }

    private void Start()
    {
        actSeq.Play(this);
    }

    private void OnDestroy()
    {
        actSeq.Stop();
    }

    private void BuildSequence()
    {
        var startCursor = actSeq.Start;
        var endCursor = actSeq.End;

        var appearNode = actSeq.CreateActionNode(() => AppearPhase());
        startCursor.SetNext(appearNode);

        var durationNode = actSeq.CreateActionNode(() => DurationPhase());
        appearNode.SetNext(durationNode);

        var disappearNode = actSeq.CreateActionNode(() => DisappearPhase());
        durationNode.SetNext(disappearNode);

        disappearNode.SetNext(endCursor);
    }

    /// <summary>
    /// 出现阶段：激活子物体，laserHead 直接设为目标尺寸，laserBody 的 scale.y 从 0 渐进到 width。
    /// </summary>
    private IEnumerator AppearPhase()
    {
        laserBody.SetActive(true);
        laserHead.SetActive(true);

        if (currentSfxHandle.HasValue && currentSfxHandle.Value.IsPlaying)
        {
            AudioManager.StopSound(currentSfxHandle.Value, sfxFadeOutTime);
        }
        currentSfxHandle = AudioManager.PlaySound(appearSfx, transform.position, volume);

        // laserHead 直接设置目标尺寸
        var headScale = laserHead.transform.localScale;
        headScale.x = width;
        headScale.y = width;
        laserHead.transform.localScale = headScale;

        // laserBody 初始 scale.y = 0
        var bodyScale = laserBody.transform.localScale;
        bodyScale.y = 0f;
        laserBody.transform.localScale = bodyScale;

        float elapsed = 0f;
        while (elapsed < appearTime)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / appearTime);
            bodyScale = laserBody.transform.localScale;
            bodyScale.y = Mathf.Lerp(0f, width, t);
            laserBody.transform.localScale = bodyScale;
            yield return null;
        }

        // 确保最终值精确
        bodyScale = laserBody.transform.localScale;
        bodyScale.y = width;
        laserBody.transform.localScale = bodyScale;
    }

    /// <summary>
    /// 持续阶段：保持激光不变，等待 durationTime。
    /// burstSfx 播完前以淡入淡出方式交叉过渡到 durationSfx。
    /// </summary>
    private IEnumerator DurationPhase()
    {
        if (currentSfxHandle.HasValue && currentSfxHandle.Value.IsPlaying)
        {
            AudioManager.StopSound(currentSfxHandle.Value, sfxFadeOutTime);
        }
        currentSfxHandle = AudioManager.PlaySound(burstSfx, transform.position, volume);

        bool crossfadeStarted = false;
        float elapsed = 0f;
        while (elapsed < durationTime)
        {
            elapsed += Time.deltaTime;

            if (!crossfadeStarted && currentSfxHandle.HasValue)
            {
                float remaining = currentSfxHandle.Value.RemainingTime;
                if (remaining <= sfxFadeOutTime && remaining > 0f)
                {
                    AudioManager.StopSound(currentSfxHandle.Value, remaining);
                    currentSfxHandle = AudioManager.PlaySoundWithFadeIn(durationSfx, transform.position, volume, remaining);
                    crossfadeStarted = true;
                }
                else if (!currentSfxHandle.Value.IsPlaying)
                {
                    currentSfxHandle = AudioManager.PlaySound(durationSfx, transform.position, volume);
                    crossfadeStarted = true;
                }
            }

            yield return null;
        }
    }

    /// <summary>
    /// 消失阶段：laserBody 的 scale.y 从 width 渐进到 0，然后取消激活。
    /// </summary>
    private IEnumerator DisappearPhase()
    {
        if (currentSfxHandle.HasValue && currentSfxHandle.Value.IsPlaying)
        {
            AudioManager.StopSound(currentSfxHandle.Value, sfxFadeOutTime);
        }
        currentSfxHandle = AudioManager.PlaySound(disappearSfx, transform.position, volume);

        float elapsed = 0f;
        while (elapsed < disappearTime)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / disappearTime);
            var bodyScale = laserBody.transform.localScale;
            bodyScale.y = Mathf.Lerp(width, 0f, t);
            laserBody.transform.localScale = bodyScale;
            yield return null;
        }

        // 确保最终值精确并取消激活
        var finalScale = laserBody.transform.localScale;
        finalScale.y = 0f;
        laserBody.transform.localScale = finalScale;

        laserBody.SetActive(false);
        laserHead.SetActive(false);
    }
}
