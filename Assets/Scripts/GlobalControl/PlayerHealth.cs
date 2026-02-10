using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class PlayerHealth : Globalizer<PlayerHealth>
{
    [Header("生命值设置")]
    [Tooltip("最大生命值")]
    [SerializeField] private int maxHealth = 40;

    [Header("血条UI引用")]
    [Tooltip("血条外壳")]
    [SerializeField] private Image HealthBar;
    [Tooltip("血条填充")]
    [SerializeField] private Image HealthBarFilling;

    private AmountBar healthBar;

    protected override void GlobeInit()
    {
        healthBar = new AmountBar(maxHealth);
    }

    public void TakeDamage(int damage)
    {
        healthBar.Decrease(damage);
        AdjustHealthBar();
    }

    public void Heal(int amount)
    {
        healthBar.Increase(amount);
        AdjustHealthBar();
    }

    public void SetMaxHealth(int newMaxHealth)
    {
        healthBar.MaxAmount = newMaxHealth;
        AdjustHealthBar();
    }

    public void FullHeal()
    {
        healthBar.CurrentAmount = healthBar.MaxAmount;
        AdjustHealthBar();
    }

    public int MaxHealth
    {
        get { return (int)healthBar.MaxAmount; }
    }

    public int CurrentHealth
    {
        get { return (int)healthBar.CurrentAmount; }
    }

    public float HealthRate
    {
        get { return healthBar.Rate; }
    }

    public void ActivateHealthBar(bool activity)
    {
        HealthBar.enabled = activity;
        HealthBarFilling.enabled = activity;
    }

    private void AdjustHealthBar()
    {
        HealthBarFilling.fillAmount = HealthRate;
    }
}
