using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public enum MatchJoinPreference : byte
{
    Solo,
    PreviousTeam
}

[RequireComponent(typeof(NetworkObject))]
public sealed class PlayerSessionManager : NetworkBehaviour
{
    public static PlayerSessionManager Local { get; private set; }

    public NetworkVariable<FixedString64Bytes> PlayerName = new(
        default,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);
    public NetworkVariable<MatchJoinPreference> JoinPreference = new(
        MatchJoinPreference.Solo,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);
    public NetworkVariable<bool> IsReturningPlayer = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);
    public NetworkVariable<bool> SessionAccepted = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);
    public NetworkVariable<FixedString128Bytes> SessionStatus = new(
        default,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public bool HasSubmittedSession { get; private set; }

    public override void OnNetworkSpawn()
    {
        if (IsOwner)
        {
            Local = this;
        }
    }

    public override void OnNetworkDespawn()
    {
        if (Local == this)
        {
            Local = null;
        }
    }

    public void SubmitLocalSession(string playerName, MatchJoinPreference preference)
    {
        if (!IsOwner || HasSubmittedSession)
        {
            return;
        }

        string safeName = string.IsNullOrWhiteSpace(playerName) ? "Player" : playerName.Trim();
        safeName = safeName.Length > 48 ? safeName.Substring(0, 48) : safeName;
        SubmitSessionServerRpc(new FixedString64Bytes(safeName), preference);
        HasSubmittedSession = true;
    }

    [ServerRpc]
    private void SubmitSessionServerRpc(FixedString64Bytes requestedName, MatchJoinPreference preference)
    {
        NetworkMatchManager matchManager = FindObjectOfType<NetworkMatchManager>();
        if (matchManager == null)
        {
            SetRejected("Match manager is unavailable");
            return;
        }

        matchManager.ProcessSessionServer(this, requestedName.ToString(), preference);
    }

    public void SetAcceptedServer(string normalizedName, MatchJoinPreference preference, bool returning)
    {
        if (!IsServer)
        {
            return;
        }

        PlayerName.Value = new FixedString64Bytes(normalizedName);
        JoinPreference.Value = preference;
        IsReturningPlayer.Value = returning;
        SessionAccepted.Value = true;
        SessionStatus.Value = new FixedString128Bytes(returning ? "Returning player joined" : "New player joined");
    }

    public void SetRejected(string status)
    {
        if (!IsServer)
        {
            return;
        }

        SessionAccepted.Value = false;
        SessionStatus.Value = new FixedString128Bytes(status ?? "Join rejected");
    }
}
