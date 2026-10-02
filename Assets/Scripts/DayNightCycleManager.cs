using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public enum DayNightPhase : byte
{
    Day,
    Night
}

[RequireComponent(typeof(NetworkObject))]
public sealed class DayNightCycleManager : NetworkBehaviour
{
    public const float DayDurationSeconds = 9f * 60f;
    public const float NightDurationSeconds = 3f * 60f;
    public const float FullCycleDurationSeconds = DayDurationSeconds + NightDurationSeconds;

    [Header("Cycle")]
    [SerializeField] private NetworkMatchManager matchManager;
    [SerializeField] private Light directionalLight;
    [SerializeField] private Color dayLightColor = Color.white;
    [SerializeField] private Color nightLightColor = new(0.08f, 0.1f, 0.2f);
    [SerializeField] private float dayLightIntensity = 1f;
    [SerializeField] private float nightLightIntensity = 0.18f;
    [SerializeField] private Color dayAmbientColor = new(0.5f, 0.5f, 0.5f);
    [SerializeField] private Color nightAmbientColor = new(0.03f, 0.04f, 0.09f);

    [Header("Wild Animals")]
    [SerializeField] private NetworkObject wildAnimalPrefab;
    [SerializeField] private Transform[] mapAnimalSpawnPoints;
    [SerializeField] private Transform[] campAnimalSpawnPoints;
    [Min(0)] [SerializeField] private int animalsPerNight = 8;
    [Min(0.1f)] [SerializeField] private float minimumSpawnDistance = 25f;

    public NetworkVariable<DayNightPhase> CurrentPhase = new(
        DayNightPhase.Day,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);
    public NetworkVariable<float> PhaseTimeRemaining = new(
        DayDurationSeconds,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private readonly List<WildAnimalAI> spawnedAnimals = new();
    private float phaseElapsed;

    public bool IsNight => CurrentPhase.Value == DayNightPhase.Night;

    private void Awake()
    {
        matchManager = matchManager != null ? matchManager : FindObjectOfType<NetworkMatchManager>();
    }

    public override void OnNetworkSpawn()
    {
        CurrentPhase.OnValueChanged += OnPhaseChanged;
        ApplyLighting(CurrentPhase.Value);

        if (IsServer)
        {
            CurrentPhase.Value = DayNightPhase.Day;
            PhaseTimeRemaining.Value = DayDurationSeconds;
            phaseElapsed = 0f;
        }
    }

    public override void OnNetworkDespawn()
    {
        CurrentPhase.OnValueChanged -= OnPhaseChanged;

        if (IsServer)
        {
            DespawnAnimalsServer();
        }
    }

    private void Update()
    {
        if (matchManager == null)
        {
            matchManager = FindObjectOfType<NetworkMatchManager>();
        }

        ApplyLighting(CurrentPhase.Value);

        if (IsServer && matchManager != null && matchManager.CurrentState.Value == MatchState.MatchEnded)
        {
            if (spawnedAnimals.Count > 0)
            {
                DespawnAnimalsServer();
            }

            return;
        }

        if (!IsServer || !CanCycle())
        {
            return;
        }

        phaseElapsed += Time.deltaTime;
        PhaseTimeRemaining.Value = Mathf.Max(0f, PhaseDuration(CurrentPhase.Value) - phaseElapsed);
        if (phaseElapsed >= PhaseDuration(CurrentPhase.Value))
        {
            AdvancePhaseServer();
        }
    }

    private bool CanCycle()
    {
        return matchManager == null || matchManager.CurrentState.Value == MatchState.BattleStarted || matchManager.CurrentState.Value == MatchState.SafeZoneShrinking;
    }

    private float PhaseDuration(DayNightPhase phase)
    {
        return phase == DayNightPhase.Day ? DayDurationSeconds : NightDurationSeconds;
    }

    private void AdvancePhaseServer()
    {
        phaseElapsed = 0f;
        DayNightPhase nextPhase = CurrentPhase.Value == DayNightPhase.Day ? DayNightPhase.Night : DayNightPhase.Day;
        CurrentPhase.Value = nextPhase;
        PhaseTimeRemaining.Value = PhaseDuration(nextPhase);

        if (nextPhase == DayNightPhase.Night)
        {
            SpawnAnimalsServer();
        }
        else
        {
            DespawnAnimalsServer();
        }
    }

    private void OnPhaseChanged(DayNightPhase previousPhase, DayNightPhase newPhase)
    {
        ApplyLighting(newPhase);
    }

    private void ApplyLighting(DayNightPhase phase)
    {
        bool night = phase == DayNightPhase.Night;
        if (directionalLight != null)
        {
            directionalLight.color = night ? nightLightColor : dayLightColor;
            directionalLight.intensity = night ? nightLightIntensity : dayLightIntensity;
        }

        RenderSettings.ambientLight = night ? nightAmbientColor : dayAmbientColor;
    }

    private void SpawnAnimalsServer()
    {
        if (wildAnimalPrefab == null || animalsPerNight <= 0)
        {
            return;
        }

        DespawnAnimalsServer();
        List<Transform> spawnPoints = BuildSpawnPointList();
        int spawnCount = Mathf.Min(animalsPerNight, spawnPoints.Count);
        for (int index = 0; index < spawnCount; index++)
        {
            Transform spawnPoint = spawnPoints[index];
            NetworkObject animalObject = Instantiate(wildAnimalPrefab, spawnPoint.position, spawnPoint.rotation);
            WildAnimalAI animal = animalObject.GetComponent<WildAnimalAI>();
            if (animal == null)
            {
                Destroy(animalObject.gameObject);
                continue;
            }

            animalObject.Spawn(true);
            animal.InitializeServer(this);
            spawnedAnimals.Add(animal);
        }
    }

    private List<Transform> BuildSpawnPointList()
    {
        List<Transform> availablePoints = new();
        AddValidSpawnPoints(availablePoints, mapAnimalSpawnPoints);
        AddValidSpawnPoints(availablePoints, campAnimalSpawnPoints);

        for (int index = availablePoints.Count - 1; index > 0; index--)
        {
            int swapIndex = Random.Range(0, index + 1);
            Transform swapPoint = availablePoints[index];
            availablePoints[index] = availablePoints[swapIndex];
            availablePoints[swapIndex] = swapPoint;
        }

        return availablePoints;
    }

    private void AddValidSpawnPoints(List<Transform> destination, Transform[] source)
    {
        if (source == null)
        {
            return;
        }

        foreach (Transform candidate in source)
        {
            if (candidate == null || IsTooCloseToExistingPoint(destination, candidate))
            {
                continue;
            }

            destination.Add(candidate);
        }
    }

    private bool IsTooCloseToExistingPoint(List<Transform> points, Transform candidate)
    {
        foreach (Transform point in points)
        {
            if (Vector3.Distance(point.position, candidate.position) < minimumSpawnDistance)
            {
                return true;
            }
        }

        return false;
    }

    private void DespawnAnimalsServer()
    {
        for (int index = spawnedAnimals.Count - 1; index >= 0; index--)
        {
            WildAnimalAI animal = spawnedAnimals[index];
            if (animal != null && animal.NetworkObject != null && animal.IsSpawned)
            {
                animal.NetworkObject.Despawn(true);
            }
        }

        spawnedAnimals.Clear();
    }
}
