using UnityEngine;

public class WeaponHitbox : MonoBehaviour
{
    public GameObject owner;
    public float damage = 10f;

    void OnTriggerEnter(Collider other)
    {
        if (!enabled) return;
        if (!owner) return;
        if (other.attachedRigidbody && other.attachedRigidbody.gameObject == owner) return;
        if (other.gameObject == owner) return;

        // Cari BossHealth atau komponen health lain
        var bossHealth = other.GetComponentInParent<BossHealth>();
        if (bossHealth)
        {
            // Efek hit
            CameraSimpleShake.Shake(0.25f);
            HitStop.Do(0.06f);
            bossHealth.TakeDamage(damage);
            Debug.Log($"[WeaponHitbox] Hit {other.name}, damage={damage}");
            return;
        }

        // TODO: dukungan Damageable umum jika diperlukan
    }
}
