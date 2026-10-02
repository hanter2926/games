using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(PlayerSurvival))]
public sealed class PlayerCampGuard : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private PlayerSurvival survival;
    [SerializeField] private PlayerController controller;
    [SerializeField] private Animator animator;
    [SerializeField] private GameObject guardIndicator;

    [Header("Guard State")]
    [SerializeField] private KeyCode guardShortcut = KeyCode.G;
    [SerializeField] private string guardAnimatorParameter = "IsOnGuardDuty";
    public UnityEvent guardDutyStarted;
    public UnityEvent guardDutyStopped;

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