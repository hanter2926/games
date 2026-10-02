using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(NetworkObject))]
public class PlayerSurvival : NetworkBehaviour
{
    [Header("Health")]
    public float maxHealth = 100f;
    public float currentHealth;

    [Header("Energy & Hunger Stats")]
    public float maxEnergy = 100f;
    public float currentEnergy;
    public float energyDrainRate = 2f; // Har second kitni energy kam hogi
    public float hungerThreshold = 20f; // Jab isse kam energy ho toh warning milegi
    private bool starvingWarningShown;

    public NetworkVariable<float> NetworkHealth = new(
        100f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);
    public NetworkVariable<float> NetworkEnergy = new(
        100f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    public bool NetworkAuthorityControlled { get; set; }

    [Header("UI Reference (Optional)")]
    public Slider healthSlider;
    public Slider energySlider; // Screen par energy bar dikhane ke liye

    void Start()
    {
        currentHealth = maxHealth;
        currentEnergy = maxEnergy;
        RefreshUI();
    }

    public override void OnNetworkSpawn()
    {
        NetworkAuthorityControlled = true;
        NetworkHealth.OnValueChanged += OnNetworkHealthChanged;
        NetworkEnergy.OnValueChanged += OnNetworkEnergyChanged;

        if (IsServer)
        {
            NetworkHealth.Value = maxHealth;
            NetworkEnergy.Value = maxEnergy;
        }

        currentHealth = NetworkHealth.Value;
        currentEnergy = NetworkEnergy.Value;
        RefreshUI();
    }

    public override void OnNetworkDespawn()
    {
        NetworkHealth.OnValueChanged -= OnNetworkHealthChanged;
        NetworkEnergy.OnValueChanged -= OnNetworkEnergyChanged;
    }

    void Update()
    {
        if (NetworkAuthorityControlled)
        {
            RefreshUI();
            return;
        }

        // Energy ko dheere-dheere kam karna (Time ke sath)
        if (currentEnergy > 0)
        {
            currentEnergy -= energyDrainRate * Time.deltaTime;
        }
        else
        {
            currentEnergy = 0;
            if (!starvingWarningShown)
            {
                starvingWarningShown = true;
                PlayerStarving();
            }
        }

        RefreshUI();

        // PC par 'E' key dabane par khana khane ka test
        if (Input.GetKeyDown(KeyCode.E))
        {
            EatFood(50f); // 50 energy wapas mil jayegi
        }
    }

    // Khana khane ka function
    public void EatFood(float foodAmount)
    {
        if (IsSpawned && !IsServer)
        {
            RequestEatFoodServerRpc(Mathf.Clamp(foodAmount, 0f, 100f));
            return;
        }

        EatFoodInternal(foodAmount);
    }

    [ServerRpc]
    private void RequestEatFoodServerRpc(float foodAmount)
    {
        EatFoodInternal(Mathf.Clamp(foodAmount, 0f, 100f));
    }

    private void EatFoodInternal(float foodAmount)
    {
        currentEnergy += foodAmount;
        currentEnergy = Mathf.Clamp(currentEnergy, 0, maxEnergy);
        starvingWarningShown = false;
        SyncNetworkState();
        Debug.Log("Yum! Khana kha liya. Current Energy: " + currentEnergy);
    }

    public void Recover(float healthAmount, float energyAmount)
    {
        if (IsSpawned && !IsServer)
        {
            return;
        }

        currentHealth = Mathf.Clamp(currentHealth + Mathf.Max(0f, healthAmount), 0f, maxHealth);
        currentEnergy = Mathf.Clamp(currentEnergy + Mathf.Max(0f, energyAmount), 0f, maxEnergy);
        if (currentEnergy > 0f)
        {
            starvingWarningShown = false;
        }
        SyncNetworkState();
        RefreshUI();
    }

    public void ApplyDamage(float damage)
    {
        if (IsSpawned && !IsServer)
        {
            return;
        }

        currentHealth = Mathf.Clamp(currentHealth - Mathf.Max(0f, damage), 0f, maxHealth);
        SyncNetworkState();
        RefreshUI();
    }

    public void SimulateEnergyDrain(float deltaTime)
    {
        if (currentEnergy <= 0f)
        {
            currentEnergy = 0f;
            if (!starvingWarningShown)
            {
                starvingWarningShown = true;
                PlayerStarving();
            }
        }
        else
        {
            currentEnergy = Mathf.Max(0f, currentEnergy - energyDrainRate * deltaTime);
        }

        SyncNetworkState();
        RefreshUI();
    }

    private void OnNetworkHealthChanged(float previousValue, float newValue)
    {
        currentHealth = newValue;
        RefreshUI();
    }

    private void OnNetworkEnergyChanged(float previousValue, float newValue)
    {
        currentEnergy = newValue;
        RefreshUI();
    }

    private void SyncNetworkState()
    {
        if (IsServer)
        {
            NetworkHealth.Value = currentHealth;
            NetworkEnergy.Value = currentEnergy;
        }
    }

    private void RefreshUI()
    {
        if (healthSlider != null && maxHealth > 0f)
        {
            healthSlider.value = currentHealth / maxHealth;
        }

        if (energySlider != null && maxEnergy > 0f)
        {
            energySlider.value = currentEnergy / maxEnergy;
        }
    }

    // Jab energy bilkul khatam ho jaye
    void PlayerStarving()
    {
        Debug.Log("Warning: Character bhookh se kamzor ho raha hai! Jaldi khana dhoondo.");
        // Yahan aap player ki health kam karne ka code bhi jodh sakte hain
    }
}