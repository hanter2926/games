using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(PlayerSurvival))]
[RequireComponent(typeof(NetworkObject))]
public sealed class PlayerCampGuard : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerSurvival survival;
    [SerializeField] private PlayerController controller;
    [SerializeField] private Animator animator;
    [SerializeField] private GameObject guardIndicator;

    [Header("Guard State")]
    [SerializeField] private KeyCode guardShortcut = KeyCode.G;
    [SerializeField] private string guardAnimatorParameter = "IsOnGuardDuty";
    [Min(0f)] [SerializeField] private float guardAlertRadius = 35f;
    [SerializeField] private LayerMask guardAlertLayer = ~0;
    public UnityEvent guardDutyStarted;
    public UnityEvent guardDutyStopped;
    public UnityEvent guardAlertRaised;
    public UnityEvent guardAlertCleared;

    public NetworkVariable<bool> NetworkAlertActive = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private CampArea camp;
    private bool isOnGuardDuty;

    public bool NetworkAuthorityControlled { get; set; }

    public bool IsInsideCamp => camp != null;
    public bool IsOnGuardDuty => isOnGuardDuty;
    public CampArea CurrentCamp => camp;

    private void Awake()
    {
        survival = survival != null ? survival : GetComponent<PlayerSurvival>();
        controller = controller != null ? controller : GetComponent<PlayerController>();
    }

    public override void OnNetworkSpawn()
    {
        NetworkAlertActive.OnValueChanged += OnAlertStateChanged;
        ApplyAlertState(NetworkAlertActive.Value);
    }

    public override void OnNetworkDespawn()
    {
        NetworkAlertActive.OnValueChanged -= OnAlertStateChanged;
    }

    private void Update()
    {
        if (NetworkAuthorityControlled)
        {
            return;
        }

        if (Input.GetKeyDown(guardShortcut))
        {
            ToggleGuardDuty();
        }

        if (camp != null && !isOnGuardDuty)
        {
            camp.Recover(survival, Time.deltaTime);
        }
    }

    public void ToggleGuardDuty()
    {
        if (!isOnGuardDuty && camp == null)
        {
            Debug.Log("Enter a CampArea before starting guard duty.");
            return;
        }

        SetGuardDuty(!isOnGuardDuty);
    }

    public void SetGuardDuty(bool enabled)
    {
        isOnGuardDuty = enabled;
        if (controller != null)
        {
            controller.MovementEnabled = !enabled;
        }

        if (animator != null && !string.IsNullOrWhiteSpace(guardAnimatorParameter))
        {
            animator.SetBool(guardAnimatorParameter, enabled);
        }

        if (guardIndicator != null)
        {
            guardIndicator.SetActive(enabled);
        }

        if (!enabled && IsServer)
        {
            NetworkAlertActive.Value = false;
        }

        if (enabled)
        {
            guardDutyStarted?.Invoke();
            Debug.Log("Guard duty active: teammates can rest in camp.");
        }
        else
        {
            guardDutyStopped?.Invoke();
            Debug.Log("Guard duty ended.");
        }
    }

    public void EvaluateGuardAlert()
    {
        if (!IsServer || !isOnGuardDuty || guardAlertRadius <= 0f)
        {
            return;
        }

        NetworkPlayer ownPlayer = GetComponent<NetworkPlayer>();
        bool enemyNearby = false;
        Collider[] nearbyColliders = Physics.OverlapSphere(transform.position, guardAlertRadius, guardAlertLayer, QueryTriggerInteraction.Ignore);
        foreach (Collider nearbyCollider in nearbyColliders)
        {
            NetworkPlayer nearbyPlayer = nearbyCollider.GetComponentInParent<NetworkPlayer>();
            if (nearbyPlayer == null || nearbyPlayer == ownPlayer || !nearbyPlayer.IsSpawned)
            {
                continue;
            }

            if (ownPlayer == null || nearbyPlayer.TeamId.Value != ownPlayer.TeamId.Value)
            {
                enemyNearby = true;
                break;
            }
        }

        if (NetworkAlertActive.Value != enemyNearby)
        {
            NetworkAlertActive.Value = enemyNearby;
        }
    }

    private void OnAlertStateChanged(bool previousValue, bool newValue)
    {
        ApplyAlertState(newValue);
    }

    private void ApplyAlertState(bool alertActive)
    {
        if (alertActive)
        {
            guardAlertRaised?.Invoke();
            Debug.LogWarning("Guard alert: an enemy player is approaching the camp.");
        }
        else
        {
            guardAlertCleared?.Invoke();
        }
    }

    public void SetCamp(CampArea enteredCamp)
    {
        camp = enteredCamp;
    }

    public void ClearCamp(CampArea exitedCamp)
    {
        if (camp != exitedCamp)
        {
            return;
        }

        camp = null;
        if (isOnGuardDuty)
        {
            SetGuardDuty(false);
        }
    }
}