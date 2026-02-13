using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BossLightningFlashEffect : MonoBehaviour
{
    [Header("闪烁特效设置")]
    [Tooltip("闪烁颜色")]
    [SerializeField] private Color flashColor = Color.white;
    [Tooltip("闪烁颜色覆盖率（中位数）")]
    [SerializeField] [Range(0f, 1f)] private float flashColorCoverage = 0.5f;
    [Tooltip("闪烁颜色覆盖率的变化幅度")]
    [SerializeField] [Range(0f, 1f)] private float flashColorCoverageVariation = 0.2f;
    [Tooltip("是否开启闪烁")]
    [SerializeField] private bool enableFlash = true;

    private SpriteRenderer spriteRenderer;
    private Material material;

    public bool EnableFlash
    {
        get => enableFlash;
        set
        {
            enableFlash = value;
            if (material == null) return;
            if (value)
            {
                SetColor(flashColor);
            }
            else
            {
                SetColorCoverage(0f);
            }
        }
    }

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        material = spriteRenderer.material;
        if (enableFlash)
        {
            SetColor(flashColor);
        }
    }

    private void Update()
    {
        if (enableFlash)
        {
            float min = Mathf.Clamp01(flashColorCoverage - flashColorCoverageVariation);
            float max = Mathf.Clamp01(flashColorCoverage + flashColorCoverageVariation);
            SetColorCoverage(Random.Range(min, max));
        }
    }

    private void SetColorCoverage(float c)
    {
        material.SetFloat("_flashFactor", c);
    }

    private void SetColor(Color c)
    {
        material.SetColor("_flashColor", c);
    }
}
