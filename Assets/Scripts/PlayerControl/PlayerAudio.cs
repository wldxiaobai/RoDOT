using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerAudio : MonoBehaviour
{
    [Header("音效设置")]
    [SerializeField] private AudioClip jumpAudio; // 跳跃音效
    [SerializeField] private AudioClip landAudio; // 落地音效
    [SerializeField] private AudioClip attackAudio; // 攻击音效
    [SerializeField] private AudioClip hurtAudio; // 受伤音效
    [SerializeField] private AudioClip deathAudio; // 死亡音效
    [SerializeField] private AudioClip walkAudio; // 行走音效
    [SerializeField] private AudioClip dashAudio; // 冲刺音效
    [SerializeField] private AudioClip defendedAudio; // 防御音效
    [SerializeField] private AudioClip parryAudio; // 格挡音效

    public void PlayJumpAudio()
    {
        AudioSource.PlayClipAtPoint(jumpAudio, transform.position);
    }

    public void PlayLandAudio()
    {
        AudioSource.PlayClipAtPoint(landAudio, transform.position);
    }

    public void PlayAttackAudio()
    {
        AudioSource.PlayClipAtPoint(attackAudio, transform.position);
    }

    public void PlayHurtAudio()
    {
        AudioSource.PlayClipAtPoint(hurtAudio, transform.position);
    }

    public void PlayDeathAudio()
    {
        AudioSource.PlayClipAtPoint(deathAudio, transform.position);
    }

    public void PlayWalkAudio()
    {
        AudioSource.PlayClipAtPoint(walkAudio, transform.position);
    }

    public void PlayDashAudio()
    {
        AudioSource.PlayClipAtPoint(dashAudio, transform.position);
    }

    public void PlayDefendedAudio()
    {
        AudioSource.PlayClipAtPoint(defendedAudio, transform.position);
    }

    public void PlayParryAudio()
    {
        AudioSource.PlayClipAtPoint(parryAudio, transform.position);
    }
}
