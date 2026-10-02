using Unity.Netcode;
using Unity.Netcode.Transports.UTP;
using UnityEngine;
using UnityEngine.SceneManagement;

using UnityEngine.UI;

public sealed class MainMenuManager : MonoBehaviour
{
    [Header("Network")]
    [SerializeField] private NetworkManager networkManager;
    [SerializeField] private UnityTransport unityTransport;
    [SerializeField] private string gameplayScene = "Gameplay";
    [SerializeField] private string defaultAddress = "127.0.0.1";
    [SerializeField] private ushort defaultPort = 7777;

    [Header("Menu UI")]
    [SerializeField] private InputField addressInput;
    [SerializeField] private InputField portInput;
    [SerializeField] private InputField playerNameInput;
    [SerializeField] private Toggle previousTeamToggle;
    [SerializeField] private Button hostButton;
    [SerializeField] private Button joinButton;
    [SerializeField] private Button exitButton;
    [SerializeField] private Text statusText;
    [SerializeField] private GameObject menuPanel;
    [SerializeField] private GameObject lobbyPanel;

    private bool sceneLoadRequested;
    private bool sessionStatusShown;

    private void Awake()
    {
        networkManager = networkManager != null ? networkManager : NetworkManager.Singleton;
        unityTransport = unityTransport != null ? unityTransport : networkManager != null
            ? networkManager.GetComponent<UnityTransport>()
            : null;

        hostButton?.onClick.AddListener(HostGame);
        joinButton?.onClick.AddListener(JoinGame);
        exitButton?.onClick.AddListener(ExitGame);
        SetPanels(menuVisible: true, lobbyVisible: false);
    }

    private void OnEnable()
    {
        if (networkManager != null)
        {
            networkManager.OnClientConnectedCallback += OnClientConnected;
            networkManager.OnClientDisconnectCallback += OnClientDisconnected;
        }
    }

    private void OnDisable()
    {
        if (networkManager != null)
        {
            networkManager.OnClientConnectedCallback -= OnClientConnected;
            networkManager.OnClientDisconnectCallback -= OnClientDisconnected;
        }

        hostButton?.onClick.RemoveListener(HostGame);
        joinButton?.onClick.RemoveListener(JoinGame);
        exitButton?.onClick.RemoveListener(ExitGame);
    }

    private void Update()
    {
        if (networkManager == null || !networkManager.IsListening)
        {
            return;
        }

        NetworkMatchManager matchManager = FindObjectOfType<NetworkMatchManager>();
        SubmitPlayerSessionIfReady();
        if (matchManager == null)
        {
            return;
        }

        bool gameplayHasStarted = matchManager.CurrentState.Value == MatchState.BattleStarted || matchManager.CurrentState.Value == MatchState.SafeZoneShrinking;
        if (gameplayHasStarted && !sceneLoadRequested)
        {
            sceneLoadRequested = true;
            SetStatus("Match starting...");
            LoadGameplayScene();
        }
    }

    public void HostGame()
    {
        if (!PrepareConnection() || networkManager == null)
        {
            SetStatus("Network setup is unavailable.");
            return;
        }

        sceneLoadRequested = false;
        if (networkManager.StartHost())
        {
            SetPanels(menuVisible: false, lobbyVisible: true);
            SetStatus("Hosting. Waiting for players...");
        }
        else
        {
            SetStatus("Unable to start host.");
        }
    }

    public void JoinGame()
    {
        if (!PrepareConnection() || networkManager == null)
        {
            SetStatus("Network setup is unavailable.");
            return;
        }

        sceneLoadRequested = false;
        if (networkManager.StartClient())
        {
            SetPanels(menuVisible: false, lobbyVisible: true);
            SetStatus("Connecting to host...");
        }
        else
        {
            SetStatus("Unable to start client.");
        }
    }

    public void SubmitPlayerSessionIfReady()
    {
        PlayerSessionManager session = PlayerSessionManager.Local;
        if (session == null)
        {
            return;
        }

        string sessionStatus = session.SessionStatus.Value.ToString();
        if (!string.IsNullOrWhiteSpace(sessionStatus) && !sessionStatusShown)
        {
            SetStatus(sessionStatus);
            sessionStatusShown = true;
        }

        if (session.HasSubmittedSession)
        {
            return;
        }

        string playerName = playerNameInput != null ? playerNameInput.text : "Player";
        MatchJoinPreference preference = previousTeamToggle != null && previousTeamToggle.isOn
            ? MatchJoinPreference.PreviousTeam
            : MatchJoinPreference.Solo;
        session.SubmitLocalSession(playerName, preference);
        sessionStatusShown = false;
    }

    public void ExitGame()
    {
        if (networkManager != null && networkManager.IsListening)
        {
            networkManager.Shutdown();
        }

        #if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
        #else
        Application.Quit();
        #endif
    }

    private bool PrepareConnection()
    {
        networkManager = networkManager != null ? networkManager : NetworkManager.Singleton;
        unityTransport = unityTransport != null ? unityTransport : networkManager != null
            ? networkManager.GetComponent<UnityTransport>()
            : null;

        if (networkManager == null || unityTransport == null || networkManager.IsListening)
        {
            return false;
        }

        string address = string.IsNullOrWhiteSpace(addressInput != null ? addressInput.text : null)
            ? defaultAddress
            : addressInput.text.Trim();
        ushort port = defaultPort;
        if (portInput != null && ushort.TryParse(portInput.text, out ushort enteredPort))
        {
            port = enteredPort;
        }

        unityTransport.SetConnectionData(address, port);
        return true;
    }

    private void OnClientConnected(ulong clientId)
    {
        if (networkManager != null && clientId == networkManager.LocalClientId)
        {
            SetStatus(networkManager.IsHost ? "Hosting. Entering session..." : "Connected. Entering session...");
        }
    }

    private void OnClientDisconnected(ulong clientId)
    {
        if (networkManager != null && clientId == networkManager.LocalClientId)
        {
            SetPanels(menuVisible: true, lobbyVisible: false);
            SetStatus("Disconnected from host.");
        }
    }

    private void LoadGameplayScene()
    {
        if (networkManager.IsHost)
        {
            networkManager.SceneManager.LoadScene(gameplayScene, LoadSceneMode.Single);
        }
        else
        {
            SetStatus("Host is loading the match...");
        }
    }

    private void SetPanels(bool menuVisible, bool lobbyVisible)
    {
        if (menuPanel != null) menuPanel.SetActive(menuVisible);
        if (lobbyPanel != null) lobbyPanel.SetActive(lobbyVisible);
    }

    private void SetStatus(string message)
    {
        if (statusText != null)
        {
            statusText.text = message;
        }
    }
}