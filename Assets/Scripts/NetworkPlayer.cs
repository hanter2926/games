using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(NetworkTransform))]
[RequireComponent(typeof(PlayerController))]
[RequireComponent(typeof(PlayerSurvival))]
[RequireComponent(typeof(PlayerCampGuard))]
[RequireComponent(typeof(DeliveryAndHospitalSystem))]
public sealed class NetworkPlayer : NetworkBehaviour
{
    [Header("Local Components")]
    [SerializeField] private PlayerController controller;
    [SerializeField] private PlayerSurvival survival;
    [SerializeField] private PlayerCampGuard campGuard;

    [Header("Network State")]
    public NetworkVariable<bool> NetworkOnGuardDuty = new(false, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> TeamId = new(-1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<FixedString64Bytes> DisplayName = new(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private void Awake()
    {
        controller = controller != null ? controller : GetComponent<PlayerController>();
        survival = survival != null ? survival : GetComponent<PlayerSurvival>();
        campGuard = campGuard != null ? campGuard : GetComponent<PlayerCampGuard>();
    }

    public override void OnNetworkSpawn()
    {
        survival.NetworkAuthorityControlled = true;
        campGuard.NetworkAuthorityControlled = true;

        NetworkOnGuardDuty.OnValueChanged += OnGuardDutyChanged;
        campGuard.guardDutyStopped.AddListener(OnGuardDutyStopped);

        if (controller != null && !IsOwner)
        {
            controller.enabled = false;
        }

        if (IsServer)
        {
            NetworkMatchManager matchManager = FindObjectOfType<NetworkMatchManager>();
            if (matchManager != null)
            {
                matchManager.RegisterPlayer(this);
            }
        }

        ApplyReplicatedState();
    }

    public override void OnNetworkDespawn()
    {
        NetworkOnGuardDuty.OnValueChanged -= OnGuardDutyChanged;
        campGuard.guardDutyStopped.RemoveListener(OnGuardDutyStopped);

        if (IsServer)
        {
            NetworkMatchManager matchManager = FindObjectOfType<NetworkMatchManager>();
            matchManager?.UnregisterPlayer(this);
        }
    }

    private void Update()
    {
        if (IsServer)
        {
            survival.SimulateEnergyDrain(Time.deltaTime);
            if (campGuard.IsInsideCamp && !campGuard.IsOnGuardDuty)
            {
                CampArea camp = campGuard.CurrentCamp;
                if (camp != null)
                {
                    camp.Recover(survival, Time.deltaTime);
                }
            }

            if (campGuard.IsOnGuardDuty)
            {
                campGuard.EvaluateGuardAlert();
            }

        }

        if (IsOwner && Input.GetKeyDown(KeyCode.G))
        {
            RequestGuardDutyServerRpc(!NetworkOnGuardDuty.Value);
        }
    }

    public void ToggleGuardDutyNetworked()
    {
        if (!IsOwner)
        {
            return;
        }

        RequestGuardDutyServerRpc(!NetworkOnGuardDuty.Value);
    }

    [ServerRpc]
    private void RequestGuardDutyServerRpc(bool enabled)
    {
        if (enabled && !campGuard.IsInsideCamp)
        {
            return;
        }

        NetworkOnGuardDuty.Value = enabled;
        if (campGuard.IsOnGuardDuty != enabled)
        {
            campGuard.SetGuardDuty(enabled);
        }
    }

    public void SetTeamAndSpawnServer(int teamId, Transform spawnPoint)
    {
        if (!IsServer)
        {
            return;
        }

        TeamId.Value = teamId;
        if (spawnPoint != null)
        {
            transform.SetPositionAndRotation(spawnPoint.position, spawnPoint.rotation);
        }
    }

    public void ApplyDamageServer(float damage)
    {
        if (!IsServer)
        {
            return;
        }

        survival.ApplyDamage(damage);
    }

    private void OnGuardDutyChanged(bool previousValue, bool newValue)
    {
        if (campGuard.IsOnGuardDuty != newValue)
        {
            campGuard.SetGuardDuty(newValue);
        }
    }

    private void OnGuardDutyStopped()
    {
        if (IsServer && NetworkOnGuardDuty.Value)
        {
            NetworkOnGuardDuty.Value = false;
        }
    }

    private void ApplyReplicatedState()
    {
        if (campGuard.IsOnGuardDuty != NetworkOnGuardDuty.Value)
        {
            campGuard.SetGuardDuty(NetworkOnGuardDuty.Value);
        }
    }
}