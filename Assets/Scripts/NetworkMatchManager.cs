using System;
using System.Collections.Generic;
using Unity.Collections;
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
    public const int MaximumPlayers = 80;
    public const float TotalBattleDurationSeconds = 22f * 60f;
    public const float SafeZoneShrinkStartSeconds = 12f * 60f;
    public const float SafeZoneShrinkDurationSeconds = 10f * 60f;
    public const float MidMatchJoinRemainingSeconds = 12f * 60f;

    private sealed class ReturningProfile
    {
        public int teamId;
    }

    [System.Serializable]
    private sealed class TeamCamp
    {
        public int teamId;
        public Transform[] spawnPoints;
    }

    [Header("Match Rules")]
    [Min(1)] [SerializeField] private int minimumPlayers = 2;
    [Min(0f)] [SerializeField] private float waitingDuration = 30f;
    [SerializeField] private float initialSafeZoneRadius = 250f;
    [SerializeField] private float finalSafeZoneRadius = 20f;
    [SerializeField] private NetworkObject lootCratePrefab;

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
    public NetworkVariable<float> MatchTimeRemaining = new(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);
    public NetworkVariable<int> MatchKillCount = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<FixedString128Bytes> LastEliminationMessage = new(default, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    private readonly List<NetworkPlayer> players = new(MaximumPlayers);
    private readonly Dictionary<string, ReturningProfile> returningProfiles = new(StringComparer.OrdinalIgnoreCase);
    private float stateElapsed;
    private float matchElapsed;

    public int ActivePlayerCount => players.Count;
    public IReadOnlyList<NetworkPlayer> ActivePlayers => players;

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
        MatchTimeRemaining.Value = 0f;
        MatchKillCount.Value = 0;
        LastEliminationMessage.Value = new FixedString128Bytes(string.Empty);
    }

    private void Update()
    {
        if (!IsServer || CurrentState.Value == MatchState.MatchEnded)
        {
            return;
        }

        stateElapsed += Time.deltaTime;
        StateTimeRemaining.Value = Mathf.Max(0f, StateTimeRemaining.Value - Time.deltaTime);

        if (CurrentState.Value == MatchState.BattleStarted || CurrentState.Value == MatchState.SafeZoneShrinking)
        {
            matchElapsed = Mathf.Min(TotalBattleDurationSeconds, matchElapsed + Time.deltaTime);
            MatchTimeRemaining.Value = Mathf.Max(0f, TotalBattleDurationSeconds - matchElapsed);
        }

        switch (CurrentState.Value)
        {
            case MatchState.WaitingForPlayers:
                if (players.Count >= minimumPlayers || stateElapsed >= waitingDuration)
                {
                    StartBattle();
                }
                break;
            case MatchState.BattleStarted:
                if (stateElapsed >= SafeZoneShrinkStartSeconds)
                {
                    BeginSafeZoneShrink();
                }
                break;
            case MatchState.SafeZoneShrinking:
                SafeZoneRadius.Value = Mathf.Lerp(initialSafeZoneRadius, finalSafeZoneRadius, stateElapsed / SafeZoneShrinkDurationSeconds);
                if (stateElapsed >= SafeZoneShrinkDurationSeconds)
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

        if (players.Count >= MaximumPlayers || (IsMatchActive && !CanAcceptMidMatchJoinServer()))
        {
            NetworkManager.DisconnectClient(player.OwnerClientId);
            return;
        }

        players.Add(player);
        if (CurrentState.Value == MatchState.WaitingForPlayers)
        {
            AssignTeamAndSpawn(player, players.Count - 1);
        }
    }

    public void RegisterEliminationServer(NetworkPlayer killer, NetworkPlayer victim)
    {
        if (!IsServer || victim == null)
        {
            return;
        }

        MatchKillCount.Value++;
        PlayerProgression killerProgression = killer != null ? killer.GetComponent<PlayerProgression>() : null;
        killerProgression?.AwardKillServer();

        string killerName = killer != null ? GetPlayerDisplayName(killer) : "Wildlife";
        string victimName = GetPlayerDisplayName(victim);
        LastEliminationMessage.Value = new FixedString128Bytes(killerName + " eliminated " + victimName + " (#" + MatchKillCount.Value + ")");
        DropLootServer(victim.transform.position);
    }

    private void DropLootServer(Vector3 position)
    {
        if (lootCratePrefab == null)
        {
            return;
        }

        NetworkObject loot = Instantiate(lootCratePrefab, position, Quaternion.identity);
        loot.Spawn(true);
    }

    private string GetPlayerDisplayName(NetworkPlayer player)
    {
        if (player == null)
        {
            return "Player";
        }

        PlayerSessionManager session = player.GetComponent<PlayerSessionManager>();
        string sessionName = session != null ? session.PlayerName.Value.ToString() : string.Empty;
        return string.IsNullOrWhiteSpace(sessionName) ? "Player" : sessionName;
    }
    public void UnregisterPlayer(NetworkPlayer player)
    {
        if (!IsServer || player == null)
        {
            return;
        }

        players.Remove(player);
        PlayerSessionManager session = player.GetComponent<PlayerSessionManager>();
        if (session != null && session.SessionAccepted.Value && !string.IsNullOrWhiteSpace(session.PlayerName.Value.ToString()))
        {
            returningProfiles[session.PlayerName.Value.ToString()] = new ReturningProfile { teamId = player.TeamId.Value };
        }
    }

    public bool CanAcceptMidMatchJoinServer()
    {
        return IsServer && (CurrentState.Value == MatchState.BattleStarted || CurrentState.Value == MatchState.SafeZoneShrinking) && MatchTimeRemaining.Value > 0f && MatchTimeRemaining.Value <= MidMatchJoinRemainingSeconds;
    }

    public bool IsMatchActive => CurrentState.Value == MatchState.BattleStarted || CurrentState.Value == MatchState.SafeZoneShrinking;

    public void ProcessSessionServer(PlayerSessionManager session, string requestedName, MatchJoinPreference preference, MatchTeamMode teamMode)
    {
        if (!IsServer || session == null)
        {
            return;
        }

        string normalizedName = string.IsNullOrWhiteSpace(requestedName) ? "Player" : requestedName.Trim();
        if (normalizedName.Length > 48)
        {
            normalizedName = normalizedName.Substring(0, 48);
        }

        if (IsMatchActive && !CanAcceptMidMatchJoinServer())
        {
            session.SetRejected("Mid-match joining is available only with 12 minutes or less remaining");
            NetworkManager.DisconnectClient(session.OwnerClientId);
            return;
        }

        bool returning = returningProfiles.TryGetValue(normalizedName, out ReturningProfile profile);
        NetworkPlayer player = session.GetComponent<NetworkPlayer>();
        if (player == null)
        {
            session.SetRejected("Network player is unavailable");
            return;
        }

        int teamId;
        if (preference == MatchJoinPreference.PreviousTeam && returning && IsConfiguredTeam(profile.teamId))
        {
            teamId = profile.teamId;
        }
        else if (teamMode != MatchTeamMode.Solo)
        {
            teamId = teamCamps != null && teamCamps.Length > 0
                ? teamCamps[FindLeastPopulatedCampIndex()].teamId
                : GetSoloTeamId(player.OwnerClientId);
        }
        else
        {
            teamId = GetSoloTeamId(player.OwnerClientId);
        }

        AssignToTeamAndSpawn(player, teamId, players.IndexOf(player));
        returningProfiles[normalizedName] = new ReturningProfile { teamId = teamId };
        session.SetAcceptedServer(normalizedName, preference, teamMode, returning);
    }

    private void StartBattle()
    {
        stateElapsed = 0f;
        matchElapsed = 0f;
        CurrentState.Value = MatchState.BattleStarted;
        StateTimeRemaining.Value = SafeZoneShrinkStartSeconds;
        MatchTimeRemaining.Value = TotalBattleDurationSeconds;

        for (int index = 0; index < players.Count; index++)
        {
            AssignTeamAndSpawn(players[index], index);
        }
    }

    private void BeginSafeZoneShrink()
    {
        stateElapsed = 0f;
        CurrentState.Value = MatchState.SafeZoneShrinking;
        StateTimeRemaining.Value = SafeZoneShrinkDurationSeconds;
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

        int campIndex = FindLeastPopulatedCampIndex();
        AssignToTeamAndSpawn(player, teamCamps[campIndex].teamId, playerIndex);
    }

    private void AssignToTeamAndSpawn(NetworkPlayer player, int teamId, int fallbackIndex)
    {
        if (player == null || teamCamps == null || teamCamps.Length == 0)
        {
            return;
        }

        int campIndex = FindCampIndex(teamId);
        if (campIndex < 0)
        {
            campIndex = Mathf.Abs(fallbackIndex) % teamCamps.Length;
        }

        TeamCamp camp = teamCamps[campIndex];
        Transform[] spawnPoints = camp.spawnPoints;
        int spawnIndex = GetNextSpawnIndex(camp.teamId, spawnPoints != null ? spawnPoints.Length : 0);
        Transform spawnPoint = spawnPoints != null && spawnPoints.Length > 0
            ? spawnPoints[spawnIndex]
            : null;
        player.SetTeamAndSpawnServer(teamId, spawnPoint);
    }

    private int FindLeastPopulatedCampIndex()
    {
        int selectedIndex = 0;
        int lowestCount = int.MaxValue;
        for (int campIndex = 0; campIndex < teamCamps.Length; campIndex++)
        {
            int population = CountTeamMembers(teamCamps[campIndex].teamId);
            if (population < lowestCount)
            {
                lowestCount = population;
                selectedIndex = campIndex;
            }
        }

        return selectedIndex;
    }

    private int GetNextSpawnIndex(int teamId, int spawnPointCount)
    {
        if (spawnPointCount <= 0)
        {
            return 0;
        }

        return CountTeamMembers(teamId) % spawnPointCount;
    }

    private int CountTeamMembers(int teamId)
    {
        int count = 0;
        foreach (NetworkPlayer player in players)
        {
            if (player != null && player.TeamId.Value == teamId)
            {
                count++;
            }
        }

        return count;
    }

    private bool IsConfiguredTeam(int teamId)
    {
        return FindCampIndex(teamId) >= 0;
    }

    private int FindCampIndex(int teamId)
    {
        if (teamCamps == null)
        {
            return -1;
        }

        for (int index = 0; index < teamCamps.Length; index++)
        {
            if (teamCamps[index].teamId == teamId)
            {
                return index;
            }
        }

        return -1;
    }

    private int GetSoloTeamId(ulong clientId)
    {
        return 1000000 + (int)(clientId % 1000000);
    }
}