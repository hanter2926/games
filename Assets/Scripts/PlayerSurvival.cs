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
    [Min(1f)] public float maxHunger = 100f;
    public float currentHunger;
    [Min(0f)] public float hungerDrainPerSecond = 0.5f;
    [Min(0f)] public float starvationDamagePerSecond = 5f;
    public float hungerThreshold = 20f;
    private bool starvingWarningShown;

    public NetworkVariable<float> NetworkHealth = new(
        100f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);
    public NetworkVariable<float> NetworkEnergy = new(
        100f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);
    public NetworkVariable<float> NetworkHunger = new(
        100f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);
    public NetworkVariable<int> NetworkMoney = new(
        250,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);
    public NetworkVariable<int> NetworkMedicalKits = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);
    public NetworkVariable<bool> NetworkIsDowned = new(
        false,
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
        currentHunger = maxHunger;
        RefreshUI();
    }

    public override void OnNetworkSpawn()
    {
        NetworkAuthorityControlled = true;
        NetworkHealth.OnValueChanged += OnNetworkHealthChanged;
        NetworkEnergy.OnValueChanged += OnNetworkEnergyChanged;
        NetworkHunger.OnValueChanged += OnNetworkHungerChanged;

        if (IsServer)
        {
            NetworkHealth.Value = maxHealth;
            NetworkEnergy.Value = maxEnergy;
            NetworkHunger.Value = maxHunger;
            NetworkMoney.Value = 250;
            NetworkMedicalKits.Value = 0;
        }

        currentHealth = NetworkHealth.Value;
        currentEnergy = NetworkEnergy.Value;
        currentHunger = NetworkHunger.Value;
        RefreshUI();
    }

    public override void OnNetworkDespawn()
    {
        NetworkHealth.OnValueChanged -= OnNetworkHealthChanged;
        NetworkEnergy.OnValueChanged -= OnNetworkEnergyChanged;
        NetworkHunger.OnValueChanged -= OnNetworkHungerChanged;
    }

    void Update()
    {
        if (NetworkAuthorityControlled)
        {
            RefreshUI();
            return;
        }

        SimulateNeeds(Time.deltaTime);

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
        if (currentHealth > 0f)
        {
            SetDownedState(false);
        }
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
        if (currentHealth <= 0f)
        {
            SetDownedState(true);
        }
        SyncNetworkState();
        RefreshUI();
    }

    public void SimulateEnergyDrain(float deltaTime)
    {
        SimulateNeeds(deltaTime);
    }

    public void SimulateNeeds(float deltaTime)
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

        currentHunger = Mathf.Max(0f, currentHunger - hungerDrainPerSecond * deltaTime);
        if (currentHunger <= 0f)
        {
            if (!starvingWarningShown)
            {
                starvingWarningShown = true;
                PlayerStarving();
            }

            currentHealth = Mathf.Max(0f, currentHealth - starvationDamagePerSecond * deltaTime);
            if (currentHealth <= 0f)
            {
                SetDownedState(true);
            }
        }

        SyncNetworkState();
        RefreshUI();
    }

    public void ApplyWeatherExposureServer(float energyDrain)
    {
        if (!IsServer || energyDrain <= 0f)
        {
            return;
        }

        currentEnergy = Mathf.Max(0f, currentEnergy - energyDrain);
        SyncNetworkState();
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

    private void OnNetworkHungerChanged(float previousValue, float newValue)
    {
        currentHunger = newValue;
        RefreshUI();
    }

    public void ConsumeFoodServer(float hungerRestored, float energyRestored)
    {
        if (IsSpawned && !IsServer)
        {
            return;
        }

        currentHunger = Mathf.Clamp(currentHunger + Mathf.Max(0f, hungerRestored), 0f, maxHunger);
        currentEnergy = Mathf.Clamp(currentEnergy + Mathf.Max(0f, energyRestored), 0f, maxEnergy);
        starvingWarningShown = false;
        SyncNetworkState();
    }

    public void HealOrReviveServer(float healthAmount, bool revive)
    {
        if (IsSpawned && !IsServer)
        {
            return;
        }

        currentHealth = Mathf.Clamp(currentHealth + Mathf.Max(0f, healthAmount), 0f, maxHealth);
        if (revive && currentHealth > 0f)
        {
            SetDownedState(false);
        }

        SyncNetworkState();
    }

    public bool TrySpendMoneyServer(int amount)
    {
        if (amount < 0 || (IsSpawned && !IsServer) || NetworkMoney.Value < amount)
        {
            return false;
        }

        NetworkMoney.Value -= amount;
        return true;
    }

    public void AddMoneyServer(int amount)
    {
        if (amount < 0 || (IsSpawned && !IsServer))
        {
            return;
        }

        NetworkMoney.Value = Mathf.Min(int.MaxValue, NetworkMoney.Value + amount);
    }

    public void AddMedicalKitsServer(int amount)
    {
        if (amount < 0 || (IsSpawned && !IsServer))
        {
            return;
        }

        NetworkMedicalKits.Value = Mathf.Min(int.MaxValue, NetworkMedicalKits.Value + amount);
    }

    public bool TryUseMedicalKitServer()
    {
        if ((IsSpawned && !IsServer) || NetworkMedicalKits.Value <= 0)
        {
            return false;
        }

        NetworkMedicalKits.Value--;
        return true;
    }

    private void SetDownedState(bool downed)
    {
        if (IsSpawned && IsServer)
        {
            NetworkIsDowned.Value = downed;
        }
    }

    private void SyncNetworkState()
    {
        if (IsServer)
        {
            NetworkHealth.Value = currentHealth;
            NetworkEnergy.Value = currentEnergy;
            NetworkHunger.Value = currentHunger;
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