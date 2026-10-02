using System.Collections.Generic;
using Unity.Collections;
using Unity.Netcode;
using UnityEngine;

public enum DeliveryItemType : byte
{
    CookedMeal,
    MedicalKit,
    GeneralSupplies
}

public enum DeliveryOrderState : byte
{
    None,
    Ordered,
    Accepted,
    Delivered,
    Cancelled
}

[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(PlayerSurvival))]
public sealed class DeliveryAndHospitalSystem : NetworkBehaviour
{
    private sealed class DeliveryOrder
    {
        public int id;
        public ulong recipientClientId;
        public ulong courierClientId;
        public DeliveryItemType item;
        public DeliveryOrderState state;
    }

    [Header("Economy")]
    [SerializeField] private int cookedMealPrice = 20;
    [SerializeField] private int medicalKitPrice = 60;
    [SerializeField] private int generalSuppliesPrice = 35;
    [SerializeField] private int mealIngredientCost = 2;
    [SerializeField] private int mealHungerRestored = 35;
    [SerializeField] private int mealEnergyRestored = 30;
    [SerializeField] private float deliveryReward = 15f;

    [Header("Hospital")]
    [SerializeField] private LayerMask hospitalZoneLayer;
    [SerializeField] private float hospitalCheckRadius = 3f;
    [SerializeField] private string hospitalSpawnTag = "HospitalSpawn";
    [SerializeField] private float hospitalHealAmount = 100f;
    [SerializeField] private bool reviveAtHospital = true;

    [Header("Delivery")]
    [SerializeField] private float deliveryDistance = 4f;

