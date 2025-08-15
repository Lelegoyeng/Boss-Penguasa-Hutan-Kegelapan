using UnityEngine;
using UnityEngine.UI;

public class SpellSlotUI : MonoBehaviour
{
    public Image iconImage;
    public Image highlightImage;
    public Text keyText;

    public void SetSpell(Spell spell, int keyNumber)
    {
        if (spell != null)
        {
            Debug.Log($"[SpellSlotUI] Setting spell '{spell.spellName}' with icon: {spell.icon != null}");
            iconImage.sprite = spell.icon;
            iconImage.enabled = true;
            keyText.text = keyNumber.ToString();
            GetComponent<Image>().color = new Color(0.2f, 0.2f, 0.2f, 0.5f); // Active background
        }
        else
        {
            SetEmpty(keyNumber);
        }
    }

    public void SetEmpty(int keyNumber)
    {
        iconImage.enabled = false;
        keyText.text = keyNumber.ToString();
        highlightImage.enabled = false;
        GetComponent<Image>().color = new Color(0.1f, 0.1f, 0.1f, 0.25f); // Inactive background
    }

    public void SetHighlight(bool isHighlighted)
    {
        if (highlightImage != null)
        {
            highlightImage.enabled = isHighlighted;
        }
    }
}
