using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[ExecuteAlways]
public class FollowFlipX : MonoBehaviour
{
    [Header("跟随设置")]
    [Tooltip("跟随目标")]
    [SerializeField] private SpriteRenderer target;
    [Tooltip("是否反向")]
    [SerializeField] private bool oppose;

    private SpriteRenderer _spriteRenderer;
    private float _baseLocalX;
    private bool _hasBaseLocalX;

    private void OnEnable()
    {
        _spriteRenderer = GetComponent<SpriteRenderer>();
        CaptureBaseLocalX();
        UpdateFlip();
    }

    private void OnValidate()
    {
        CaptureBaseLocalX();
        UpdateFlip();
    }

    private void Update()
    {
        UpdateFlip();
    }

    private void UpdateFlip()
    {
        if (_spriteRenderer == null || target == null)
        {
            return;
        }

        _spriteRenderer.flipX = oppose ? !target.flipX : target.flipX;
        var desiredFlip = _spriteRenderer.flipX;
        if (!_hasBaseLocalX || Mathf.Approximately(_baseLocalX, 0f))
        {
            return;
        }

        var localPos = transform.localPosition;
        localPos.x = _baseLocalX * (desiredFlip ? -1f : 1f);
        transform.localPosition = localPos;
    }

    private void CaptureBaseLocalX()
    {
        if (!_hasBaseLocalX)
        {
            _baseLocalX = transform.localPosition.x;
            _hasBaseLocalX = !Mathf.Approximately(_baseLocalX, 0f);
        }
    }
}
