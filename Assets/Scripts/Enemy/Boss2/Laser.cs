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

    [Header("子物体设置")]
    [Tooltip("激光主体")]
    [SerializeField] private GameObject laserBody;
    [Tooltip("激光头")]
    [SerializeField] private GameObject laserHead;

    private readonly ActSeq actSeq = new();

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
    /// </summary>
    private IEnumerator DurationPhase()
    {
        yield return new WaitForSeconds(durationTime);
    }

    /// <summary>
    /// 消失阶段：laserBody 的 scale.y 从 width 渐进到 0，然后取消激活。
    /// </summary>
    private IEnumerator DisappearPhase()
    {
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
