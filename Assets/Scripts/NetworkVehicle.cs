using Unity.Netcode;
using UnityEngine;

public enum NetworkVehicleType : byte
{
    Standard,
    DeliveryVan
}

[RequireComponent(typeof(NetworkObject))]
[RequireComponent(typeof(NetworkTransform))]
public sealed class NetworkVehicle : NetworkBehaviour
{
    [Header("Vehicle")]
    [SerializeField] private NetworkVehicleType vehicleType = NetworkVehicleType.Standard;
    [SerializeField] private Transform driverSeat;
    [SerializeField] private Transform exitPoint;
    [SerializeField] private float maxSpeed = 12f;
    [SerializeField] private float turnSpeed = 70f;
    [SerializeField] private float interactionDistance = 3f;
    [SerializeField] private float inputSendInterval = 0.1f;

    public NetworkVariable<ulong> DriverClientId = new(
        ulong.MaxValue,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private float throttleInput;
    private float steeringInput;
    private float inputTimer;

    public bool IsOccupied => DriverClientId.Value != ulong.MaxValue;

    private void Update()
    {
        if (IsServer)
        {
            SimulateVehicleServer();
        }

        if (IsDriverOwner())
        {
            inputTimer -= Time.deltaTime;
            if (inputTimer <= 0f)
            {
                inputTimer = Mathf.Max(0.02f, inputSendInterval);
                SubmitDriveInputServerRpc(Input.GetAxisRaw("Vertical"), Input.GetAxisRaw("Horizontal"));
            }
        }
    }

    public void EnterVehicle()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsClient)
        {
            RequestEnterServerRpc();
        }
    }

    public void ExitVehicle()
    {
        if (NetworkManager.Singleton != null && NetworkManager.Singleton.IsClient)
        {
            RequestExitServerRpc();
        }
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestEnterServerRpc(ServerRpcParams rpcParams = default)
    {
        if (IsOccupied || NetworkManager.Singleton == null || !NetworkManager.Singleton.ConnectedClients.TryGetValue(rpcParams.Receive.SenderClientId, out NetworkClient client) || client.PlayerObject == null)
        {
            return;
        }

        NetworkPlayer player = client.PlayerObject.GetComponent<NetworkPlayer>();
        if (player == null || Vector3.Distance(player.transform.position, transform.position) > interactionDistance || (vehicleType == NetworkVehicleType.DeliveryVan && !player.GetComponent<DeliveryAndHospitalSystem>().IsDeliveryBoy.Value))
        {
            return;
        }

        DriverClientId.Value = rpcParams.Receive.SenderClientId;
        player.SetVehicleServer(this, true);
        SnapDriverToSeatServer(player);
    }

    [ServerRpc(RequireOwnership = false)]
    private void RequestExitServerRpc(ServerRpcParams rpcParams = default)
    {
        if (!IsOccupied || DriverClientId.Value != rpcParams.Receive.SenderClientId)
        {
            return;
        }

        NetworkPlayer player = GetDriverServer();
        if (player != null)
        {
            player.SetVehicleServer(this, false);
            player.transform.SetPositionAndRotation(exitPoint != null ? exitPoint.position : transform.position + transform.right * 2f, transform.rotation);
        }

        DriverClientId.Value = ulong.MaxValue;
        throttleInput = 0f;
        steeringInput = 0f;
    }

    [ServerRpc]
    private void SubmitDriveInputServerRpc(float throttle, float steering, ServerRpcParams rpcParams = default)
    {
        if (DriverClientId.Value != rpcParams.Receive.SenderClientId)
        {
            return;
        }

        throttleInput = Mathf.Clamp(throttle, -1f, 1f);
        steeringInput = Mathf.Clamp(steering, -1f, 1f);
    }

    private void SimulateVehicleServer()
    {
        if (!IsOccupied)
        {
            return;
        }

        transform.Rotate(0f, steeringInput * turnSpeed * Time.deltaTime, 0f);
        transform.position += transform.forward * throttleInput * maxSpeed * Time.deltaTime;
        NetworkPlayer driver = GetDriverServer();
        if (driver != null)
        {
            SnapDriverToSeatServer(driver);
        }
    }

    private void SnapDriverToSeatServer(NetworkPlayer driver)
    {
        if (driverSeat != null)
        {
            driver.transform.SetPositionAndRotation(driverSeat.position, driverSeat.rotation);
        }
    }

    private NetworkPlayer GetDriverServer()
    {
        if (NetworkManager.Singleton == null || !NetworkManager.Singleton.ConnectedClients.TryGetValue(DriverClientId.Value, out NetworkClient client) || client.PlayerObject == null)
        {
            return null;
        }

        return client.PlayerObject.GetComponent<NetworkPlayer>();
    }

    private bool IsDriverOwner()
    {
        return NetworkManager.Singleton != null && DriverClientId.Value == NetworkManager.Singleton.LocalClientId;
    }
}
