using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerHealth : Globalizer<PlayerHealth>
{
    [Tooltip("最大生命值")]
    [SerializeField] private int maxHealth = 100;
    [Tooltip("是否开启调试功能")]
    [SerializeField] bool debugMode = false;

    private int currentHealth;

    protected override void Awake()
    {
        base.Awake();
        currentHealth = maxHealth;
    }

    public void TakeDamage(int damage)
    {
        currentHealth -= damage;
        currentHealth = Mathf.Max(currentHealth, 0);
    }

    public void Heal(int amount)
    {
        currentHealth += amount;
        currentHealth = Mathf.Min(currentHealth, maxHealth);
    }

    private void Update()
    {
        if (debugMode)
        {
            if (Input.GetKeyDown(KeyCode.Equals))
            {
                Heal(10);
            }
            if (Input.GetKeyDown(KeyCode.Minus))
            {
                TakeDamage(10);
            }
        }

    }
}
