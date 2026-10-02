using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(Collider))]
public sealed class TentIntimacySystem : NetworkBehaviour
{
    [Header("Interaction")]
    [SerializeField] private KeyCode interactKey = KeyCode.E;
    [SerializeField] private KeyCode wardrobePanelKey = KeyCode.Tab;
    [Min(0.5f)] [SerializeField] private float interactionRadius = 4f;
    [Min(1f)] [SerializeField] private float consentTimeout = 20f;

    [Header("Shared Rest")]
    [Min(0f)] [SerializeField] private float healthRecovery = 20f;
    [Min(0f)] [SerializeField] private float energyRecovery = 30f;
    [Min(0f)] [SerializeField] private float emotionalBondIncrease = 5f;
    [Min(0.1f)] [SerializeField] private float animationDuration = 8f;
    [SerializeField] private Transform firstRestAnchor;
    [SerializeField] private Transform secondRestAnchor;
    [SerializeField] private string firstRestTrigger = "SharedRest";
    [SerializeField] private string secondRestTrigger = "SharedRest";

    [Header("Private Wardrobe")]
    [SerializeField] private string wardrobeRootName = "Wardrobe";

    private readonly HashSet<NetworkPlayer> nearbyPlayers = new();
    private readonly Dictionary<ulong, bool[]> originalWardrobeStates = new();
    private ulong pendingInitiatorClientId;
    private ulong pendingPartnerClientId;
    private ulong expectedConsentClientId;
    private ulong activeRestInitiatorClientId;
    private ulong activeRestPartnerClientId;
    private ulong activeRestInitiatorObjectId;
    private ulong activeRestPartnerObjectId;
    private float consentExpiresAt;
    private bool consentPending;
    private bool restInProgress;
    private bool localWardrobeSession;
    private GameObject promptCanvas;
    private readonly List<GameObject> localWardrobeOptions = new();
    private NetworkPlayer localWardrobePlayer;
    private int selectedWardrobeOption;
    private bool localClothingVisible;
    private Text wardrobeStatusText;

