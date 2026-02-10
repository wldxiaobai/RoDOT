using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FreezeFrameManager : Globalizer<FreezeFrameManager>
{
    [SerializeField] private float defaultFreezeDuration = 0.08f;
    private bool isFrozen = false;

    public void TriggerFreezeFrame(float duration = -1)
    {
        if (isFrozen) return;

        float freezeTime = duration > 0 ? duration : defaultFreezeDuration;
        StartCoroutine(FreezeFrameCoroutine(freezeTime));
    }

    private IEnumerator FreezeFrameCoroutine(float duration)
    {
        isFrozen = true;

        // 暂停所有可暂停的对象
        Time.timeScale = 0f;

        yield return new WaitForSecondsRealtime(duration);

        Time.timeScale = 1f;
        isFrozen = false;
    }
}
