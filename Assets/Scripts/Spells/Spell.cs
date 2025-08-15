using UnityEngine;

[CreateAssetMenu(fileName = "New Spell", menuName = "Spells/Spell")]
public class Spell : ScriptableObject
{
    [Header("UI")]
    public Sprite icon; // Ikon untuk ditampilkan di UI
    [Header("General")]
    public string spellName;
    public float cooldown = 0.5f;

    [Header("Projectile")]
    public GameObject projectilePrefab;
    public float speed = 20f;
    public float lifeTime = 3f;
    public float projectileScale = 1f; // Skala proyektil
    public AudioClip castSound; // Suara saat sihir ditembakkan

    [Header("Impact")]
    public GameObject hitEffect; // Efek visual saat mengenai target
    public float hitEffectScale = 1f; // Skala efek ledakan
    public AudioClip hitSound; // Suara saat mengenai target
}
