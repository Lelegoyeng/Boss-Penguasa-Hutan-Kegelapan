using UnityEngine;
using System.Collections;

[RequireComponent(typeof(CharacterController))]
public class DashAttack : BossAttackBase
{
    [Header("Dash Settings")]
    public float dashSpeed = 18f;
    public float dashDistance = 10f;
    public float hitRadius = 1.2f;
    public LayerMask hitMask;
    [Tooltip("Opsional: VFX trail saat dash")] public GameObject dashTrailPrefab;
    private GameObject spawnedTrail;

    private CharacterController controller;

    private void Reset()
    {
        // Normal preset defaults
        attackName = "Dash";
        cooldown = 5f;
        windupTime = 0.5f;
        activeTime = 0.0f; // movement-driven
        recoveryTime = 0.6f;
        damage = 40f;
        dashSpeed = 18f;
        dashDistance = 10f;
        hitRadius = 1.2f;
    }

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
    }

    protected override IEnumerator OnWindup(Transform target)
    {
        yield return new WaitForSeconds(windupTime);
    }

    protected override IEnumerator OnActive(Transform target)
    {
        // Camera shake for the duration of the dash
        float estDuration = dashSpeed > 0.01f ? (dashDistance / dashSpeed) : 0.25f;
        if (CameraShakeService.Instance != null) CameraShakeService.Instance.Shake(estDuration, 0.18f);
        if (dashTrailPrefab && !spawnedTrail)
        {
            spawnedTrail = Instantiate(dashTrailPrefab, transform);
        }
        float travelled = 0f;
        Vector3 dir = target ? (target.position - transform.position).normalized : transform.forward;
        while (travelled < dashDistance)
        {
            float step = dashSpeed * Time.deltaTime;
            travelled += step;
            controller.Move(dir * step);
            // hit check
            Collider[] hits = Physics.OverlapSphere(transform.position, hitRadius, hitMask, QueryTriggerInteraction.Ignore);
            foreach (var h in hits)
            {
                if (h.TryGetComponent<IDamageable>(out var dmg))
                {
                    dmg.TakeDamage(damage);
                }
            }
            yield return null;
        }
        if (spawnedTrail)
        {
            Destroy(spawnedTrail);
            spawnedTrail = null;
        }
    }

    protected override IEnumerator OnRecovery(Transform target)
    {
        yield return new WaitForSeconds(recoveryTime);
    }

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0.9f, 0.1f, 0.9f, 0.25f);
        Gizmos.DrawWireSphere(transform.position, hitRadius);
        Vector3 dir = transform.forward;
        Gizmos.color = new Color(0.9f, 0.1f, 0.9f, 0.9f);
        Gizmos.DrawLine(transform.position, transform.position + dir * dashDistance);
    }
}
