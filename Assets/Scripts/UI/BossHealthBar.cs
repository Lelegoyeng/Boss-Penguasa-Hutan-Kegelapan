using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class BossHealthBar : MonoBehaviour
{
    public BossHealth bossHealth;
    public Slider slider;
    public TextMeshProUGUI nameText;

    private void Start()
    {
        if (bossHealth)
        {
            nameText.text = bossHealth.bossName;
            slider.maxValue = bossHealth.maxHealth;
            slider.value = bossHealth.CurrentHealth;
            bossHealth.OnHealthChanged += OnHealthChanged;
        }
        else
        {
            gameObject.SetActive(false);
        }
    }

    private void OnDestroy()
    {
        if (bossHealth)
        {
            bossHealth.OnHealthChanged -= OnHealthChanged;
        }
    }

    void OnHealthChanged(float current, float max)
    {
        slider.maxValue = max;
        slider.value = current;
        if (current <= 0f) gameObject.SetActive(false);
    }
}
