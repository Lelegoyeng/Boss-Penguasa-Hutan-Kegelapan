using UnityEngine;
using System.Collections;

public class SlamAttack : BossAttackBase
{
    [Header("Slam Settings")]
    public float radius = 4f;
    public LayerMask hitMask;
    public Transform slamPoint;
    public ParticleSystem slamVfx;
    public float shockwaveForce = 8f;
    [Tooltip("Opsional: VFX impact yang di-spawn di pusat slam")] public GameObject slamImpactVfxPrefab;

    private void Reset()
    {
        // Normal preset defaults
        attackName = "Slam";
        cooldown = 4f;
        windupTime = 0.8f;
        activeTime = 0.2f;
        recoveryTime = 0.6f;
        damage = 45f;
        radius = 4f;
        shockwaveForce = 8f;
    }

    protected override IEnumerator OnWindup(Transform target)
    {
        yield return new WaitForSeconds(windupTime);
    }

    protected override IEnumerator OnActive(Transform target)
    {
        if (slamVfx) slamVfx.Play();
        if (CameraShakeService.Instance != null) CameraShakeService.Instance.Shake(0.35f, 0.35f);
        Vector3 center = slamPoint ? slamPoint.position : transform.position;
        if (slamImpactVfxPrefab) Object.Instantiate(slamImpactVfxPrefab, center, Quaternion.identity);
        Collider[] hits = Physics.OverlapSphere(center, radius, hitMask, QueryTriggerInteraction.Ignore);
        foreach (var h in hits)
        {
            if (h.TryGetComponent<IDamageable>(out var dmg))
            {
                dmg.TakeDamage(damage);
            }
            if (h.attachedRigidbody)
            {
                Vector3 dir = (h.transform.position - center).normalized;
                h.attachedRigidbody.AddForce(dir * shockwaveForce, ForceMode.Impulse);
            }
        }
        yield return new WaitForSeconds(activeTime);
    }

    protected override IEnumerator OnRecovery(Transform target)
    {
        yield return new WaitForSeconds(recoveryTime);
    }

    private void OnDrawGizmosSelected()
    {
        Vector3 center = slamPoint ? slamPoint.position : transform.position;
        Gizmos.color = new Color(0.1f, 0.6f, 1f, 0.2f);
        Gizmos.DrawSphere(center, 0.15f);
        Gizmos.DrawWireSphere(center, radius);
    }
}
