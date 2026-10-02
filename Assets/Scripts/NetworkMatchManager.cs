using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public enum MatchState : byte
{
    WaitingForPlayers,
    BattleStarted,
    SafeZoneShrinking,
    MatchEnded
}

public sealed class NetworkMatchManager : NetworkBehaviour
{
    [System.Serializable]
    private sealed class TeamCamp
    {
        public int teamId;
        public Transform[] spawnPoints;
    }

    [Header("Match Rules")]
    [Min(1)] [SerializeField] private int minimumPlayers = 2;
    [Min(0f)] [SerializeField] private float waitingDuration = 30f;
    [Min(1f)] [SerializeField] private float battleDuration = 900f;
    [Min(1f)] [SerializeField] private float safeZoneDuration = 600f;
    [SerializeField] private float initialSafeZoneRadius = 250f;
    [SerializeField] private float finalSafeZoneRadius = 20f;

    [Header("Team Camps")]
    [SerializeField] private TeamCamp[] teamCamps;

    public NetworkVariable<MatchState> CurrentState = new(
        MatchState.WaitingForPlayers,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);
    public NetworkVariable<float> StateTimeRemaining = new(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);
    public NetworkVariable<float> SafeZoneRadius = new(
        250f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private readonly List<NetworkPlayer> players = new();
    private float stateElapsed;

    public override void OnNetworkSpawn()
    {
        DontDestroyOnLoad(gameObject);

        if (!IsServer)
        {
            return;
        }

        CurrentState.Value = MatchState.WaitingForPlayers;
        SafeZoneRadius.Value = initialSafeZoneRadius;
        StateTimeRemaining.Value = waitingDuration;
    }

    private void Update()
    {
        if (!IsServer || CurrentState.Value == MatchState.MatchEnded)
        {
            return;
        }

        stateElapsed += Time.deltaTime;
        StateTimeRemaining.Value = Mathf.Max(0f, StateTimeRemaining.Value - Time.deltaTime);

        switch (CurrentState.Value)
        {
            case MatchState.WaitingForPlayers:
                if (players.Count >= minimumPlayers || stateElapsed >= waitingDuration)
                {
                    StartBattle();
                }
                break;
            case MatchState.BattleStarted:
                if (stateElapsed >= battleDuration)
                {
                    BeginSafeZoneShrink();
                }
                break;
            case MatchState.SafeZoneShrinking:
                SafeZoneRadius.Value = Mathf.Lerp(initialSafeZoneRadius, finalSafeZoneRadius, stateElapsed / safeZoneDuration);
                if (stateElapsed >= safeZoneDuration)
                {
                    EndMatch();
                }
                break;
        }
    }

    public void RegisterPlayer(NetworkPlayer player)
    {
        if (!IsServer || player == null || players.Contains(player))
        {
            return;
        }

        players.Add(player);
        AssignTeamAndSpawn(player, players.Count - 1);
    }
    public void UnregisterPlayer(NetworkPlayer player)
    {
        if (!IsServer || player == null)
        {
            return;
        }

        players.Remove(player);
    }

    private void StartBattle()
    {
        stateElapsed = 0f;
        CurrentState.Value = MatchState.BattleStarted;
        StateTimeRemaining.Value = battleDuration;

        for (int index = 0; index < players.Count; index++)
        {
            AssignTeamAndSpawn(players[index], index);
        }
    }

    private void BeginSafeZoneShrink()
    {
        stateElapsed = 0f;
        CurrentState.Value = MatchState.SafeZoneShrinking;
        StateTimeRemaining.Value = safeZoneDuration;
    }

    private void EndMatch()
    {
        CurrentState.Value = MatchState.MatchEnded;
        StateTimeRemaining.Value = 0f;
    }

    private void AssignTeamAndSpawn(NetworkPlayer player, int playerIndex)
    {
        if (teamCamps == null || teamCamps.Length == 0)
        {
            return;
        }

        int campIndex = playerIndex % teamCamps.Length;
        TeamCamp camp = teamCamps[campIndex];
        Transform[] spawnPoints = camp.spawnPoints;
        Transform spawnPoint = spawnPoints != null && spawnPoints.Length > 0
            ? spawnPoints[playerIndex / teamCamps.Length % spawnPoints.Length]
            : null;
        player.SetTeamAndSpawnServer(camp.teamId, spawnPoint);
    }
}