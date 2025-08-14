using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(CharacterController))]
public class PlayerHealth : MonoBehaviour, IDamageable
{
    public float maxHealth = 200f;
    public UnityEvent onDeath;
    public UnityEvent<float, float> onHealthChanged; // current, max

    public float CurrentHealth { get; private set; }

    void Awake()
    {
        CurrentHealth = maxHealth;
        gameObject.tag = "Player"; // ensure tagged as Player
    }

    public void TakeDamage(float amount)
    {
        if (CurrentHealth <= 0f) return;
        CurrentHealth = Mathf.Max(0f, CurrentHealth - amount);
        onHealthChanged?.Invoke(CurrentHealth, maxHealth);
        if (CurrentHealth <= 0f)
        {
            onDeath?.Invoke();
        }
    }
}
