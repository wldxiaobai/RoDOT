using System;
using UnityEngine;

public class AmountBar
{
    private float maxAmount;
    private float currentAmount;

    public AmountBar(float maxAmount)
    {
        this.maxAmount = maxAmount;
        this.currentAmount = maxAmount;
    }

    public float CurrentAmount
    {
        get { return currentAmount; }
        set
        {
            currentAmount = value;
            if (currentAmount < 0)
            {
                currentAmount = 0;
            }
            else if (currentAmount > maxAmount)
            {
                currentAmount = maxAmount;
            }
        }
    }

    public float MaxAmount
    {
        get { return maxAmount; }
        set
        {
            maxAmount = value;
            if (currentAmount > maxAmount)
            {
                currentAmount = maxAmount;
            }
        }
    }

    public float Rate
    {
        get { return (currentAmount / maxAmount); }
        set
        {
            var v = Math.Clamp(value, 0f, 1f);
            currentAmount = MaxAmount * v;
        }
    }

    public AmountBar Increase(float amount)
    {
        CurrentAmount += amount;
        return this;
    }

    public AmountBar Decrease(float amount)
    {
        CurrentAmount -= amount;
        return this;
    }

    static public AmountBar operator +(AmountBar bar, float amount)
    {
        bar.currentAmount += amount;
        if (bar.currentAmount > bar.maxAmount)
        {
            bar.currentAmount = bar.maxAmount;
        }
        return bar;
    }

    static public AmountBar operator -(AmountBar bar, float amount)
    {
        bar.currentAmount -= amount;
        if (bar.currentAmount < 0)
        {
            bar.currentAmount = 0;
        }
        return bar;
    }
}
