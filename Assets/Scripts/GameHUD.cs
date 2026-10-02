using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public sealed class GameHUD : MonoBehaviour
{
    [Header("Status UI")]
    [SerializeField] private Slider healthSlider;
    [SerializeField] private Slider energySlider;
    [SerializeField] private Text teamCampStatusText;
    [SerializeField] private Text guardStatusText;
    [SerializeField] private Text matchStatusText;

    [Header("Action Buttons")]
    [SerializeField] private Button eatFoodButton;
    [SerializeField] private Button guardDutyButton;
    [SerializeField] private Button laughButton;
    [SerializeField] private Button speakButton;
    [SerializeField] private Button byeButton;
    [SerializeField] private Button fireButton;
    [SerializeField] private float foodAmount = 50f;

    private NetworkPlayer localPlayer;
    private PlayerSurvival localSurvival;
    private PlayerCampGuard localCampGuard;
    private PlayerSocialInteractions localSocial;
    private NetworkPlayerCombat localCombat;
    private NetworkMatchManager matchManager;

    private void OnEnable()
    {
        eatFoodButton?.onClick.AddListener(EatFood);
        guardDutyButton?.onClick.AddListener(ToggleGuardDuty);
        laughButton?.onClick.AddListener(Laugh);
        speakButton?.onClick.AddListener(Speak);
        byeButton?.onClick.AddListener(WaveBye);
        fireButton?.onClick.AddListener(Fire);
    }

    private void OnDisable()
    {
        eatFoodButton?.onClick.RemoveListener(EatFood);
        guardDutyButton?.onClick.RemoveListener(ToggleGuardDuty);
        laughButton?.onClick.RemoveListener(Laugh);
        speakButton?.onClick.RemoveListener(Speak);
        byeButton?.onClick.RemoveListener(WaveBye);
        fireButton?.onClick.RemoveListener(Fire);
        UnbindMatchManager();
    }

    private void Update()
    {
        TryBindLocalPlayer();
        TryBindMatchManager();
        RefreshPlayerUI();
    }

    public void EatFood()
    {
        if (localSurvival != null)
        {
            localSurvival.EatFood(foodAmount);
        }
    }

    public void ToggleGuardDuty()
    {
        if (localPlayer != null)
        {
            localPlayer.ToggleGuardDutyNetworked();
        }
    }

    public void Laugh()
    {
        localSocial?.Laugh();
    }

    public void Speak()
    {
        localSocial?.Speak();
    }

    public void WaveBye()
    {
        localSocial?.WaveBye();
    }

    public void Fire()
    {
        localCombat?.Fire();
    }

    private void TryBindLocalPlayer()
    {
        NetworkManager networkManager = NetworkManager.Singleton;
        NetworkObject playerObject = networkManager != null && networkManager.IsListening
            ? networkManager.SpawnManager.GetLocalPlayerObject()
            : null;
        NetworkPlayer nextPlayer = playerObject != null ? playerObject.GetComponent<NetworkPlayer>() : null;

        if (nextPlayer == localPlayer)
        {
            return;
        }

        localPlayer = nextPlayer;
        localSurvival = localPlayer != null ? localPlayer.GetComponent<PlayerSurvival>() : null;
        localCampGuard = localPlayer != null ? localPlayer.GetComponent<PlayerCampGuard>() : null;
        localSocial = localPlayer != null ? localPlayer.GetComponent<PlayerSocialInteractions>() : null;
        localCombat = localPlayer != null ? localPlayer.GetComponent<NetworkPlayerCombat>() : null;
    }

    private void TryBindMatchManager()
    {
        if (matchManager == null)
        {
            matchManager = FindObjectOfType<NetworkMatchManager>();
            if (matchManager != null)
            {
                matchManager.CurrentState.OnValueChanged += OnMatchStateChanged;
            }
        }

        if (matchManager != null)
        {
            UpdateMatchStatus(matchManager.CurrentState.Value);
        }
    }

    private void UnbindMatchManager()
    {
        if (matchManager != null)
        {
            matchManager.CurrentState.OnValueChanged -= OnMatchStateChanged;
        }
    }

    private void RefreshPlayerUI()
    {
        if (localSurvival != null)
        {
            if (healthSlider != null && localSurvival.maxHealth > 0f)
            {
                healthSlider.value = localSurvival.currentHealth / localSurvival.maxHealth;
            }

            if (energySlider != null && localSurvival.maxEnergy > 0f)
            {
                energySlider.value = localSurvival.currentEnergy / localSurvival.maxEnergy;
            }
        }

        if (localCampGuard != null)
        {
            if (guardStatusText != null)
            {
                guardStatusText.text = localCampGuard.IsOnGuardDuty ? "GUARD DUTY" : "RESTING";
            }

            if (teamCampStatusText != null && localPlayer != null)
            {
                string campState = localCampGuard.IsInsideCamp ? "Inside camp" : "Outside camp";
                teamCampStatusText.text = "Team " + localPlayer.TeamId.Value + " | " + campState;
            }
        }
    }

    private void OnMatchStateChanged(MatchState previousState, MatchState newState)
    {
        UpdateMatchStatus(newState);
    }

    private void UpdateMatchStatus(MatchState state)
    {
        if (matchStatusText != null)
        {
            matchStatusText.text = state switch
            {
                MatchState.WaitingForPlayers => "Waiting for players",
                MatchState.BattleStarted => "Battle started",
                MatchState.SafeZoneShrinking => "Safe zone shrinking",
                MatchState.MatchEnded => "Match ended",
                _ => string.Empty
            };
        }
    }
}