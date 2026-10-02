using UnityEngine;

[RequireComponent(typeof(Collider))]
public sealed class CampArea : MonoBehaviour
{
    [Header("Recovery")]
    [Min(0f)] public float healthRecoveryPerSecond = 4f;
    [Min(0f)] public float energyRecoveryPerSecond = 8f;

    private void Reset()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        PlayerCampGuard player = other.GetComponentInParent<PlayerCampGuard>();
        if (player != null)
        {
            player.SetCamp(this);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        PlayerCampGuard player = other.GetComponentInParent<PlayerCampGuard>();
        if (player != null)
        {
            player.ClearCamp(this);
        }
    }

    public void Recover(PlayerSurvival survival, float deltaTime)
    {
        survival.Recover(healthRecoveryPerSecond * deltaTime, energyRecoveryPerSecond * deltaTime);
    }
}