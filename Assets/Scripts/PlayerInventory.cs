using System;
using Unity.Netcode;
using UnityEngine;

public enum InventoryItemType : byte
{
    Food,
    VegetarianIngredient,
    NonVegetarianIngredient,
    MedicalKit,
    Weapon,
    Ammunition,
    GeneralSupply
}

public struct InventorySlot : INetworkSerializable, IEquatable<InventorySlot>
{
    public InventoryItemType itemType;
    public ushort quantity;

    public void NetworkSerialize<T>(BufferSerializer<T> serializer) where T : IReaderWriter
    {
        serializer.SerializeValue(ref itemType);
        serializer.SerializeValue(ref quantity);
    }

    public bool Equals(InventorySlot other)
    {
        return itemType == other.itemType && quantity == other.quantity;
    }
}

[RequireComponent(typeof(NetworkObject))]
public sealed class PlayerInventory : NetworkBehaviour
{
    [Min(1)] [SerializeField] private int maxSlots = 12;
    [Min(1)] [SerializeField] private int maxStackSize = 99;

    public NetworkList<InventorySlot> Slots { get; private set; }

    private void Awake()
    {
        Slots = new NetworkList<InventorySlot>(null, NetworkVariableReadPermission.Everyone, NetworkVariableWritePermission.Server);
    }

    public override void OnNetworkDespawn()
    {
        Slots?.Dispose();
    }

    public bool AddItemServer(InventoryItemType itemType, int quantity)
    {
        if (!IsServer || quantity <= 0)
        {
            return false;
        }

        int remaining = quantity;
        for (int index = 0; index < Slots.Count && remaining > 0; index++)
        {
            InventorySlot slot = Slots[index];
            if (slot.itemType != itemType || slot.quantity >= maxStackSize)
            {
                continue;
            }

            int amount = Mathf.Min(remaining, maxStackSize - slot.quantity);
            slot.quantity = (ushort)(slot.quantity + amount);
            Slots[index] = slot;
            remaining -= amount;
        }

        while (remaining > 0 && Slots.Count < maxSlots)
        {
            int amount = Mathf.Min(remaining, maxStackSize);
            Slots.Add(new InventorySlot { itemType = itemType, quantity = (ushort)amount });
            remaining -= amount;
        }

        return remaining == 0;
    }

    public bool RemoveItemServer(InventoryItemType itemType, int quantity)
    {
        if (!IsServer || quantity <= 0 || CountItem(itemType) < quantity)
        {
            return false;
        }

        int remaining = quantity;
        for (int index = Slots.Count - 1; index >= 0 && remaining > 0; index--)
        {
            InventorySlot slot = Slots[index];
            if (slot.itemType != itemType)
            {
                continue;
            }

            int amount = Mathf.Min(remaining, slot.quantity);
            slot.quantity = (ushort)(slot.quantity - amount);
            remaining -= amount;
            if (slot.quantity == 0)
            {
                Slots.RemoveAt(index);
            }
            else
            {
                Slots[index] = slot;
            }
        }

        return remaining == 0;
    }

    public int CountItem(InventoryItemType itemType)
    {
        int count = 0;
        for (int index = 0; index < Slots.Count; index++)
        {
            if (Slots[index].itemType == itemType)
            {
                count += Slots[index].quantity;
            }
        }

        return count;
    }
}
