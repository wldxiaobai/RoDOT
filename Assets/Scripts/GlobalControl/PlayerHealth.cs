using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerHealth : Globalizer<PlayerHealth>
{
    [Tooltip("最大生命值")]
    [SerializeField] private int maxHealth = 100;

    private AmountBar healthBar;

    protected override void GlobeInit()
    {
        healthBar = new AmountBar(maxHealth);
    }

    public void TakeDamage(int damage)
    {
        healthBar.Decrease(damage);
    }

    public void Heal(int amount)
    {
        healthBar.Increase(amount);
    }

    public void SetMaxHealth(int newMaxHealth)
    {
        healthBar.MaxAmount = newMaxHealth;
    }

    public void FullHeal()
    {
        healthBar.CurrentAmount = healthBar.MaxAmount;
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
}
