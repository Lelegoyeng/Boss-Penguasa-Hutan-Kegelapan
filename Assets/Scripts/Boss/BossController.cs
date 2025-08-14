using UnityEngine;
using UnityEngine.AI;
using System.Collections;
using System.Linq;

[RequireComponent(typeof(NavMeshAgent))]
[RequireComponent(typeof(BossHealth))]
public class BossController : MonoBehaviour
{
    public enum BossState { Idle, Chase, Attack, Stagger, PhaseTransition, Dead }

    [Header("References")]
    public Transform player;
    public BossHealth health;

    [Header("Movement")]
    public float detectRange = 20f;
    public float attackRange = 4f;
    public float chaseStopDistance = 2.5f;

    [Header("AI")]
    public float thinkInterval = 0.2f;
    public float staggerDuration = 0.6f;

    private NavMeshAgent agent;
    private BossAttackBase[] attacks;
    private BossState state = BossState.Idle;
    private bool phase2;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        health = health ? health : GetComponent<BossHealth>();
        attacks = GetComponentsInChildren<BossAttackBase>(true);
    }

    private void OnEnable()
    {
        if (health != null)
        {
            health.OnHalfHealth += EnterPhase2;
            health.OnDeath += OnDeath;
        }
        StartCoroutine(ThinkLoop());
    }

    private void OnDisable()
    {
        if (health != null)
        {
            health.OnHalfHealth -= EnterPhase2;
            health.OnDeath -= OnDeath;
        }
        StopAllCoroutines();
    }

    IEnumerator ThinkLoop()
    {
        while (state != BossState.Dead)
        {
            switch (state)
            {
                case BossState.Idle:
                    HandleIdle();
                    break;
                case BossState.Chase:
                    HandleChase();
                    break;
                case BossState.Attack:
                    // handled by attack coroutines
                    break;
            }
            yield return new WaitForSeconds(thinkInterval);
        }
    }

    void HandleIdle()
    {
        if (!player) FindPlayer();
        if (player && Vector3.Distance(transform.position, player.position) <= detectRange)
        {
            state = BossState.Chase;
        }
    }

    void HandleChase()
    {
        if (!player) { state = BossState.Idle; return; }
        float dist = Vector3.Distance(transform.position, player.position);
        if (dist > attackRange)
        {
            agent.isStopped = false;
            agent.stoppingDistance = chaseStopDistance;
            agent.SetDestination(player.position);
        }
        else
        {
            agent.isStopped = true;
            TryAttack();
        }
    }

    void TryAttack()
    {
        var ready = attacks.Where(a => a.enabled && a.CanUse()).OrderBy(a => Random.value).FirstOrDefault();
        if (ready != null)
        {
            state = BossState.Attack;
            StartCoroutine(DoAttack(ready));
        }
    }

    IEnumerator DoAttack(BossAttackBase attack)
    {
        yield return attack.ExecuteAttackRoutine(player);
        if (state != BossState.Dead)
        {
            state = BossState.Chase;
        }
    }

    void EnterPhase2()
    {
        if (phase2) return;
        phase2 = true;
        // make boss more aggressive
        attackRange += 1f;
        foreach (var a in attacks)
        {
            a.cooldown = Mathf.Max(0.4f, a.cooldown * 0.7f);
            a.damage *= 1.25f;
        }
        StartCoroutine(PhaseTransitionRoutine());
    }

    IEnumerator PhaseTransitionRoutine()
    {
        state = BossState.PhaseTransition;
        yield return new WaitForSeconds(1.2f);
        state = BossState.Chase;
    }

    void OnDeath()
    {
        state = BossState.Dead;
        agent.isStopped = true;
        // optional: disable colliders/attacks
        foreach (var a in attacks) a.enabled = false;
    }

    void FindPlayer()
    {
        var p = GameObject.FindGameObjectWithTag("Player");
        if (p) player = p.transform;
    }
}
