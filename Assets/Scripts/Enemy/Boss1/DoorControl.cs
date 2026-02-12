using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class DoorControl : MonoBehaviour
{
    [Header("材质与动画机引用")]
    public SpriteRenderer Door_Back;
    public SpriteRenderer Door_Forward;
    public Animator Anim_Back;
    public Animator Anim_Forward;

    [Header("参数设置")]
    public string openParam = "open";
    [Range(0f,1f)] public float colorCoverRate = 0.5f;
    public float flashDuration = 1f; 
    public Color flashColor = Color.white;

    private Coroutine _flashCoroutine;

    // 闪烁特效
    public void DoorFlashEffect()
    {
        _flashCoroutine = StartCoroutine(FlashEffectCoroutine(flashDuration, flashColor));
    }

    IEnumerator FlashEffectCoroutine(float duration, Color color)
    {
        float elapsed = 0f;
        var m1 = Door_Back.material; var m2 = Door_Forward.material;
        m1.SetColor("_flashColor", color);
        m2.SetColor("_flashColor", color);
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            m1.SetFloat("_flashFactor", Mathf.Lerp(colorCoverRate, 0f, elapsed / duration));
            m2.SetFloat("_flashFactor", Mathf.Lerp(colorCoverRate, 0f, elapsed / duration));
            yield return null;
        }
    }

    public void StopDoorFlashEffect()
    {
        if (_flashCoroutine != null)
        {
            StopCoroutine(_flashCoroutine);
            _flashCoroutine = null;
        }
        var m1 = Door_Back.material; var m2 = Door_Forward.material;
        m1.SetFloat("_flashFactor", 0f);
        m2.SetFloat("_flashFactor", 0f);
    }

    // 触发动画
    public void OpenDoor() { Anim_Back.SetTrigger(openParam); Anim_Forward.SetTrigger(openParam); }
}
