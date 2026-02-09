using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class FlagMovement : MonoBehaviour
{
    [SerializeField] private float liftHeight = 1.8f; // 旗帜提升的高度
    [SerializeField] private float liftDuration = 1.0f; // 提升动画的持续时间

    void Start()
    {
        StartCoroutine(FlagLiftCoroutine());
    }

    IEnumerator FlagLiftCoroutine()
    {
        Vector3 originalPosition = transform.position;
        Vector3 targetPosition = originalPosition + new Vector3(0, liftHeight, 0);
        float elapsedTime = 0f;
        while (elapsedTime < liftDuration)
        {
            transform.position = Vector3.Lerp(originalPosition, targetPosition, elapsedTime / liftDuration);
            elapsedTime += Time.deltaTime;
            yield return null;
        }
        transform.position = targetPosition; // 确保最终位置正确
    }
}
