using System.Collections;
using Unity.Netcode;
using UnityEngine;

public enum CookingRecipeType : byte
{
    Vegetarian,
    NonVegetarian
}

public enum CookingState : byte
{
    Ready,
    Cooking,
    NoIngredients,
    Complete
}

[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(PlayerSurvival))]
[RequireComponent(typeof(DeliveryAndHospitalSystem))]
public sealed class CampCookingSystem : NetworkBehaviour
{
    [Header("Portable Kit")]
    [SerializeField] private bool startsWithPortableCookingKit = true;
    [SerializeField] private float cookingDurationSeconds = 4f;

    [Header("Vegetarian Recipe")]
    [SerializeField] private int vegetarianIngredientCost = 2;
    [SerializeField] private float vegetarianHealth = 12f;
    [SerializeField] private float vegetarianEnergy = 30f;
    [SerializeField] private float vegetarianHunger = 35f;

    [Header("Non-Vegetarian Recipe")]
    [SerializeField] private int nonVegetarianIngredientCost = 3;
    [SerializeField] private float nonVegetarianHealth = 24f;
    [SerializeField] private float nonVegetarianEnergy = 45f;
    [SerializeField] private float nonVegetarianHunger = 28f;

    [Header("Partner Sharing")]
    [SerializeField] private float sharingDistance = 5f;

    public NetworkVariable<bool> HasPortableCookingKit = new(
        true,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);
    public NetworkVariable<CookingRecipeType> SelectedRecipe = new(
        CookingRecipeType.Vegetarian,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);
    public NetworkVariable<CookingState> State = new(
        CookingState.Ready,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);
    public NetworkVariable<int> PreparedMealCount = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private PlayerSurvival survival;
    private DeliveryAndHospitalSystem deliverySystem;
    private Coroutine cookingRoutine;

    private void Awake()
    {
        survival = GetComponent<PlayerSurvival>();
        deliverySystem = GetComponent<DeliveryAndHospitalSystem>();
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            HasPortableCookingKit.Value = startsWithPortableCookingKit;
            State.Value = CookingState.Ready;
        }
    }

    public override void OnNetworkDespawn()
    {
        if (cookingRoutine != null)
        {
            StopCoroutine(cookingRoutine);
            cookingRoutine = null;
        }
    }

    public void CookVegetarian()
    {
        RequestCook(CookingRecipeType.Vegetarian);
    }

    public void CookNonVegetarian()
    {
        RequestCook(CookingRecipeType.NonVegetarian);
    }

    public void RequestCook(CookingRecipeType recipe)
    {
        if (IsOwner)
        {
            RequestCookServerRpc(recipe);
        }
    }

    [ServerRpc]
    private void RequestCookServerRpc(CookingRecipeType recipe)
    {
        if (!HasPortableCookingKit.Value || State.Value == CookingState.Cooking)
        {
            return;
        }

        int ingredientCost = GetIngredientCost(recipe);
        if (deliverySystem.NetworkIngredients.Value < ingredientCost)
        {
            State.Value = CookingState.NoIngredients;
            return;
        }

        deliverySystem.NetworkIngredients.Value -= ingredientCost;
        SelectedRecipe.Value = recipe;
        State.Value = CookingState.Cooking;
        cookingRoutine = StartCoroutine(FinishCookingServer(recipe));
    }

    private IEnumerator FinishCookingServer(CookingRecipeType recipe)
    {
        yield return new WaitForSeconds(Mathf.Max(0.1f, cookingDurationSeconds));
        cookingRoutine = null;

        if (!IsServer || !IsSpawned || !HasPortableCookingKit.Value)
        {
            yield break;
        }

        SelectedRecipe.Value = recipe;
        PreparedMealCount.Value = Mathf.Min(99, PreparedMealCount.Value + 1);
        State.Value = CookingState.Complete;
    }

    public void SharePreparedMealWithPlayer(ulong teammateClientId)
    {
        if (IsOwner)
        {
            SharePreparedMealServerRpc(teammateClientId);
        }
    }

    [ServerRpc]
    private void SharePreparedMealServerRpc(ulong teammateClientId)
    {
        if (PreparedMealCount.Value <= 0 || NetworkManager.Singleton == null || !NetworkManager.Singleton.ConnectedClients.TryGetValue(teammateClientId, out NetworkClient teammateClient) || teammateClient.PlayerObject == null)
        {
            return;
        }

        NetworkPlayer recipient = teammateClient.PlayerObject.GetComponent<NetworkPlayer>();
        NetworkPlayer owner = GetComponent<NetworkPlayer>();
        if (recipient == null || owner == null || recipient.TeamId.Value != owner.TeamId.Value || Vector3.Distance(transform.position, recipient.transform.position) > sharingDistance)
        {
            return;
        }

        PlayerSurvival recipientSurvival = recipient.GetComponent<PlayerSurvival>();
        if (recipientSurvival == null)
        {
            return;
        }

        PreparedMealCount.Value--;
        CookingRecipeType recipe = SelectedRecipe.Value;
        recipientSurvival.Recover(GetHealthValue(recipe), GetEnergyValue(recipe));
        recipientSurvival.ConsumeFoodServer(GetHungerValue(recipe), 0f);
        State.Value = PreparedMealCount.Value > 0 ? CookingState.Complete : CookingState.Ready;
    }

    public void SetPortableCookingKitServer(bool enabled)
    {
        if (IsServer)
        {
            HasPortableCookingKit.Value = enabled;
        }
    }

    private int GetIngredientCost(CookingRecipeType recipe)
    {
        return recipe == CookingRecipeType.Vegetarian ? vegetarianIngredientCost : nonVegetarianIngredientCost;
    }

    private float GetHealthValue(CookingRecipeType recipe)
    {
        return recipe == CookingRecipeType.Vegetarian ? vegetarianHealth : nonVegetarianHealth;
    }

    private float GetEnergyValue(CookingRecipeType recipe)
    {
        return recipe == CookingRecipeType.Vegetarian ? vegetarianEnergy : nonVegetarianEnergy;
    }

    private float GetHungerValue(CookingRecipeType recipe)
    {
        return recipe == CookingRecipeType.Vegetarian ? vegetarianHunger : nonVegetarianHunger;
    }
}
