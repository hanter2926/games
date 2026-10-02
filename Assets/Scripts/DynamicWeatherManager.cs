using Unity.Netcode;
using UnityEngine;

public enum WeatherType : byte
{
    Clear,
    Rain,
    Fog,
    Storm
}

[RequireComponent(typeof(NetworkObject))]
public sealed class DynamicWeatherManager : NetworkBehaviour
{
    [Header("References")]
    [SerializeField] private NetworkMatchManager matchManager;
    [SerializeField] private DayNightCycleManager dayNightCycle;
    [SerializeField] private ParticleSystem rainParticles;

    [Header("Weather")]
    [SerializeField] private float weatherChangeInterval = 45f;
    [SerializeField] private float rainDrainPerSecond = 0.15f;
    [SerializeField] private float stormDrainPerSecond = 0.35f;
    [SerializeField] private float clearFogDensity = 0f;
    [SerializeField] private float rainFogDensity = 0.008f;
    [SerializeField] private float nightFogDensity = 0.018f;

    public NetworkVariable<WeatherType> CurrentWeather = new(
        WeatherType.Clear,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);
    public NetworkVariable<float> WeatherIntensity = new(
        0f,
        NetworkVariableReadPermission.Everyone,
        NetworkVariableWritePermission.Server);

    private float weatherTimer;
    private float exposureTimer;

    private void Awake()
    {
        matchManager = matchManager != null ? matchManager : FindObjectOfType<NetworkMatchManager>();
        dayNightCycle = dayNightCycle != null ? dayNightCycle : FindObjectOfType<DayNightCycleManager>();
    }

    public override void OnNetworkSpawn()
    {
        CurrentWeather.OnValueChanged += OnWeatherChanged;
        ApplyWeather(CurrentWeather.Value, WeatherIntensity.Value);
    }

    public override void OnNetworkDespawn()
    {
        CurrentWeather.OnValueChanged -= OnWeatherChanged;
    }

    private void Update()
    {
        ApplyWeather(CurrentWeather.Value, WeatherIntensity.Value);
        if (!IsServer || matchManager == null || !matchManager.IsMatchActive)
        {
            return;
        }

        weatherTimer -= Time.deltaTime;
        exposureTimer -= Time.deltaTime;
        if (weatherTimer <= 0f)
        {
            weatherTimer = Mathf.Max(5f, weatherChangeInterval);
            SelectWeatherServer();
        }

        if (exposureTimer <= 0f)
        {
            exposureTimer = 1f;
            ApplyExposureServer();
        }
    }

    private void SelectWeatherServer()
    {
        bool isNight = dayNightCycle != null && dayNightCycle.IsNight;
        WeatherType weather;
        if (isNight)
        {
            weather = Random.value < 0.5f ? WeatherType.Fog : WeatherType.Rain;
        }
        else
        {
            float roll = Random.value;
            weather = roll < 0.65f ? WeatherType.Clear : roll < 0.95f ? WeatherType.Rain : WeatherType.Storm;
        }

        CurrentWeather.Value = weather;
        WeatherIntensity.Value = weather == WeatherType.Clear ? 0f : Random.Range(0.35f, 1f);
    }

    private void ApplyExposureServer()
    {
        float drain = CurrentWeather.Value switch
        {
            WeatherType.Rain => rainDrainPerSecond,
            WeatherType.Storm => stormDrainPerSecond,
            _ => 0f
        } * WeatherIntensity.Value;

        if (drain <= 0f)
        {
            return;
        }

        foreach (NetworkPlayer player in matchManager.ActivePlayers)
        {
            player?.GetComponent<PlayerSurvival>()?.ApplyWeatherExposureServer(drain);
        }
    }

    private void OnWeatherChanged(WeatherType previousWeather, WeatherType newWeather)
    {
        ApplyWeather(newWeather, WeatherIntensity.Value);
    }

    private void ApplyWeather(WeatherType weather, float intensity)
    {
        bool rain = weather == WeatherType.Rain || weather == WeatherType.Storm;
        if (rainParticles != null)
        {
            if (rain)
            {
                var emission = rainParticles.emission;
                emission.rateOverTime = 250f * Mathf.Clamp01(intensity);
                if (!rainParticles.isPlaying) rainParticles.Play();
            }
            else if (rainParticles.isPlaying)
            {
                rainParticles.Stop();
            }
        }

        RenderSettings.fog = weather == WeatherType.Fog || (dayNightCycle != null && dayNightCycle.IsNight);
        RenderSettings.fogDensity = weather == WeatherType.Fog || (dayNightCycle != null && dayNightCycle.IsNight)
            ? nightFogDensity * Mathf.Max(0.35f, intensity)
            : rain ? rainFogDensity * intensity : clearFogDensity;
    }
}
