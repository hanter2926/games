using Unity.Netcode;
using UnityEngine;
using UnityEngine.Events;

[RequireComponent(typeof(NetworkObject))]
public sealed class NetworkPlayerCombat : NetworkBehaviour
{
    [Header("Weapon")]
    [SerializeField] private Camera ownerCamera;
    [SerializeField] private Transform muzzle;
    [SerializeField] private float damage = 25f;
    [SerializeField] private float range = 150f;
    [SerializeField] private LayerMask hitMask = ~0;
    [SerializeField] private string fireTrigger = "Fire";
    [SerializeField] private Animator animator;
    [SerializeField] private ParticleSystem muzzleFlash;
    [SerializeField] private GameObject hitMarker;
    [SerializeField] private float hitMarkerDuration = 0.12f;
    public UnityEvent hitConfirmed;


    public void Fire()
    {
        if (!IsOwner)
        {
            return;
        }

        Transform origin = muzzle != null ? muzzle : transform;
        Vector3 direction = ownerCamera != null ? ownerCamera.transform.forward : transform.forward;
        FireServerRpc(origin.position, direction.normalized);
    }

    [ServerRpc]
    private void FireServerRpc(Vector3 requestedOrigin, Vector3 direction)
    {
        Vector3 origin = muzzle != null ? muzzle.position : transform.position;
        if (Vector3.Distance(origin, requestedOrigin) > 3f || direction.sqrMagnitude < 0.9f)
        {
            return;
        }

        bool hitConfirmedOnServer = false;
        if (Physics.Raycast(origin, direction, out RaycastHit hit, range, hitMask, QueryTriggerInteraction.Ignore))
        {
            NetworkPlayer target = hit.collider.GetComponentInParent<NetworkPlayer>();
            if (target != null && target != GetComponent<NetworkPlayer>())
            {
                hitConfirmedOnServer = target.TryApplyDamageFromPlayerServer(GetComponent<NetworkPlayer>(), damage);
            }
        }

        PlayFireClientRpc();
        if (hitConfirmedOnServer)
        {
            PlayMuzzleFlashClientRpc();
            PlayHitMarkerClientRpc(new ClientRpcParams
            {
                Send = new ClientRpcSendParams
                {
                    TargetClientIds = new[] { OwnerClientId }
                }
            });
        }
    }

    [ClientRpc]
    private void PlayFireClientRpc()
    {
        if (animator != null && !string.IsNullOrWhiteSpace(fireTrigger))
        {
            animator.SetTrigger(fireTrigger);
        }
    }

    [ClientRpc]
    private void PlayMuzzleFlashClientRpc()
    {
        if (muzzleFlash != null)
        {
            muzzleFlash.Play();
        }
    }

    [ClientRpc]
    private void PlayHitMarkerClientRpc(ClientRpcParams clientRpcParams = default)
    {
        hitConfirmed?.Invoke();
        if (hitMarker != null)
        {
            StartCoroutine(ShowHitMarker());
        }
    }

    private System.Collections.IEnumerator ShowHitMarker()
    {
        hitMarker.SetActive(true);
        yield return new WaitForSeconds(Mathf.Max(0f, hitMarkerDuration));
        hitMarker.SetActive(false);
    }
}