using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using static AttackHitInfo;

public class BlockInPhase1 : MonoBehaviour
{
    [Header("抵御效果设置")]
    [Tooltip("抵御时的震屏强度")]
    [SerializeField] private float mag;
    [Tooltip("抵御时的震屏时间")]
    [SerializeField] private float duration;
    [Tooltip("抵御时的音效")]
    [SerializeField] private AudioClip blockSFX;
    [Tooltip("音量")]
    [SerializeField] [Range(0f, 1f)] private float blockSFXVolume = 0.6f;
    [Tooltip("冻结帧时长")]
    [SerializeField] private float freezeFrameDuration = 0.1f;
    [Tooltip("是否启用抵御")]
    [SerializeField] private bool isActived;

    private Rigidbody2D blockRigidbody;

    public bool IsActived
    {
        get => isActived;
        set
        {
            isActived = value;
            if (value)
            {
                EnableBlock();
            }
            else
            {
                DisableBlock();
            }
        }
    }

    private void Awake()
    {
        if (isActived)
        {
            EnableBlock();
        }
    }

    private void EnableBlock()
    {
        if (blockRigidbody == null)
        {
            blockRigidbody = gameObject.AddComponent<Rigidbody2D>();
            blockRigidbody.bodyType = RigidbodyType2D.Kinematic;
        }
    }

    private void DisableBlock()
    {
        if (blockRigidbody != null)
        {
            Destroy(blockRigidbody);
            blockRigidbody = null;
        }
    }

    private void HandleBlock(GameObject other)
    {
        if (!isActived) return;

        if (other.TryGetComponent<AttackHitInfo>(out var hitInfo))
        {
            if (hitInfo.GetHitResult(gameObject) != HitResult.None ||
                hitInfo.AttackPosition == Position.Hostile ||
                hitInfo.AttackPosition == Position.None)
                return;

            hitInfo.RecordHitObject(gameObject);

            if (CameraShakeManager.IsInitialized)
            {
                CameraShakeManager.Instance.ShakeStraight(Vector2.right, duration, mag);
            }

            FreezeFrameManager.Instance.TriggerFreezeFrame(freezeFrameDuration);
            AudioManager.PlaySound(blockSFX, transform.position, blockSFXVolume);
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        HandleBlock(collision.gameObject);
    }

    private void OnTriggerEnter2D(Collider2D collider)
    {
        HandleBlock(collider.gameObject);
    }
}
