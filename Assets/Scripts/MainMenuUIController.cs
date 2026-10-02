using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public sealed class MainMenuUIController : MonoBehaviour
{
    [Header("Managers")]
    [SerializeField] private MainMenuManager mainMenuManager;

    [Header("Play and Mode")]
    [SerializeField] private InputField playerNameInput;
    [SerializeField] private Toggle previousTeamToggle;
    [SerializeField] private Toggle soloToggle;
    [SerializeField] private Toggle duoToggle;
    [SerializeField] private Toggle squadToggle;
    [SerializeField] private Dropdown mapDropdown;
    [SerializeField] private Button playButton;
    [SerializeField] private Button hostButton;
    [SerializeField] private Button joinButton;

    [Header("Profile")]
    [SerializeField] private Text profileNameText;
    [SerializeField] private Text levelText;
    [SerializeField] private Text rankText;
    [SerializeField] private Text killsText;
    [SerializeField] private Text matchesText;
    [SerializeField] private Image avatarImage;

    [Header("Settings")]
    [SerializeField] private Slider masterVolumeSlider;
    [SerializeField] private Slider sfxVolumeSlider;
    [SerializeField] private Slider musicVolumeSlider;
    [SerializeField] private Slider sensitivitySlider;

    [Header("Store and Inventory")]
    [SerializeField] private Text inventoryText;
    [SerializeField] private Button buyFoodButton;
    [SerializeField] private Button buySuppliesButton;
    [SerializeField] private Button customizeButton;
    public UnityEvent buyFoodRequested;
    public UnityEvent buySuppliesRequested;
    public UnityEvent customizeRequested;

    [Header("Panels")]
    [SerializeField] private GameObject profilePanel;
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private GameObject storePanel;

    private void Awake()
    {
        mainMenuManager = mainMenuManager != null ? mainMenuManager : FindObjectOfType<MainMenuManager>();
        LoadSettings();
        RegisterListeners();
        RefreshProfile();
    }

    private void OnDestroy()
    {
        UnregisterListeners();
    }

    private void Update()
    {
        RefreshProfile();
    }

    public void PlaySelectedMode()
    {
        ApplyPlayerIdentity();
        if (mainMenuManager != null)
        {
            mainMenuManager.HostGame();
        }
    }

    public void JoinSelectedMode()
    {
        ApplyPlayerIdentity();
        if (mainMenuManager != null)
        {
            mainMenuManager.JoinGame();
        }
    }

    public void ApplyPlayerIdentity()
    {
        if (mainMenuManager != null)
        {
            mainMenuManager.SetPlayerIdentity(
                playerNameInput != null ? playerNameInput.text : "Player",
                previousTeamToggle != null && previousTeamToggle.isOn,
                GetSelectedTeamMode());
        }
    }

    public void ShowProfile(bool visible)
    {
        if (profilePanel != null) profilePanel.SetActive(visible);
    }

    public void ShowSettings(bool visible)
    {
        if (settingsPanel != null) settingsPanel.SetActive(visible);
    }

    public void ShowStore(bool visible)
    {
        if (storePanel != null) storePanel.SetActive(visible);
    }

    private void RegisterListeners()
    {
        playButton?.onClick.AddListener(PlaySelectedMode);
        hostButton?.onClick.AddListener(PlaySelectedMode);
        joinButton?.onClick.AddListener(JoinSelectedMode);
        buyFoodButton?.onClick.AddListener(RequestFoodPurchase);
        buySuppliesButton?.onClick.AddListener(RequestSupplyPurchase);
        customizeButton?.onClick.AddListener(RequestCustomization);
        masterVolumeSlider?.onValueChanged.AddListener(SetMasterVolume);
        sfxVolumeSlider?.onValueChanged.AddListener(SetSfxVolume);
        musicVolumeSlider?.onValueChanged.AddListener(SetMusicVolume);
        sensitivitySlider?.onValueChanged.AddListener(SetSensitivity);
    }

    private void UnregisterListeners()
    {
        playButton?.onClick.RemoveListener(PlaySelectedMode);
        hostButton?.onClick.RemoveListener(PlaySelectedMode);
        joinButton?.onClick.RemoveListener(JoinSelectedMode);
        buyFoodButton?.onClick.RemoveListener(RequestFoodPurchase);
        buySuppliesButton?.onClick.RemoveListener(RequestSupplyPurchase);
        customizeButton?.onClick.RemoveListener(RequestCustomization);
        masterVolumeSlider?.onValueChanged.RemoveListener(SetMasterVolume);
        sfxVolumeSlider?.onValueChanged.RemoveListener(SetSfxVolume);
        musicVolumeSlider?.onValueChanged.RemoveListener(SetMusicVolume);
        sensitivitySlider?.onValueChanged.RemoveListener(SetSensitivity);
    }

    private void RefreshProfile()
    {
        PlayerSessionManager session = PlayerSessionManager.Local;
        if (session == null)
        {
            return;
        }

        NetworkPlayer player = session.GetComponent<NetworkPlayer>();
        PlayerProgression progression = session.GetComponent<PlayerProgression>();
        if (profileNameText != null) profileNameText.text = session.PlayerName.Value.ToString();
        if (progression == null) return;
        if (levelText != null) levelText.text = "Level " + progression.Level.Value;
        if (rankText != null) rankText.text = progression.Rank.Value.ToString();
        if (killsText != null) killsText.text = "Kills " + progression.Kills.Value;
        if (matchesText != null) matchesText.text = "Matches " + progression.MatchesPlayed.Value;
        if (inventoryText != null && player != null)
        {
            PlayerSurvival survival = player.GetComponent<PlayerSurvival>();
            inventoryText.text = survival != null ? "Money: $" + survival.CurrentMoney + " | Kits: " + survival.CurrentMedicalKits : string.Empty;
        }
    }

    private void RequestFoodPurchase() => buyFoodRequested?.Invoke();
    private void RequestSupplyPurchase() => buySuppliesRequested?.Invoke();
    private void RequestCustomization() => customizeRequested?.Invoke();

    private MatchTeamMode GetSelectedTeamMode()
    {
        if (soloToggle != null && soloToggle.isOn) return MatchTeamMode.Solo;
        if (duoToggle != null && duoToggle.isOn) return MatchTeamMode.Duo;
        return MatchTeamMode.Squad;
    }

    private void SetMasterVolume(float value)
    {
        AudioListener.volume = value;
        PlayerPrefs.SetFloat("MasterVolume", value);
    }

    private void SetSfxVolume(float value) => PlayerPrefs.SetFloat("SfxVolume", value);
    private void SetMusicVolume(float value) => PlayerPrefs.SetFloat("MusicVolume", value);
    private void SetSensitivity(float value) => PlayerPrefs.SetFloat("Sensitivity", value);

    private void LoadSettings()
    {
        if (masterVolumeSlider != null) masterVolumeSlider.value = PlayerPrefs.GetFloat("MasterVolume", 1f);
        if (sfxVolumeSlider != null) sfxVolumeSlider.value = PlayerPrefs.GetFloat("SfxVolume", 1f);
        if (musicVolumeSlider != null) musicVolumeSlider.value = PlayerPrefs.GetFloat("MusicVolume", 1f);
        if (sensitivitySlider != null) sensitivitySlider.value = PlayerPrefs.GetFloat("Sensitivity", 1f);
    }
}
