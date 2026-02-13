using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CameraShakeManager : Globalizer<CameraShakeManager>
{
    [SerializeField] private string cameraTag = "MainCamera";

    private Transform cameraTrans;
    private Coroutine straightShakeCoroutine;
    private Coroutine rotateShakeCoroutine;
    private Vector3 straightShakeRestPosition;
    private Quaternion rotateShakeRestRotation;

    public void ShakeStraight(Vector2 dir, float duration, float magnitude)
    {
        cameraTrans = GameObject.FindGameObjectWithTag(cameraTag).transform;
        if (cameraTrans == null || duration <= 0f || magnitude <= 0f)
        {
            return;
        }

        if (straightShakeCoroutine != null)
        {
            StopCoroutine(straightShakeCoroutine);
            cameraTrans.localPosition = straightShakeRestPosition;
        }

        straightShakeCoroutine = StartCoroutine(ShakeStraightRoutine(dir, duration, magnitude));
    }

    public void ShakeRotate(float duration, float magnitude)
    {
        cameraTrans = GameObject.FindGameObjectWithTag(cameraTag).transform;
        if (cameraTrans == null || duration <= 0f || magnitude <= 0f)
        {
            return;
        }

        if (rotateShakeCoroutine != null)
        {
            StopCoroutine(rotateShakeCoroutine);
            cameraTrans.localRotation = rotateShakeRestRotation;
        }

        rotateShakeCoroutine = StartCoroutine(ShakeRotateRoutine(duration, magnitude));
    }

    private IEnumerator ShakeStraightRoutine(Vector2 direction, float duration, float magnitude)
    {
        straightShakeRestPosition = cameraTrans.localPosition;
        var normalizedDir = direction == Vector2.zero ? Vector2.up : direction.normalized;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            var progress = Mathf.Clamp01(elapsed / duration);
            var decay = 1f - progress;
            var offset = Mathf.Sin(progress * Mathf.PI * 2f) * magnitude * decay;
            cameraTrans.localPosition = straightShakeRestPosition + (Vector3)(normalizedDir * offset);
            yield return null;
        }

        cameraTrans.localPosition = straightShakeRestPosition;
        straightShakeCoroutine = null;
    }

    private IEnumerator ShakeRotateRoutine(float duration, float magnitude)
    {
        rotateShakeRestRotation = cameraTrans.localRotation;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            var progress = Mathf.Clamp01(elapsed / duration);
            var decay = 1f - progress;
            var angle = Mathf.Sin(progress * Mathf.PI * 2f) * magnitude * decay;
            cameraTrans.localRotation = rotateShakeRestRotation * Quaternion.Euler(0f, 0f, angle);
            yield return null;
        }

        cameraTrans.localRotation = rotateShakeRestRotation;
        rotateShakeCoroutine = null;
    }
}
