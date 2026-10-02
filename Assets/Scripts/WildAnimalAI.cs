using Unity.Netcode;
using UnityEngine;
using UnityEngine.AI;

[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(NetworkTransform))]
[RequireComponent(typeof(NavMeshAgent))]
public sealed class WildAnimalAI : NetworkBehaviour
{
    [Header("AI")]
    [SerializeField] private float detectionRadius = 45f;
    [SerializeField] private float attackRange = 2.2f;
    [SerializeField] private float attackCooldown = 2f;
    [SerializeField] private float damagePerAttack = 12f;
    [SerializeField] private float targetRefreshInterval = 0.5f;
    [SerializeField] private string attackTrigger = "Attack";
    [SerializeField] private Animator animator;
    [SerializeField] private AudioSource attackAudio;

    public NetworkVariable<bool> IsAggressive = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private DayNightCycleManager cycleManager;
    private NetworkMatchManager matchManager;
    private NavMeshAgent agent;
    private NetworkPlayer target;
    private float targetRefreshTimer;
    private float attackTimer;

    private void Awake()
    {
        agent = GetComponent<NavMeshAgent>();
        matchManager = FindObjectOfType<NetworkMatchManager>();
    }

    public void InitializeServer(DayNightCycleManager manager)
    {
        if (!IsServer)
        {
            return;
        }

        cycleManager = manager;
        IsAggressive.Value = true;
    }

    public override void OnNetworkSpawn()
    {
        if (!IsServer && agent != null)
        {
            agent.enabled = false;
        }
    }

    private void Update()
    {
        if (!IsServer || !IsAggressive.Value)
        {
            return;
        }

        if (cycleManager != null && !cycleManager.IsNight)
        {
            NetworkObject.Despawn(true);
            return;
        }

        targetRefreshTimer -= Time.deltaTime;
        attackTimer -= Time.deltaTime;
        if (targetRefreshTimer <= 0f)
        {
            targetRefreshTimer = Mathf.Max(0.1f, targetRefreshInterval);
            target = FindBestTarget();
        }

        if (target == null || !target.IsSpawned)
        {
            StopMoving();
            return;
        }

        float distance = Vector3.Distance(transform.position, target.transform.position);
        if (distance > detectionRadius)
        {
            target = null;
            StopMoving();
            return;
        }

        if (distance <= attackRange)
        {
            StopMoving();
            FaceTarget();
            if (attackTimer <= 0f)
            {
                attackTimer = Mathf.Max(0.1f, attackCooldown);
                target.ApplyDamageServer(damagePerAttack);
                PlayAttackClientRpc();
            }
        }
        else
        {
            ChaseTarget();
        }
    }

    private NetworkPlayer FindBestTarget()
    {
        if (matchManager == null)
        {
            matchManager = FindObjectOfType<NetworkMatchManager>();
        }

        if (matchManager == null)
        {
            return null;
        }

        NetworkPlayer bestTarget = null;
        float bestScore = float.MaxValue;

        foreach (NetworkPlayer player in matchManager.ActivePlayers)
        {
            if (player == null || !player.IsSpawned)
            {
                continue;
            }

            PlayerSurvival survival = player.GetComponent<PlayerSurvival>();
            if (survival != null && survival.NetworkIsDowned.Value)
            {
                continue;
            }

            float distance = Vector3.Distance(transform.position, player.transform.position);
            if (distance > detectionRadius)
            {
                continue;
            }

            PlayerCampGuard guard = player.GetComponent<PlayerCampGuard>();
            bool isOutsideCamp = guard == null || !guard.IsInsideCamp;
            float score = distance + (isOutsideCamp ? 0f : 25f);
            if (score < bestScore)
            {
                bestScore = score;
                bestTarget = player;
            }
        }

        return bestTarget;
    }

    private void ChaseTarget()
    {
        if (agent == null || !agent.enabled || !agent.isOnNavMesh)
        {
            return;
        }

        agent.isStopped = false;
        agent.SetDestination(target.transform.position);
    }

    private void StopMoving()
    {
        if (agent != null && agent.enabled && agent.isOnNavMesh)
        {
            agent.isStopped = true;
            agent.ResetPath();
        }
    }

    private void FaceTarget()
    {
        Vector3 direction = target.transform.position - transform.position;
        direction.y = 0f;
        if (direction.sqrMagnitude > 0.01f)
        {
            transform.rotation = Quaternion.LookRotation(direction.normalized);
        }
    }

    [ClientRpc]
    private void PlayAttackClientRpc()
    {
        if (animator != null && !string.IsNullOrWhiteSpace(attackTrigger))
        {
            animator.SetTrigger(attackTrigger);
        }

        if (attackAudio != null)
        {
            attackAudio.Play();
        }
    }
}
