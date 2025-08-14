using UnityEngine;
using System.Collections;

public class CleaveAttack : BossAttackBase
{
    [Header("Cleave Settings")]
    public float range = 3.5f;
    public float angle = 100f; // degrees
    public LayerMask hitMask;
    public Transform attackOrigin;
    public ParticleSystem vfx;
    [Tooltip("Opsional: VFX yang di-spawn di titik kena")] public GameObject hitVfxPrefab;

    private void Reset()
    {
        // Normal preset defaults
        attackName = "Cleave";
        cooldown = 3f;
        windupTime = 0.6f;
        activeTime = 0.2f;
        recoveryTime = 0.5f;
        damage = 35f;
        range = 3.5f;
        angle = 100f;
    }

    protected override IEnumerator OnWindup(Transform target)
    {
        float t = 0f;
        while (t < windupTime)
        {
            t += Time.deltaTime;
            // optional: face target
            if (target) transform.forward = Vector3.Lerp(transform.forward, (target.position - transform.position).normalized, t / windupTime);
            yield return null;
        }
    }

    protected override IEnumerator OnActive(Transform target)
    {
        if (vfx) vfx.Play();
        if (CameraShakeService.Instance != null) CameraShakeService.Instance.Shake(0.2f, 0.22f);
        // Hit in cone
        Vector3 origin = attackOrigin ? attackOrigin.position : transform.position + transform.forward;
        Collider[] hits = Physics.OverlapSphere(origin, range, hitMask, QueryTriggerInteraction.Ignore);
        foreach (var h in hits)
        {
            Vector3 dir = (h.transform.position - transform.position).normalized;
            float a = Vector3.Angle(transform.forward, dir);
            if (a <= angle * 0.5f)
            {
                if (h.TryGetComponent<IDamageable>(out var dmg))
                {
                    dmg.TakeDamage(damage);
                    if (hitVfxPrefab)
                    {
                        Vector3 hitPos = h.ClosestPoint(origin);
                        Object.Instantiate(hitVfxPrefab, hitPos, Quaternion.identity);
                    }
                }
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
        Gizmos.color = new Color(1f, 0.4f, 0.1f, 0.25f);
        Vector3 o = attackOrigin ? attackOrigin.position : transform.position + transform.forward;
        Gizmos.DrawWireSphere(o, range);

        // Draw cone edges
        Vector3 forward = transform.forward;
        Quaternion leftRot = Quaternion.AngleAxis(-angle * 0.5f, Vector3.up);
        Quaternion rightRot = Quaternion.AngleAxis(angle * 0.5f, Vector3.up);
        Vector3 left = leftRot * forward;
        Vector3 right = rightRot * forward;
        Gizmos.color = new Color(1f, 0.4f, 0.1f, 0.9f);
        Gizmos.DrawLine(transform.position, transform.position + left * range);
        Gizmos.DrawLine(transform.position, transform.position + right * range);
    }
}
