using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

public sealed class GameHUD : MonoBehaviour
{
    [Header("Status UI")]
    [SerializeField] private Slider healthSlider;
    [SerializeField] private Slider energySlider;
    [SerializeField] private Slider staminaSlider;
    [SerializeField] private Slider hungerSlider;
    [SerializeField] private Text teamCampStatusText;
    [SerializeField] private Text guardStatusText;
    [SerializeField] private Text matchStatusText;
    [SerializeField] private Text moneyText;
    [SerializeField] private Text deliveryStatusText;
    [SerializeField] private Text medicalKitsText;
    [SerializeField] private Text cookingStatusText;

    [Header("Action Buttons")]
    [SerializeField] private Button eatFoodButton;
    [SerializeField] private Button guardDutyButton;
    [SerializeField] private Button laughButton;
    [SerializeField] private Button speakButton;
    [SerializeField] private Button byeButton;
    [SerializeField] private Button fireButton;
    [SerializeField] private Button cookMealButton;
    [SerializeField] private Button orderMealButton;
    [SerializeField] private Button orderMedicalKitButton;
    [SerializeField] private Button hospitalButton;
    [SerializeField] private Button medicalKitButton;
    [SerializeField] private Button acceptDeliveryButton;
    [SerializeField] private Button completeDeliveryButton;
    [SerializeField] private Button cookVegetarianButton;
    [SerializeField] private Button cookNonVegetarianButton;
    [SerializeField] private float foodAmount = 50f;

    private NetworkPlayer localPlayer;
    private PlayerSurvival localSurvival;
    private PlayerController localController;
    private PlayerCampGuard localCampGuard;
    private PlayerSocialInteractions localSocial;
    private NetworkPlayerCombat localCombat;
    private DeliveryAndHospitalSystem localDelivery;
    private CampCookingSystem localCooking;
    private NetworkMatchManager matchManager;

    private void OnEnable()
    {
        eatFoodButton?.onClick.AddListener(EatFood);
        guardDutyButton?.onClick.AddListener(ToggleGuardDuty);
        laughButton?.onClick.AddListener(Laugh);
        speakButton?.onClick.AddListener(Speak);
        byeButton?.onClick.AddListener(WaveBye);
        fireButton?.onClick.AddListener(Fire);
        cookMealButton?.onClick.AddListener(CookMeal);
        orderMealButton?.onClick.AddListener(OrderMeal);
        orderMedicalKitButton?.onClick.AddListener(OrderMedicalKit);
        hospitalButton?.onClick.AddListener(RequestHospitalTreatment);
        medicalKitButton?.onClick.AddListener(UseMedicalKit);
        acceptDeliveryButton?.onClick.AddListener(AcceptDelivery);
        completeDeliveryButton?.onClick.AddListener(CompleteDelivery);
        cookVegetarianButton?.onClick.AddListener(CookVegetarian);
        cookNonVegetarianButton?.onClick.AddListener(CookNonVegetarian);
    }

    private void OnDisable()
    {
        eatFoodButton?.onClick.RemoveListener(EatFood);
        guardDutyButton?.onClick.RemoveListener(ToggleGuardDuty);
        laughButton?.onClick.RemoveListener(Laugh);
        speakButton?.onClick.RemoveListener(Speak);
        byeButton?.onClick.RemoveListener(WaveBye);
        fireButton?.onClick.RemoveListener(Fire);
        cookMealButton?.onClick.RemoveListener(CookMeal);
        orderMealButton?.onClick.RemoveListener(OrderMeal);
        orderMedicalKitButton?.onClick.RemoveListener(OrderMedicalKit);
        hospitalButton?.onClick.RemoveListener(RequestHospitalTreatment);
        medicalKitButton?.onClick.RemoveListener(UseMedicalKit);
        acceptDeliveryButton?.onClick.RemoveListener(AcceptDelivery);
        completeDeliveryButton?.onClick.RemoveListener(CompleteDelivery);
        cookVegetarianButton?.onClick.RemoveListener(CookVegetarian);
        cookNonVegetarianButton?.onClick.RemoveListener(CookNonVegetarian);
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

    public void CookMeal()
    {
        localDelivery?.CookMeal();
    }

    public void OrderMeal()
    {
        localDelivery?.OrderDelivery(DeliveryItemType.CookedMeal);
    }

    public void OrderMedicalKit()
    {
        localDelivery?.OrderDelivery(DeliveryItemType.MedicalKit);
    }

    public void RequestHospitalTreatment()
    {
        localDelivery?.RequestHospitalTreatment();
    }

    public void UseMedicalKit()
    {
        localDelivery?.UseMedicalKit();
    }

    public void AcceptDelivery()
    {
        if (localDelivery != null)
        {
            localDelivery.AcceptDelivery(localDelivery.ActiveOrderId.Value);
        }
    }

    public void CompleteDelivery()
    {
        localDelivery?.CompleteDelivery();
    }

    public void CookVegetarian()
    {
        localCooking?.CookVegetarian();
    }

    public void CookNonVegetarian()
    {
        localCooking?.CookNonVegetarian();
    }

    public void SharePreparedMeal(ulong teammateClientId)
    {
        localCooking?.SharePreparedMealWithPlayer(teammateClientId);
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
        localController = localPlayer != null ? localPlayer.GetComponent<PlayerController>() : null;
        localSurvival = localPlayer != null ? localPlayer.GetComponent<PlayerSurvival>() : null;
        localCampGuard = localPlayer != null ? localPlayer.GetComponent<PlayerCampGuard>() : null;
        localSocial = localPlayer != null ? localPlayer.GetComponent<PlayerSocialInteractions>() : null;
        localCombat = localPlayer != null ? localPlayer.GetComponent<NetworkPlayerCombat>() : null;
        localDelivery = localPlayer != null ? localPlayer.GetComponent<DeliveryAndHospitalSystem>() : null;
        localCooking = localPlayer != null ? localPlayer.GetComponent<CampCookingSystem>() : null;
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

            if (hungerSlider != null && localSurvival.maxHunger > 0f)
            {
                hungerSlider.value = localSurvival.currentHunger / localSurvival.maxHunger;
            }

            if (moneyText != null)
            {
                moneyText.text = "$" + localSurvival.CurrentMoney;
            }

            if (medicalKitsText != null)
            {
                medicalKitsText.text = "Medical kits: " + localSurvival.CurrentMedicalKits;
            }
        }

        if (localDelivery != null && deliveryStatusText != null)
        {
            deliveryStatusText.text = localDelivery.DeliveryStatus.Value.ToString();
        }

        if (localCooking != null && cookingStatusText != null)
        {
            cookingStatusText.text = localCooking.State.Value + " | Meals: " + localCooking.PreparedMealCount.Value;
        }

        if (localController != null && staminaSlider != null && localController.maxStamina > 0f)
        {
            staminaSlider.value = localController.CurrentStamina / localController.maxStamina;
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