using Unity.Netcode;
using UnityEngine;

public enum PlayerRankTier : byte
{
    Bronze,
    Silver,
    Gold,
    Platinum,
    Diamond,
    Heroic
}

[RequireComponent(typeof(NetworkObject))]
public sealed class PlayerProgression : NetworkBehaviour
{
    public NetworkVariable<int> Experience = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> Level = new(1, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<PlayerRankTier> Rank = new(PlayerRankTier.Bronze, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> Kills = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> MatchesPlayed = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<int> DeliveriesCompleted = new(0, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<float> SurvivalSeconds = new(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    public NetworkVariable<float> EmotionalBond = new(0f, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);

    [Min(0f)] public float maxEmotionalBond = 100f;

    private float survivalXpAccumulator;

    public static int GetExperienceForNextLevel(int level)
    {
        return Mathf.Max(100, level * 100);
    }

    public void AwardKillServer()
    {
        if (IsServer)
        {
            Kills.Value++;
            AwardExperienceServer(150);
        }
    }

    public void AwardMatchStartServer()
    {
        if (IsServer)
        {
            MatchesPlayed.Value++;
            AwardExperienceServer(50);
        }
    }

    public void AwardDeliveryServer()
    {
        if (IsServer)
        {
            DeliveriesCompleted.Value++;
            AwardExperienceServer(100);
        }
    }

    public void AddSurvivalTimeServer(float seconds)
    {
        if (!IsServer || seconds <= 0f)
        {
            return;
        }

        SurvivalSeconds.Value += seconds;
        survivalXpAccumulator += seconds;
        if (survivalXpAccumulator >= 60f)
        {
            int minutes = Mathf.FloorToInt(survivalXpAccumulator / 60f);
            survivalXpAccumulator -= minutes * 60f;
            AwardExperienceServer(minutes * 10);
        }
    }

    public void AddEmotionalBondServer(float amount)
    {
        if (!IsServer || amount <= 0f)
        {
            return;
        }

        EmotionalBond.Value = Mathf.Clamp(EmotionalBond.Value + amount, 0f, maxEmotionalBond);
    }

    public void AwardExperienceServer(int amount)
    {
        if (!IsServer || amount <= 0)
        {
            return;
        }

        Experience.Value += amount;
        while (Experience.Value >= GetExperienceForNextLevel(Level.Value))
        {
            Experience.Value -= GetExperienceForNextLevel(Level.Value);
            Level.Value++;
            Rank.Value = GetRankForLevel(Level.Value);
        }
    }

    public static PlayerRankTier GetRankForLevel(int level)
    {
        if (level >= 50) return PlayerRankTier.Heroic;
        if (level >= 40) return PlayerRankTier.Diamond;
        if (level >= 30) return PlayerRankTier.Platinum;
        if (level >= 20) return PlayerRankTier.Gold;
        if (level >= 10) return PlayerRankTier.Silver;
        return PlayerRankTier.Bronze;
    }
}
