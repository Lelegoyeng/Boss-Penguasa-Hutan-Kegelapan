using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NavMeshAgent))]
public class DragonChaseAI : MonoBehaviour
{
    public Transform target;
    public float detectRange = 50f;
    public float stopDistance = 6f;
    public float repathInterval = 0.25f;

    private NavMeshAgent _agent;
    private float _timer;

    void Awake()
    {
        _agent = GetComponent<NavMeshAgent>();
        _agent.stoppingDistance = stopDistance;
        _agent.autoBraking = true;
    }

    void Start()
    {
        if (!target)
        {
            var player = GameObject.Find("Player_Wizard");
            if (player) target = player.transform;
        }
    }

    void Update()
    {
        if (!target) return;
        if (!_agent.isOnNavMesh) return;

        _timer += Time.deltaTime;
        if (_timer < repathInterval) return;
        _timer = 0f;

        float dist = Vector3.Distance(transform.position, target.position);
        if (dist <= detectRange)
        {
            if (!_agent.pathPending)
            {
                _agent.stoppingDistance = stopDistance;
                _agent.SetDestination(target.position);
            }
        }
    }
}
