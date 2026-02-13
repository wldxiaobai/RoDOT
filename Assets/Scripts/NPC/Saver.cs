using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Saver : MonoBehaviour
{
    [Header("存档设置")]
    [Tooltip("存档的玩家位置相对存档点的偏移")]
    [SerializeField] private Vector2 offset = Vector2.zero;
    [Tooltip("存档点唯一签名（不同存档点必须设置不同的签名）")]
    [SerializeField] private string uniqueSignature = "Saver0";

    [Header("视觉效果设置")]
    [Tooltip("存档点未激活时的贴图")]
    [SerializeField] private Sprite inactiveSaver;
    [Tooltip("存档点激活时的贴图")]
    [SerializeField] private Sprite activeSaver;
    [Tooltip("旗帜物体")]
    [SerializeField] private GameObject flagPerfab;

    [Header("音频设置")]
    [Range(0f, 1f)]
    [Tooltip("磨刀音效音量")]
    [SerializeField] private float sharpnessSoundVolume = 0.8f;
    [Tooltip("磨刀音效")]
    [SerializeField] private AudioClip sharpnessSound;

    private SpriteRenderer spriteRenderer;
    private Material material;
    private string currentSceneName;
    private bool isActivated = false;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        material = spriteRenderer.material;
        currentSceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
        SetSaverActivate(
            LevelInfoDict.GetState(
                (currentSceneName + "_" + uniqueSignature), 0 // 读取存档点状态，默认为0（未激活）
                ) != 0
            );
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.TryGetComponent<AttackHitInfo>(out var hitInfo))
        {
            // 只有当攻击来自友方且未命中过该存档点时才触发存档逻辑
            if (hitInfo.GetHitResult(gameObject) != HitResult.None ||
                hitInfo.AttackPosition != Position.Friendly)
                return;

            Sharpness.Instance.FullSharpness(); // 恢复锐利度
            if (!isActivated)
            {
                PlayerHealth.Instance.FullHeal(); // 恢复生命值
            }
            AudioManager.PlaySound(sharpnessSound, transform.position, sharpnessSoundVolume); // 播放磨刀音效
            var incoming = hitInfo.GetHitInfo();
            if (incoming.IsValid)
            {
                hitInfo.RecordHitObject(gameObject);
                SaverOnHit();
            }
        }
    }

    // 存档逻辑：保存玩家位置和当前场景名称，并更新存档点状态
    private void SaverOnHit()
    {
        if (!isActivated)
        {
            // 只有当存档点未激活时才进行存档操作
            if(SaveManeger.Instance == null || SaveManeger.Instance.Data == null)
            {
                Debug.LogWarning("SaveManeger or SaveData is null. Cannot save player position.");
                return;
            }
            SaveManeger.Instance.Data.PlayerPosition = (Vector2)transform.position + offset;
            SaveManeger.Instance.Data.SceneName = currentSceneName;
            LevelInfoDict.SetState(
                currentSceneName + "_" + uniqueSignature,
                1);
            SaveManeger.Instance.DataSave();
            SetSaverActivate(true);
        }
    }

    // 更新存档点状态和贴图
    private void SetSaverActivate(bool active)
    {
        // 只有当存档点从未激活变为激活时才生成旗帜，避免重复生成
        if (!isActivated && active && flagPerfab != null)
        {
            SpawnFlag();
        }

        // 更新存档点状态和贴图
        isActivated = active;
        spriteRenderer.sprite = active? activeSaver : inactiveSaver;
    }

    private void SpawnFlag()
    {
        // 生成旗帜物体，位置与存档点相同，旋转为默认
        Instantiate(flagPerfab, ( transform.position + new Vector3(0f, -1.8f, -1f) ), Quaternion.identity);
    }
}
