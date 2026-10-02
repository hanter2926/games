using Unity.Netcode;
using UnityEngine;

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

        if (Physics.Raycast(origin, direction, out RaycastHit hit, range, hitMask, QueryTriggerInteraction.Ignore))
        {
            NetworkPlayer target = hit.collider.GetComponentInParent<NetworkPlayer>();
            if (target != null && target != GetComponent<NetworkPlayer>())
            {
                target.ApplyDamageServer(damage);
            }
        }

        PlayFireClientRpc();
    }

    [ClientRpc]
    private void PlayFireClientRpc()
    {
        if (animator != null && !string.IsNullOrWhiteSpace(fireTrigger))
        {
            animator.SetTrigger(fireTrigger);
        }
    }
}