    private void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    private void Update()
    {
        if (IsServer && consentPending && Time.time >= consentExpiresAt)
        {
            ShowNoticeClientRpc("Shared rest request expired.", TargetClient(pendingInitiatorClientId));
            ShowNoticeClientRpc("Shared rest request expired.", TargetClient(pendingPartnerClientId));
            ClearConsent();
        }

        if (!IsSpawned || !IsClient)
        {
            return;
        }

        if (localWardrobeSession && Input.GetKeyDown(wardrobePanelKey) && localWardrobePlayer != null)
        {
            if (promptCanvas == null)
            {
                OpenWardrobe(localWardrobePlayer);
            }
            else
            {
                ClosePrompt();
            }
        }

        if (!Input.GetKeyDown(interactKey)) return;

        NetworkPlayer localPlayer = null;
        foreach (NetworkPlayer player in nearbyPlayers)
        {
            if (player != null && player.IsOwner)
            {
                localPlayer = player;
                break;
            }
        }

        if (localPlayer == null)
        {
            return;
        }

        NetworkPlayer nearestPartner = null;
        float nearestDistance = interactionRadius * interactionRadius;
        foreach (NetworkPlayer player in nearbyPlayers)
        {
            if (player == null || player == localPlayer || player.IsOwner)
            {
                continue;
            }

            float distance = (player.transform.position - localPlayer.transform.position).sqrMagnitude;
            if (distance < nearestDistance)
            {
                nearestPartner = player;
                nearestDistance = distance;
            }
        }

        if (nearestPartner != null)
        {
            RequestSharedRest(nearestPartner);
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        NetworkPlayer player = other.GetComponentInParent<NetworkPlayer>();
        if (player != null)
        {
            nearbyPlayers.Add(player);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        NetworkPlayer player = other.GetComponentInParent<NetworkPlayer>();
        if (player != null)
        {
            nearbyPlayers.Remove(player);
        }
    }

    public void RequestSharedRest(NetworkPlayer partner)
    {
        if (!IsSpawned || !IsOwnerOfNearbyPlayer() || partner == null || partner.IsOwner)
        {
            return;
        }

        RequestSharedRestServerRpc(partner.NetworkObjectId);
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestSharedRestServerRpc(ulong partnerNetworkObjectId, ServerRpcParams rpcParams = default)
    {
        if (consentPending || restInProgress || NetworkManager == null)
        {
            return;
        }

        ulong initiatorClientId = rpcParams.Receive.SenderClientId;
        if (!TryGetPlayer(initiatorClientId, out NetworkPlayer initiator) ||
            !NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(partnerNetworkObjectId, out NetworkObject partnerObject))
        {
            return;
        }

        NetworkPlayer partner = partnerObject.GetComponent<NetworkPlayer>();
        if (!IsValidPair(initiator, partner) || partner.OwnerClientId == initiatorClientId)
        {
            return;
        }

        pendingInitiatorClientId = initiatorClientId;
        pendingPartnerClientId = partner.OwnerClientId;
        expectedConsentClientId = pendingPartnerClientId;
        consentExpiresAt = Time.time + consentTimeout;
        consentPending = true;
        ShowConsentPromptClientRpc(
            $"{GetPlayerName(initiator)} wants to share a rest in the tent.",
            TargetClient(pendingPartnerClientId));
    }

    [ServerRpc(RequireOwnership = false)]
    private void RespondToConsentServerRpc(bool accepted, ServerRpcParams rpcParams = default)
    {
        ulong responderClientId = rpcParams.Receive.SenderClientId;
        if (!consentPending || responderClientId != expectedConsentClientId)
        {
            return;
        }

        if (!accepted)
        {
            ulong otherClientId = responderClientId == pendingPartnerClientId
                ? pendingInitiatorClientId
                : pendingPartnerClientId;
            ShowNoticeClientRpc("Shared rest was declined.", TargetClient(otherClientId));
            ClearConsent();
            return;
        }

        if (responderClientId == pendingPartnerClientId)
        {
            expectedConsentClientId = pendingInitiatorClientId;
            ShowConsentPromptClientRpc(
                $"{GetPlayerNameByClientId(pendingPartnerClientId)} agreed. Accept shared rest?",
                TargetClient(pendingInitiatorClientId));
            return;
        }

        if (TryGetPlayer(pendingInitiatorClientId, out NetworkPlayer initiator) &&
            TryGetPlayer(pendingPartnerClientId, out NetworkPlayer partner) &&
            IsValidPair(initiator, partner))
        {
            BeginSharedRest(initiator, partner);
        }
        else
        {
            ShowNoticeClientRpc("Shared rest could not start. Stay together inside the tent.", TargetClients(pendingInitiatorClientId, pendingPartnerClientId));
        }

        ClearConsent();
    }

    private void BeginSharedRest(NetworkPlayer initiator, NetworkPlayer partner)
    {
        restInProgress = true;
        activeRestInitiatorClientId = initiator.OwnerClientId;
        activeRestPartnerClientId = partner.OwnerClientId;
        activeRestInitiatorObjectId = initiator.NetworkObjectId;
        activeRestPartnerObjectId = partner.NetworkObjectId;
        MoveToAnchor(initiator, firstRestAnchor);
        MoveToAnchor(partner, secondRestAnchor);

        initiator.GetComponent<PlayerSurvival>().Recover(healthRecovery, energyRecovery);
        partner.GetComponent<PlayerSurvival>().Recover(healthRecovery, energyRecovery);
        initiator.GetComponent<PlayerProgression>().AddEmotionalBondServer(emotionalBondIncrease);
        partner.GetComponent<PlayerProgression>().AddEmotionalBondServer(emotionalBondIncrease);

        PlaySharedRestClientRpc(
            initiator.NetworkObjectId,
            partner.NetworkObjectId,
            animationDuration,
            TargetClients(initiator.OwnerClientId, partner.OwnerClientId));
        StartCoroutine(ResetRestStateAfterDelay(animationDuration));
    }

    [ClientRpc]
    private void ShowConsentPromptClientRpc(string message, ClientRpcParams clientRpcParams = default)
    {
        CreatePrompt(message, true);
    }

    [ClientRpc]
    private void ShowNoticeClientRpc(string message, ClientRpcParams clientRpcParams = default)
    {
        CreatePrompt(message, false);
    }

    [ClientRpc]
    private void PlaySharedRestClientRpc(
        ulong initiatorNetworkObjectId,
        ulong partnerNetworkObjectId,
        float duration,
        ClientRpcParams clientRpcParams = default)
    {
        localWardrobeSession = true;
        if (TryGetSpawnedPlayer(initiatorNetworkObjectId, out NetworkPlayer initiator))
        {
            CaptureWardrobeState(initiator);
            PlayRestAnimation(initiator, firstRestTrigger);
            SetMovement(initiator, false);
            if (initiator.IsOwner)
            {
                OpenWardrobe(initiator);
            }
        }

        if (TryGetSpawnedPlayer(partnerNetworkObjectId, out NetworkPlayer partner))
        {
            CaptureWardrobeState(partner);
            PlayRestAnimation(partner, secondRestTrigger);
            SetMovement(partner, false);
            if (partner.IsOwner)
            {
                OpenWardrobe(partner);
            }
        }

        StartCoroutine(RestoreMovementAfterDelay(initiatorNetworkObjectId, partnerNetworkObjectId, duration));
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestWardrobeChangeServerRpc(
        ulong targetPlayerObjectId,
        int outfitIndex,
        bool clothingVisible,
        ServerRpcParams rpcParams = default)
    {
        ulong senderClientId = rpcParams.Receive.SenderClientId;
        if (!restInProgress ||
            (senderClientId != activeRestInitiatorClientId && senderClientId != activeRestPartnerClientId) ||
            !TryGetSpawnedPlayer(targetPlayerObjectId, out NetworkPlayer targetPlayer) ||
            targetPlayer.OwnerClientId != senderClientId ||
            (targetPlayerObjectId != activeRestInitiatorObjectId && targetPlayerObjectId != activeRestPartnerObjectId))
        {
            return;
        }

        Transform wardrobeRoot = FindWardrobeRoot(targetPlayer);
        if (wardrobeRoot == null || outfitIndex < -1 || outfitIndex >= wardrobeRoot.childCount)
        {
            return;
        }

        bool applyClothing = clothingVisible && outfitIndex >= 0;
        ApplyWardrobeChangeClientRpc(
            targetPlayerObjectId,
            outfitIndex,
            applyClothing,
            TargetClients(activeRestInitiatorClientId, activeRestPartnerClientId));
    }

    [ClientRpc]
    private void ApplyWardrobeChangeClientRpc(
        ulong targetPlayerObjectId,
        int outfitIndex,
        bool clothingVisible,
        ClientRpcParams clientRpcParams = default)
    {
        if (!TryGetSpawnedPlayer(targetPlayerObjectId, out NetworkPlayer targetPlayer))
        {
            return;
        }

        Transform wardrobeRoot = FindWardrobeRoot(targetPlayer);
        if (wardrobeRoot == null)
        {
            return;
        }

        for (int index = 0; index < wardrobeRoot.childCount; index++)
        {
            wardrobeRoot.GetChild(index).gameObject.SetActive(clothingVisible && index == outfitIndex);
        }

        if (targetPlayer.IsOwner)
        {
            selectedWardrobeOption = outfitIndex + 1;
            localClothingVisible = clothingVisible;
            RefreshWardrobeStatus();
        }
    }

    [ClientRpc]
    private void EndSharedRestClientRpc(ClientRpcParams clientRpcParams = default)
    {
        localWardrobeSession = false;
        RestoreOriginalWardrobeStates();
        ClosePrompt();
        localWardrobeOptions.Clear();
        localWardrobePlayer = null;
        wardrobeStatusText = null;
    }

    private void CaptureWardrobeState(NetworkPlayer player)
    {
        Transform wardrobeRoot = FindWardrobeRoot(player);
        if (wardrobeRoot == null || originalWardrobeStates.ContainsKey(player.NetworkObjectId))
        {
            return;
        }

        bool[] activeStates = new bool[wardrobeRoot.childCount];
        for (int index = 0; index < wardrobeRoot.childCount; index++)
        {
            activeStates[index] = wardrobeRoot.GetChild(index).gameObject.activeSelf;
        }

        originalWardrobeStates.Add(player.NetworkObjectId, activeStates);
    }

    private void RestoreOriginalWardrobeStates()
    {
        foreach (KeyValuePair<ulong, bool[]> state in originalWardrobeStates)
        {
            if (!TryGetSpawnedPlayer(state.Key, out NetworkPlayer player))
            {
                continue;
            }

            Transform wardrobeRoot = FindWardrobeRoot(player);
            if (wardrobeRoot == null)
            {
                continue;
            }

            int childCount = Mathf.Min(wardrobeRoot.childCount, state.Value.Length);
            for (int index = 0; index < childCount; index++)
            {
                wardrobeRoot.GetChild(index).gameObject.SetActive(state.Value[index]);
            }
        }

        originalWardrobeStates.Clear();
    }

    private void OpenWardrobe(NetworkPlayer player)
    {
        Transform wardrobeRoot = FindWardrobeRoot(player);
        if (wardrobeRoot == null)
        {
            Debug.LogWarning($"No '{wardrobeRootName}' child found on {player.name}; private wardrobe controls are unavailable.");
            return;
        }

        localWardrobePlayer = player;
        localWardrobeOptions.Clear();
        int activeIndex = -1;
        for (int index = 0; index < wardrobeRoot.childCount; index++)
        {
            GameObject outfit = wardrobeRoot.GetChild(index).gameObject;
            localWardrobeOptions.Add(outfit);
            if (outfit.activeSelf)
            {
                activeIndex = index;
            }
        }

        selectedWardrobeOption = activeIndex + 1;
        localClothingVisible = activeIndex >= 0;
        CreateWardrobePanel();
        RefreshWardrobeStatus();
    }

    private void CreateWardrobePanel()
    {
        ClosePrompt();
        promptCanvas = new GameObject("TentWardrobe", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = promptCanvas.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        CanvasScaler scaler = promptCanvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        EnsureEventSystem();

        GameObject panel = CreateUiObject("Panel", promptCanvas.transform, typeof(Image));
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(520f, 230f);
        panel.GetComponent<Image>().color = new Color(0.08f, 0.1f, 0.09f, 0.96f);

        CreateLabel("Title", panel.transform, "Private wardrobe", new Vector2(0f, 72f), new Vector2(470f, 40f), 24);
        wardrobeStatusText = CreateLabel("OutfitStatus", panel.transform, string.Empty, new Vector2(0f, 28f), new Vector2(470f, 40f), 20);
        CreateWardrobeButton("Previous", "Previous", new Vector2(-180f, -48f), () => ChangeWardrobeSelection(-1));
        CreateWardrobeButton("ToggleClothing", "Clothes: On/Off", new Vector2(-60f, -48f), ToggleWardrobeClothing);
        CreateWardrobeButton("Next", "Next", new Vector2(60f, -48f), () => ChangeWardrobeSelection(1));
        CreateWardrobeButton("Close", "Close", new Vector2(180f, -48f), ClosePrompt);
    }

    private void CreateWardrobeButton(string objectName, string label, Vector2 position, System.Action action)
    {
        GameObject buttonObject = CreateUiObject(objectName, promptCanvas.transform.GetChild(0), typeof(Image), typeof(Button));
        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
        buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
        buttonRect.anchoredPosition = position;
        buttonRect.sizeDelta = new Vector2(110f, 46f);
        buttonObject.GetComponent<Image>().color = new Color(0.22f, 0.4f, 0.34f, 1f);
        buttonObject.GetComponent<Button>().onClick.AddListener(() => action());
        CreateLabel("Label", buttonObject.transform, label, Vector2.zero, new Vector2(104f, 40f), 16);
    }

    private void ChangeWardrobeSelection(int direction)
    {
        if (localWardrobePlayer == null || localWardrobeOptions.Count == 0)
        {
            return;
        }

        int optionCount = localWardrobeOptions.Count + 1;
        selectedWardrobeOption = (selectedWardrobeOption + direction + optionCount) % optionCount;
        localClothingVisible = selectedWardrobeOption > 0;
        SubmitWardrobeChange();
    }

    private void ToggleWardrobeClothing()
    {
        if (localWardrobePlayer == null || selectedWardrobeOption == 0)
        {
            return;
        }

        localClothingVisible = !localClothingVisible;
        SubmitWardrobeChange();
    }

    private void SubmitWardrobeChange()
    {
        if (!IsSpawned || localWardrobePlayer == null || !localWardrobePlayer.IsOwner)
        {
            return;
        }

        RequestWardrobeChangeServerRpc(
            localWardrobePlayer.NetworkObjectId,
            selectedWardrobeOption - 1,
            localClothingVisible);
    }

    private void RefreshWardrobeStatus()
    {
        if (wardrobeStatusText == null)
        {
            return;
        }

        string outfitName = selectedWardrobeOption == 0
            ? "No outfit"
            : localWardrobeOptions[selectedWardrobeOption - 1].name;
        wardrobeStatusText.text = $"{outfitName} | {(localClothingVisible ? "Visible" : "Hidden")}";
    }

    private Transform FindWardrobeRoot(NetworkPlayer player)
    {
        if (player == null || string.IsNullOrWhiteSpace(wardrobeRootName))
        {
            return null;
        }

        foreach (Transform child in player.GetComponentsInChildren<Transform>(true))
        {
            if (child.name == wardrobeRootName)
            {
                return child;
            }
        }

        return null;
    }

    private void CreatePrompt(string message, bool allowConsent)
    {
        ClosePrompt();
        promptCanvas = new GameObject("TentRestPrompt", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        Canvas canvas = promptCanvas.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        CanvasScaler scaler = promptCanvas.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1280f, 720f);
        EnsureEventSystem();

        GameObject panel = CreateUiObject("Panel", promptCanvas.transform, typeof(Image));
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(440f, 190f);
        panel.GetComponent<Image>().color = new Color(0.08f, 0.1f, 0.09f, 0.96f);

        CreateLabel("Message", panel.transform, message, new Vector2(0f, 34f), new Vector2(400f, 80f), 22);
        if (allowConsent)
        {
            CreateButton("Accept", panel.transform, "Accept", new Vector2(-95f, -55f), true);
            CreateButton("Decline", panel.transform, "Decline", new Vector2(95f, -55f), false);
        }
        else
        {
            CreateButton("Close", panel.transform, "Close", new Vector2(0f, -55f), false, true);
        }
    }

    private void CreateButton(string objectName, Transform parent, string label, Vector2 position, bool accepted, bool justClose = false)
    {
        GameObject buttonObject = CreateUiObject(objectName, parent, typeof(Image), typeof(Button));
        RectTransform buttonRect = buttonObject.GetComponent<RectTransform>();
        buttonRect.anchorMin = new Vector2(0.5f, 0.5f);
        buttonRect.anchorMax = new Vector2(0.5f, 0.5f);
        buttonRect.anchoredPosition = position;
        buttonRect.sizeDelta = new Vector2(150f, 48f);
        buttonObject.GetComponent<Image>().color = accepted
            ? new Color(0.22f, 0.48f, 0.32f, 1f)
            : new Color(0.48f, 0.25f, 0.23f, 1f);
        buttonObject.GetComponent<Button>().onClick.AddListener(() =>
        {
            ClosePrompt();
            if (!justClose && IsSpawned)
            {
                RespondToConsentServerRpc(accepted);
            }
        });
        CreateLabel("Label", buttonObject.transform, label, Vector2.zero, new Vector2(140f, 40f), 20);
    }

    private static Text CreateLabel(string objectName, Transform parent, string value, Vector2 position, Vector2 size, int fontSize)
    {
        GameObject labelObject = CreateUiObject(objectName, parent, typeof(Text));
        RectTransform labelRect = labelObject.GetComponent<RectTransform>();
        labelRect.anchorMin = new Vector2(0.5f, 0.5f);
        labelRect.anchorMax = new Vector2(0.5f, 0.5f);
        labelRect.anchoredPosition = position;
        labelRect.sizeDelta = size;
        Text label = labelObject.GetComponent<Text>();
        label.text = value;
        label.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
        label.fontSize = fontSize;
        label.alignment = TextAnchor.MiddleCenter;
        label.color = Color.white;
        label.horizontalOverflow = HorizontalWrapMode.Wrap;
        label.verticalOverflow = VerticalWrapMode.Overflow;
        label.raycastTarget = false;
        return label;
    }

    private static GameObject CreateUiObject(string objectName, Transform parent, params System.Type[] components)
    {
        GameObject uiObject = new(objectName, components);
        uiObject.transform.SetParent(parent, false);
        return uiObject;
    }

    private static void EnsureEventSystem()
    {
        if (EventSystem.current == null)
        {
            new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
        }
    }

    private void ClosePrompt()
    {
        if (promptCanvas != null)
        {
            Destroy(promptCanvas);
            promptCanvas = null;
        }
    }

    private IEnumerator RestoreMovementAfterDelay(ulong initiatorId, ulong partnerId, float duration)
    {
        yield return new WaitForSeconds(duration);
        if (TryGetSpawnedPlayer(initiatorId, out NetworkPlayer initiator)) SetMovement(initiator, true);
        if (TryGetSpawnedPlayer(partnerId, out NetworkPlayer partner)) SetMovement(partner, true);
    }

    private IEnumerator ResetRestStateAfterDelay(float duration)
    {
        yield return new WaitForSeconds(duration);
        if (!IsServer || !restInProgress)
        {
            yield break;
        }

        EndSharedRestClientRpc(TargetClients(activeRestInitiatorClientId, activeRestPartnerClientId));
        restInProgress = false;
        activeRestInitiatorClientId = 0;
        activeRestPartnerClientId = 0;
        activeRestInitiatorObjectId = 0;
        activeRestPartnerObjectId = 0;
    }

    private void MoveToAnchor(NetworkPlayer player, Transform anchor)
    {
        if (anchor != null)
        {
            player.transform.SetPositionAndRotation(anchor.position, anchor.rotation);
        }
    }

    private static void PlayRestAnimation(NetworkPlayer player, string triggerName)
    {
        Animator animator = player.GetComponentInChildren<Animator>();
        if (animator != null && !string.IsNullOrWhiteSpace(triggerName))
        {
            animator.SetTrigger(triggerName);
        }
    }

    private static void SetMovement(NetworkPlayer player, bool enabled)
    {
        PlayerController controller = player.GetComponent<PlayerController>();
        if (controller != null)
        {
            controller.MovementEnabled = enabled;
        }
    }

    private bool IsValidPair(NetworkPlayer initiator, NetworkPlayer partner)
    {
        return initiator != null && partner != null && initiator != partner &&
            initiator.IsSpawned && partner.IsSpawned &&
            initiator.TeamId.Value == partner.TeamId.Value &&
            IsNearTent(initiator) && IsNearTent(partner) &&
            !initiator.GetComponent<PlayerSurvival>().NetworkIsDowned.Value &&
            !partner.GetComponent<PlayerSurvival>().NetworkIsDowned.Value;
    }

    private bool IsNearTent(NetworkPlayer player)
    {
        return (player.transform.position - transform.position).sqrMagnitude <= interactionRadius * interactionRadius;
    }

    private bool TryGetPlayer(ulong clientId, out NetworkPlayer player)
    {
        player = null;
        return NetworkManager.ConnectedClients.TryGetValue(clientId, out NetworkClient client) &&
            client.PlayerObject != null && (player = client.PlayerObject.GetComponent<NetworkPlayer>()) != null;
    }

    private bool TryGetSpawnedPlayer(ulong networkObjectId, out NetworkPlayer player)
    {
        player = null;
        return NetworkManager != null &&
            NetworkManager.SpawnManager.SpawnedObjects.TryGetValue(networkObjectId, out NetworkObject playerObject) &&
            (player = playerObject.GetComponent<NetworkPlayer>()) != null;
    }

    private string GetPlayerNameByClientId(ulong clientId)
    {
        return TryGetPlayer(clientId, out NetworkPlayer player) ? GetPlayerName(player) : "Your partner";
    }

    private static string GetPlayerName(NetworkPlayer player)
    {
        string name = player.DisplayName.Value.ToString();
        return string.IsNullOrWhiteSpace(name) ? "Your partner" : name;
    }

    private static ClientRpcParams TargetClient(ulong clientId)
    {
        return new ClientRpcParams
        {
            Send = new ClientRpcSendParams { TargetClientIds = new[] { clientId } }
        };
    }

    private static ClientRpcParams TargetClients(ulong firstClientId, ulong secondClientId)
    {
        return new ClientRpcParams
        {
            Send = new ClientRpcSendParams { TargetClientIds = new[] { firstClientId, secondClientId } }
        };
    }

    private bool IsOwnerOfNearbyPlayer()
    {
        foreach (NetworkPlayer player in nearbyPlayers)
        {
            if (player != null && player.IsOwner)
            {
                return true;
            }
        }

        return false;
    }

    private void ClearConsent()
    {
        consentPending = false;
        pendingInitiatorClientId = 0;
        pendingPartnerClientId = 0;
        expectedConsentClientId = 0;
        consentExpiresAt = 0f;
    }

    public override void OnNetworkDespawn()
    {
        ClearConsent();
        localWardrobeSession = false;
        RestoreOriginalWardrobeStates();
        ClosePrompt();
        nearbyPlayers.Clear();
    }
}