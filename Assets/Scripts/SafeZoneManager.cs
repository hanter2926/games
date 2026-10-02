using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public sealed class SafeZoneManager : NetworkBehaviour
{
    [Header("Zone")]
    [SerializeField] private NetworkMatchManager matchManager;
    [SerializeField] private Transform zoneVisual;
    [SerializeField] private float zoneDamagePerSecond = 5f;
    [SerializeField] private float damageTickInterval = 0.5f;
    [SerializeField] private float visualBaseDiameter = 1f;
    private float damageTimer;

    private void Awake()
    {
        matchManager = matchManager != null ? matchManager : FindObjectOfType<NetworkMatchManager>();
    }

    private void Update()
    {
        if (matchManager == null)
        {
            matchManager = FindObjectOfType<NetworkMatchManager>();
        }

        if (matchManager == null)
        {
            return;
        }

        float radius = matchManager.SafeZoneRadius.Value;
        UpdateZoneVisual(radius);

        if (!IsServer || !IsDamageActive(matchManager.CurrentState.Value))
        {
            return;
        }

        damageTimer -= Time.deltaTime;
        if (damageTimer > 0f)
        {
            return;
        }

        damageTimer = Mathf.Max(0.05f, damageTickInterval);
        DamagePlayersOutsideZone(radius);
    }

    private bool IsDamageActive(MatchState state)
    {
        return state == MatchState.BattleStarted || state == MatchState.SafeZoneShrinking;
    }

    private void DamagePlayersOutsideZone(float radius)
    {
        if (matchManager == null)
        {
            return;
        }

        Vector3 center = transform.position;
        float radiusSquared = radius * radius;
        float damage = Mathf.Max(0f, zoneDamagePerSecond) * damageTimer;

        foreach (NetworkPlayer player in matchManager.ActivePlayers)
        {
            if (player == null || !player.IsSpawned || !IsOutside(player.transform.position, center, radiusSquared))
            {
                continue;
            }

            player.ApplyDamageServer(damage);
        }
    }

    private bool IsOutside(Vector3 position, Vector3 center, float radiusSquared)
    {
        Vector3 offset = position - center;
        offset.y = 0f;
        return offset.sqrMagnitude > radiusSquared;
    }

    private void UpdateZoneVisual(float radius)
    {
        if (zoneVisual == null || visualBaseDiameter <= 0f)
        {
            return;
        }

        float diameterScale = radius * 2f / visualBaseDiameter;
        zoneVisual.localScale = new Vector3(diameterScale, zoneVisual.localScale.y, diameterScale);
    }
}