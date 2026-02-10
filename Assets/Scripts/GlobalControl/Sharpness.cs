using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class Sharpness : Globalizer<Sharpness>
{
    [Header("锋利度设置")]
    [Tooltip("最大锋利度")]
    [SerializeField] private float maxSharpness = 100.0f;
    [Tooltip("锋利度每物理帧流失量")]
    [SerializeField] private float decreaseAmount = 0.14f;

    [Header("锋利条UI引用")]
    [Tooltip("锋利条外壳")]
    [SerializeField] private Image SharpnessBar;
    [Tooltip("锋利条填充")]
    [SerializeField] private Image SharpnessBarFilling;

    private AmountBar sharpnessBar;
    private bool active = false;

    protected override void GlobeInit()
    {
        sharpnessBar = new AmountBar(maxSharpness);
    }

    private void FixedUpdate()
    {
        if (active)
        {
            DecreaseSharpness(decreaseAmount);
        }
    }

    public void DecreaseSharpness(float amount)
    {
        sharpnessBar.Decrease(amount);
        AdjustSharpnessBar();
    }

    public void IncreaseSharpness(float amount)
    {
        sharpnessBar.Increase(amount);
        AdjustSharpnessBar();
    }

    public void FullSharpness()
    {
        sharpnessBar.CurrentAmount = sharpnessBar.MaxAmount;
        AdjustSharpnessBar();
    }

    public float SharpnessRate
    {
        get { return sharpnessBar.CurrentAmount / sharpnessBar.MaxAmount; }
    }

    public float GetDamageMultiplier()
    {
        var rawRate = sharpnessBar.MaxAmount <= 0f
            ? 0f
            : sharpnessBar.CurrentAmount / sharpnessBar.MaxAmount;

        var percent = Mathf.FloorToInt(Mathf.Clamp01(rawRate) * 100f);
        if (percent >= 50)
        {
            return 1f;
        }

        percent = Mathf.Clamp(percent, 0, 50);
        var multiplier = (25 + percent) / 100f;
        return Mathf.Max(0.25f, multiplier);
    }

    public void ActivateSharpnessBar(bool activity)
    {
        SharpnessBar.enabled = activity;
        SharpnessBarFilling.enabled = activity;
        active = activity;
    }

    private void AdjustSharpnessBar()
    {
        SharpnessBarFilling.fillAmount = SharpnessRate;
    }
}
