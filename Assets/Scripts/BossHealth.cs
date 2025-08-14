using UnityEngine;
using System;

public class BossHealth : MonoBehaviour
{
    [Header("Health Settings")]
    public string bossName = "Lele Troll Goyeng";
    public float maxHealth = 1000f;

    [Tooltip("True saat boss mati")] public bool isDead;
    public float CurrentHealth { get; private set; }

    public event Action<float, float> OnHealthChanged; // current, max
    public event Action OnDeath;
    public event Action OnHalfHealth; // trigger phase change

    private bool halfHealthTriggered = false;

    private void Awake()
    {
        CurrentHealth = maxHealth;
        isDead = false;
    }

    public void ResetHealth()
    {
        CurrentHealth = maxHealth;
        isDead = false;
        halfHealthTriggered = false;
        OnHealthChanged?.Invoke(CurrentHealth, maxHealth);
    }

    public void TakeDamage(float amount)
    {
        if (isDead || amount <= 0f) return;
        CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);
        OnHealthChanged?.Invoke(CurrentHealth, maxHealth);

        if (!halfHealthTriggered && CurrentHealth <= maxHealth * 0.5f)
        {
            halfHealthTriggered = true;
            OnHalfHealth?.Invoke();
        }

        if (CurrentHealth <= 0f)
        {
            isDead = true;
            OnDeath?.Invoke();
        }
    }
}
