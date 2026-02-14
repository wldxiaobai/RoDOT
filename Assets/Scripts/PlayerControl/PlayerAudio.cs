using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerAudio : MonoBehaviour
{
    [Header("音效设置")]
    [Range(0f, 1f)]
    [SerializeField] private float volume = 1f; // 音量控制
    [SerializeField] private AudioClip jumpAudio; // 跳跃音效
    [SerializeField] private AudioClip landAudio; // 落地音效
    [SerializeField] private AudioClip attackAudio; // 攻击音效
    [SerializeField] private AudioClip hurtAudio; // 受伤音效
    [SerializeField] private AudioClip deathAudio; // 死亡音效
    [SerializeField] private AudioClip walkAudio; // 行走音效
    [SerializeField] private AudioClip dashAudio; // 冲刺音效
    [SerializeField] private AudioClip defendedAudio; // 防御音效
    [SerializeField] private AudioClip parryAudio; // 格挡音效
    [SerializeField] private AudioClip RespawnAudio; // 重生音效

    public void PlayJumpAudio()
    {
        PlayClip(jumpAudio);
    }

    public void PlayLandAudio()
    {
        PlayClip(landAudio);
    }

    public void PlayAttackAudio()
    {
        PlayClip(attackAudio);
    }

    public void PlayHurtAudio()
    {
        PlayClip(hurtAudio);
    }

    public void PlayDeathAudio()
    {
        PlayClip(deathAudio);
    }

    public void PlayWalkAudio()
    {
        PlayClip(walkAudio);
    }

    public void PlayDashAudio()
    {
        PlayClip(dashAudio);
    }

    public void PlayDefendedAudio()
    {
        PlayClip(defendedAudio);
    }

    public void PlayParryAudio()
    {
        PlayClip(parryAudio);
    }

    public void PlayRespawnAudio()
    {
        PlayClip(RespawnAudio);
    }

    private void PlayClip(AudioClip clip)
    {
        if (clip == null)
        {
            Debug.LogWarning("PlayerAudio: clip is not assigned.", this);
            return;
        }

        AudioManager.PlaySound(clip, transform.position, volume);
    }
}
