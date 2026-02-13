#nullable enable
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FlyingSword : MonoBehaviour
{
    [Header("动作设置")]
    [Tooltip("瞄准时间")]
    [SerializeField] private float aimTime = 1f;
    [Tooltip("停顿时间")]
    [SerializeField] private float pauseTime = 0.5f;
    [Tooltip("飞行速度")]
    [SerializeField] private float flySpeed = 5f;
    [Tooltip("瞄准旋转Lerp速度")]
    [SerializeField] private float aimLerpSpeed = 5f;
    [Tooltip("飞行最大时间")]
    [SerializeField] private float flyLifetime = 5f;

    private Transform? player;
    private readonly ActSeq actSeq = new();
    private Vector2 flyDirection;

    private void Awake()
    {
        // 生成时贴图方向随机
        float randomAngle = Random.Range(0f, 360f);
        transform.rotation = Quaternion.Euler(0f, 0f, randomAngle);

        BuildSequence();
    }

    private void Start()
    {
        GetPlayer();
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

        var aimNode = actSeq.CreateActionNode(() => AimPhase());
        startCursor.SetNext(aimNode);

        var pauseNode = actSeq.CreateActionNode(() => PausePhase());
        aimNode.SetNext(pauseNode);

        var flyNode = actSeq.CreateActionNode(() => FlyPhase());
        pauseNode.SetNext(flyNode);

        flyNode.SetNext(endCursor);
    }

    /// <summary>
    /// 瞄准阶段：在 aimTime 内以 Lerp 突跃方式将贴图方向逐渐指向玩家。
    /// </summary>
    private IEnumerator AimPhase()
    {
        float elapsed = 0f;
        while (elapsed < aimTime)
        {
            if (player != null)
            {
                Vector2 dirToPlayer = ((Vector2)player.position - (Vector2)transform.position).normalized;
                float targetAngle = Mathf.Atan2(dirToPlayer.y, dirToPlayer.x) * Mathf.Rad2Deg;
                float currentAngle = transform.eulerAngles.z;
                float newAngle = Mathf.LerpAngle(currentAngle, targetAngle, aimLerpSpeed * Time.deltaTime);
                transform.rotation = Quaternion.Euler(0f, 0f, newAngle);
            }

            elapsed += Time.deltaTime;
            yield return null;
        }

        // 锁定飞行方向（贴图默认朝右，即 transform.right 为正面方向）
        flyDirection = transform.right;
    }

    /// <summary>
    /// 停顿阶段：不改变贴图方向，原地不动。
    /// </summary>
    private IEnumerator PausePhase()
    {
        yield return new WaitForSeconds(pauseTime);
    }

    /// <summary>
    /// 飞行阶段：沿既定方向匀速飞行，超过存活时间后自毁。
    /// </summary>
    private IEnumerator FlyPhase()
    {
        float elapsed = 0f;
        while (elapsed < flyLifetime)
        {
            transform.position += (Vector3)(flyDirection * flySpeed * Time.deltaTime);
            elapsed += Time.deltaTime;
            yield return null;
        }
    }

    private void GetPlayer()
    {
        if (GlobalPlayer.Instance == null || GlobalPlayer.Instance.Player == null)
        {
            Debug.Log("无法找到玩家对象，视为玩家已死亡。");
            return;
        }
        player = GlobalPlayer.Instance.Player.transform;
    }
}