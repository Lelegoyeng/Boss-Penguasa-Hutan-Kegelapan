using UnityEngine;
using System.Collections.Generic;

public class SpellBarUI : MonoBehaviour
{
    public GameObject spellSlotPrefab;
    public Transform container;

    private List<SpellSlotUI> _spellSlots = new List<SpellSlotUI>();

    public void CreateSpellSlots(List<Spell> spells)
    {
        // Clear existing slots
        foreach (Transform child in container)
        {
            Destroy(child.gameObject);
        }

        if (spellSlotPrefab == null)
        {
            Debug.LogError("[SpellBarUI] SpellSlot Prefab is not assigned!");
            return;
        }

        int totalSlots = 5; // Always create 5 slots
        Debug.Log($"[SpellBarUI] Creating {totalSlots} total spell slots.");

        for (int i = 0; i < totalSlots; i++)
        {
            GameObject slotGO = Instantiate(spellSlotPrefab, container);
            SpellSlotUI slotUI = slotGO.GetComponent<SpellSlotUI>();
            if (slotUI != null)
            {
                if (i < spells.Count)
                {
                    // This slot has a spell
                    slotUI.SetSpell(spells[i], i + 1);
                }
                else
                {
                    // This is an empty slot
                    slotUI.SetEmpty(i + 1);
                }
                _spellSlots.Add(slotUI);
            }
            else
            {
                Debug.LogError("[SpellBarUI] The prefab for spell slots does not have a SpellSlotUI component.");
            }
        }
    }

    public void UpdateHighlight(int activeIndex)
    {
        for (int i = 0; i < _spellSlots.Count; i++)
        {
            _spellSlots[i].SetHighlight(i == activeIndex);
        }
    }
}