    public NetworkVariable<bool> IsDeliveryBoy = new(
        false,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);
    public NetworkVariable<int> NetworkIngredients = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);
    public NetworkVariable<int> NetworkSupplies = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);
    public NetworkVariable<int> ActiveOrderId = new(
        0,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);
    public NetworkVariable<ulong> OrderRecipientClientId = new(
        ulong.MaxValue,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);
    public NetworkVariable<DeliveryItemType> ActiveOrderItem = new(
        DeliveryItemType.CookedMeal,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);
    public NetworkVariable<DeliveryOrderState> ActiveOrderState = new(
        DeliveryOrderState.None,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);
    public NetworkVariable<FixedString128Bytes> DeliveryStatus = new(
        default,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private static readonly Dictionary<int, DeliveryOrder> serverOrders = new();
    private static int nextOrderId = 1;
    private PlayerSurvival survival;

    private void Awake()
    {
        survival = GetComponent<PlayerSurvival>();
    }

    public override void OnNetworkSpawn()
    {
        if (IsServer)
        {
            SetStatus("Ready");
        }
    }

    public override void OnNetworkDespawn()
    {
        if (!IsServer)
        {
            return;
        }

        List<int> ordersToRemove = new();
        foreach (KeyValuePair<int, DeliveryOrder> entry in serverOrders)
        {
            DeliveryOrder order = entry.Value;
            if (order.recipientClientId == OwnerClientId || order.courierClientId == OwnerClientId)
            {
                ordersToRemove.Add(entry.Key);
            }
        }

        foreach (int orderId in ordersToRemove)
        {
            serverOrders.Remove(orderId);
        }
    }

    public void GatherIngredients(int amount = 1)
    {
        if (IsOwner)
        {
            GatherIngredientsServerRpc(Mathf.Clamp(amount, 1, 10));
        }
    }

    [ServerRpc]
    private void GatherIngredientsServerRpc(int amount)
    {
        NetworkIngredients.Value = Mathf.Min(999, NetworkIngredients.Value + amount);
        SetStatus("Ingredients gathered");
    }

    public void CookMeal()
    {
        if (IsOwner)
        {
            CookMealServerRpc();
        }
    }

    [ServerRpc]
    private void CookMealServerRpc()
    {
        if (NetworkIngredients.Value < mealIngredientCost)
        {
            SetStatus("Need more ingredients");
            return;
        }

        NetworkIngredients.Value -= mealIngredientCost;
        survival.ConsumeFoodServer(mealHungerRestored, mealEnergyRestored);
        SetStatus("Meal cooked");
    }

    public void BuySupplies(DeliveryItemType item)
    {
        if (IsOwner)
        {
            BuySuppliesServerRpc(item);
        }
    }

    [ServerRpc]
    private void BuySuppliesServerRpc(DeliveryItemType item)
    {
        int price = GetPrice(item);
        if (!survival.TrySpendMoneyServer(price))
        {
            SetStatus("Not enough money");
            return;
        }

        GrantDeliveredItem(item);
        SetStatus(item + " purchased");
    }

    public void OrderDelivery(DeliveryItemType item)
    {
        if (IsOwner)
        {
            RequestDeliveryOrderServerRpc(item);
        }
    }

    [ServerRpc]
    private void RequestDeliveryOrderServerRpc(DeliveryItemType item)
    {
        CreateOrderForRecipientServer(OwnerClientId, item);
    }

    public int CreateOrderForPlayerServer(ulong recipientClientId, DeliveryItemType item)
    {
        if (!IsServer || NetworkManager.Singleton == null || !NetworkManager.Singleton.ConnectedClients.ContainsKey(recipientClientId))
        {
            return 0;
        }

        return CreateOrderForRecipientServer(recipientClientId, item);
    }

    private int CreateOrderForRecipientServer(ulong recipientClientId, DeliveryItemType item)
    {
        NetworkPlayer recipientPlayer = GetPlayer(recipientClientId);
        if (recipientPlayer == null)
        {
            return 0;
        }

        PlayerSurvival recipientSurvival = recipientPlayer.GetComponent<PlayerSurvival>();
        if (!recipientSurvival.TrySpendMoneyServer(GetPrice(item)))
        {
            SetStatusForPlayer(recipientClientId, "Not enough money for order");
            return 0;
        }

        int orderId = nextOrderId++;
        serverOrders[orderId] = new DeliveryOrder
        {
            id = orderId,
            recipientClientId = recipientClientId,
            courierClientId = ulong.MaxValue,
            item = item,
            state = DeliveryOrderState.Ordered
        };

        SetOrderState(recipientPlayer, serverOrders[orderId], "Order placed");
        return orderId;
    }

    public void AcceptDelivery(int orderId)
    {
        if (IsOwner)
        {
            AcceptDeliveryServerRpc(orderId);
        }
    }

    [ServerRpc]
    private void AcceptDeliveryServerRpc(int orderId)
    {
        if (!IsDeliveryBoy.Value || !serverOrders.TryGetValue(orderId, out DeliveryOrder order) || order.state != DeliveryOrderState.Ordered)
        {
            return;
        }

        order.courierClientId = OwnerClientId;
        order.state = DeliveryOrderState.Accepted;
        SetOrderState(GetPlayer(order.recipientClientId), order, "Delivery accepted");
        SetOrderState(GetPlayer(order.courierClientId), order, "Deliver package to the recipient");
    }

    public void CompleteDelivery()
    {
        if (IsOwner)
        {
            CompleteDeliveryServerRpc();
        }
    }

    public bool CompleteNpcDeliveryServer(int orderId, Transform npcCourier)
    {
        if (!IsServer || npcCourier == null || !serverOrders.TryGetValue(orderId, out DeliveryOrder order) || order.state != DeliveryOrderState.Ordered)
        {
            return false;
        }

        NetworkPlayer recipient = GetPlayer(order.recipientClientId);
        if (recipient == null || Vector3.Distance(npcCourier.position, recipient.transform.position) > deliveryDistance)
        {
            return false;
        }

        DeliveryAndHospitalSystem recipientSystem = recipient.GetComponent<DeliveryAndHospitalSystem>();
        recipientSystem.GrantDeliveredItem(order.item);
        order.state = DeliveryOrderState.Delivered;
        SetOrderState(recipient, order, "NPC delivery received");
        serverOrders.Remove(order.id);
        return true;
    }

    [ServerRpc]
    private void CompleteDeliveryServerRpc()
    {
        if (!IsDeliveryBoy.Value || !serverOrders.TryGetValue(ActiveOrderId.Value, out DeliveryOrder order) || order.courierClientId != OwnerClientId || order.state != DeliveryOrderState.Accepted)
        {
            return;
        }

        NetworkPlayer recipient = GetPlayer(order.recipientClientId);
        if (recipient == null || Vector3.Distance(transform.position, recipient.transform.position) > deliveryDistance)
        {
            SetStatus("Recipient is too far away");
            return;
        }

        DeliveryAndHospitalSystem recipientSystem = recipient.GetComponent<DeliveryAndHospitalSystem>();
        recipientSystem.GrantDeliveredItem(order.item);
        survival.AddMoneyServer(Mathf.RoundToInt(deliveryReward));
        order.state = DeliveryOrderState.Delivered;
        SetOrderState(recipient, order, "Delivery received");
        SetStatus("Delivery complete");
        serverOrders.Remove(order.id);
    }

    public void RequestHospitalTreatment()
    {
        if (IsOwner)
        {
            RequestHospitalTreatmentServerRpc();
        }
    }

    [ServerRpc]
    private void RequestHospitalTreatmentServerRpc()
    {
        if (!IsInsideHospitalZone())
        {
            SetStatus("Enter a hospital zone");
            return;
        }

        if (survival.NetworkIsDowned.Value && !reviveAtHospital)
        {
            SetStatus("Hospital cannot revive this player");
            return;
        }

        bool wasDowned = survival.NetworkIsDowned.Value;
        survival.HealOrReviveServer(hospitalHealAmount, reviveAtHospital);
        if (wasDowned && reviveAtHospital)
        {
            MoveToHospitalSpawnServer();
        }
        SetStatus("Hospital treatment complete");
    }

    public void UseMedicalKit()
    {
        if (IsOwner)
        {
            UseMedicalKitServerRpc();
        }
    }

    [ServerRpc]
    private void UseMedicalKitServerRpc()
    {
        if (!survival.TryUseMedicalKitServer())
        {
            SetStatus("No medical kits available");
            return;
        }

        survival.HealOrReviveServer(hospitalHealAmount * 0.5f, true);
        SetStatus("Medical kit used");
    }

    public void AssignDeliveryBoyServer(bool enabled)
    {
        if (IsServer)
        {
            IsDeliveryBoy.Value = enabled;
            SetStatus(enabled ? "Delivery Boy role assigned" : "Delivery Boy role removed");
        }
    }

    private bool IsInsideHospitalZone()
    {
        Collider[] zones = Physics.OverlapSphere(transform.position, hospitalCheckRadius, hospitalZoneLayer, QueryTriggerInteraction.Collide);
        return zones.Length > 0;
    }

    private void MoveToHospitalSpawnServer()
    {
        GameObject[] spawnObjects;
        try
        {
            spawnObjects = GameObject.FindGameObjectsWithTag(hospitalSpawnTag);
        }
        catch (UnityException)
        {
            return;
        }

        if (spawnObjects.Length == 0)
        {
            return;
        }

        GameObject nearestSpawn = spawnObjects[0];
        float nearestDistance = Vector3.SqrMagnitude(transform.position - nearestSpawn.transform.position);
        for (int index = 1; index < spawnObjects.Length; index++)
        {
            float distance = Vector3.SqrMagnitude(transform.position - spawnObjects[index].transform.position);
            if (distance < nearestDistance)
            {
                nearestSpawn = spawnObjects[index];
                nearestDistance = distance;
            }
        }

        CharacterController characterController = GetComponent<CharacterController>();
        if (characterController != null)
        {
            characterController.enabled = false;
        }

        transform.SetPositionAndRotation(nearestSpawn.transform.position, nearestSpawn.transform.rotation);

        if (characterController != null)
        {
            characterController.enabled = true;
        }
    }

    private void GrantDeliveredItem(DeliveryItemType item)
    {
        switch (item)
        {
            case DeliveryItemType.CookedMeal:
                survival.ConsumeFoodServer(mealHungerRestored, mealEnergyRestored);
                break;
            case DeliveryItemType.MedicalKit:
                survival.AddMedicalKitsServer(1);
                break;
            case DeliveryItemType.GeneralSupplies:
                NetworkSupplies.Value = Mathf.Min(999, NetworkSupplies.Value + 1);
                break;
        }
    }

    private int GetPrice(DeliveryItemType item)
    {
        return item switch
        {
            DeliveryItemType.CookedMeal => cookedMealPrice,
            DeliveryItemType.MedicalKit => medicalKitPrice,
            DeliveryItemType.GeneralSupplies => generalSuppliesPrice,
            _ => 0
        };
    }

    private NetworkPlayer GetPlayer(ulong clientId)
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.ConnectedClients.TryGetValue(clientId, out NetworkClient client))
        {
            return null;
        }

        return client.PlayerObject != null ? client.PlayerObject.GetComponent<NetworkPlayer>() : null;
    }

    private void SetOrderState(NetworkPlayer player, DeliveryOrder order, string status)
    {
        if (player == null)
        {
            return;
        }

        DeliveryAndHospitalSystem delivery = player.GetComponent<DeliveryAndHospitalSystem>();
        delivery.ActiveOrderId.Value = order.id;
        delivery.OrderRecipientClientId.Value = order.recipientClientId;
        delivery.ActiveOrderItem.Value = order.item;
        delivery.ActiveOrderState.Value = order.state;
        delivery.SetStatus(status);
    }

    private void SetStatus(string status)
    {
        DeliveryStatus.Value = new FixedString128Bytes(status ?? string.Empty);
    }

    private void SetStatusForPlayer(ulong clientId, string status)
    {
        NetworkPlayer player = GetPlayer(clientId);
        if (player != null)
        {
            player.GetComponent<DeliveryAndHospitalSystem>().SetStatus(status);
        }
    }
}
