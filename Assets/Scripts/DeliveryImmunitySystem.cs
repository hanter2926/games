using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

[RequireComponent(typeof(NetworkObject))]
public sealed class DeliveryImmunitySystem : NetworkBehaviour
{
    public const float ImmunityDurationSeconds = 60f;

    private sealed class ImmunityRecord
    {
        public ulong courierClientId;
        public ulong protectedRecipientClientId;
        public int protectedTeamId;
        public double expiresAtServerTime;
    }

    public NetworkVariable<bool> NetworkIsImmune = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);
    public NetworkVariable<float> NetworkRemainingSeconds = new(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);
    public NetworkVariable<ulong> ProtectedRecipientClientId = new(
        ulong.MaxValue,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);
    public NetworkVariable<int> ProtectedTeamId = new(
        -1,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private static readonly Dictionary<ulong, ImmunityRecord> activeRecords = new();

    public override void OnNetworkDespawn()
    {
        if (IsServer)
        {
            ClearForCourierServer(OwnerClientId);
        }
    }

    private void Update()
    {
        if (!IsServer || !NetworkIsImmune.Value)
        {
            return;
        }

        if (!activeRecords.TryGetValue(OwnerClientId, out ImmunityRecord record))
        {
            SetProtectionState(false, null);
            return;
        }

        double remaining = record.expiresAtServerTime - NetworkManager.ServerTime.Time;
        if (remaining <= 0d)
        {
            ClearForCourierServer(OwnerClientId);
            return;
        }

        NetworkRemainingSeconds.Value = Mathf.Clamp((float)remaining, 0f, ImmunityDurationSeconds);
    }

    public static bool ActivateForDeliveryServer(NetworkPlayer courier, NetworkPlayer recipient)
    {
        if (courier == null || recipient == null || !courier.IsServer || !courier.IsSpawned || !recipient.IsSpawned)
        {
            return false;
        }

        DeliveryImmunitySystem immunity = courier.GetComponent<DeliveryImmunitySystem>();
        if (immunity == null)
        {
            return false;
        }

        ImmunityRecord record = new()
        {
            courierClientId = courier.OwnerClientId,
            protectedRecipientClientId = recipient.OwnerClientId,
            protectedTeamId = recipient.TeamId.Value,
            expiresAtServerTime = NetworkManager.ServerTime.Time + ImmunityDurationSeconds
        };

        activeRecords[courier.OwnerClientId] = record;
        immunity.SetProtectionState(true, record);
        return true;
    }

    public static void ClearForCourierServer(ulong courierClientId)
    {
        if (!activeRecords.TryGetValue(courierClientId, out ImmunityRecord record))
        {
            return;
        }

        activeRecords.Remove(courierClientId);
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.ConnectedClients.TryGetValue(courierClientId, out NetworkClient client) && client.PlayerObject != null)
        {
            DeliveryImmunitySystem immunity = client.PlayerObject.GetComponent<DeliveryImmunitySystem>();
            immunity?.SetProtectionState(false, null);
        }
    }

    public static void ClearForProtectedRecipientServer(ulong recipientClientId)
    {
        List<ulong> couriersToClear = new();
        foreach (KeyValuePair<ulong, ImmunityRecord> entry in activeRecords)
        {
            if (entry.Value.protectedRecipientClientId == recipientClientId)
            {
                couriersToClear.Add(entry.Key);
            }
        }

        foreach (ulong courierClientId in couriersToClear)
        {
            ClearForCourierServer(courierClientId);
        }
    }

    public bool IsDamageBlockedFromServer(NetworkPlayer attacker)
    {
        if (!IsServer || !NetworkIsImmune.Value || attacker == null)
        {
            return false;
        }

        if (!activeRecords.TryGetValue(OwnerClientId, out ImmunityRecord record))
        {
            return false;
        }

        if (record.expiresAtServerTime <= NetworkManager.ServerTime.Time)
        {
            ClearForCourierServer(OwnerClientId);
            return false;
        }

        return attacker.OwnerClientId == record.protectedRecipientClientId ||
               record.protectedTeamId >= 0 && attacker.TeamId.Value == record.protectedTeamId;
    }

    private void SetProtectionState(bool enabled, ImmunityRecord record)
    {
        NetworkIsImmune.Value = enabled;
        if (!enabled || record == null)
        {
            NetworkRemainingSeconds.Value = 0f;
            ProtectedRecipientClientId.Value = ulong.MaxValue;
            ProtectedTeamId.Value = -1;
            return;
        }

        NetworkRemainingSeconds.Value = ImmunityDurationSeconds;
        ProtectedRecipientClientId.Value = record.protectedRecipientClientId;
        ProtectedTeamId.Value = record.protectedTeamId;
    }
}
