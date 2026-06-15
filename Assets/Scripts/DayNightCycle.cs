using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(Light))]
public sealed class DayNightCycle : MonoBehaviour
{
    [Header("Time")]
    [SerializeField, Min(1f)] private float fullDayDurationSeconds = 7200f;
    [SerializeField, Range(0f, 24f)] private float startTimeOfDay = 8f;
    [SerializeField] private bool runCycle = true;

    [Header("Sun")]
    [SerializeField] private float sunAzimuth = 170f;
    [SerializeField] private float daySunIntensity = 1.1f;
    [SerializeField] private float nightSunIntensity = 0.025f;
    [SerializeField] private Color daySunColor = new(1f, 0.956f, 0.88f);
    [SerializeField] private Color sunsetSunColor = new(1f, 0.48f, 0.24f);
    [SerializeField] private Color nightSunColor = new(0.18f, 0.25f, 0.45f);

    [Header("Ambient Light")]
    [SerializeField] private float ambientDayIntensity = 1f;
    [SerializeField] private float ambientNightIntensity = 0.32f;
    [SerializeField] private Color dayAmbientSky = new(0.42f, 0.58f, 0.82f);
    [SerializeField] private Color dayAmbientEquator = new(0.34f, 0.4f, 0.48f);
    [SerializeField] private Color dayAmbientGround = new(0.16f, 0.15f, 0.14f);
    [SerializeField] private Color nightAmbientSky = new(0.025f, 0.045f, 0.11f);
    [SerializeField] private Color nightAmbientEquator = new(0.018f, 0.028f, 0.065f);
    [SerializeField] private Color nightAmbientGround = new(0.01f, 0.012f, 0.025f);

    [Header("Reflections")]
    [SerializeField] private float reflectionDayIntensity = 1f;
    [SerializeField] private float reflectionNightIntensity = 0.35f;

    [Header("Artificial Lights")]
    [SerializeField] private bool controlArtificialLights = true;
    [SerializeField, Range(0f, 1f)] private float artificialLightsOnDarkness = 0.45f;

    private Light sunLight;
    private ControlledLight[] artificialLights;
    private float currentTimeOfDay;

    public float CurrentTimeOfDay => currentTimeOfDay;

    private void Awake()
    {
        sunLight = GetComponent<Light>();
        RenderSettings.sun = sunLight;
        currentTimeOfDay = Mathf.Repeat(startTimeOfDay, 24f);
        CacheArtificialLights();
        ApplyLighting();
    }

    private void Update()
    {
        if (runCycle)
        {
            float gameHoursPerSecond = 24f / Mathf.Max(fullDayDurationSeconds, 1f);
            currentTimeOfDay = Mathf.Repeat(
                currentTimeOfDay + Time.deltaTime * gameHoursPerSecond,
                24f);
        }

        ApplyLighting();
    }

    private void ApplyLighting()
    {
        float sunAngle = currentTimeOfDay / 24f * 360f - 90f;
        transform.rotation = Quaternion.Euler(sunAngle, sunAzimuth, 0f);

        float sunHeight = Vector3.Dot(-transform.forward, Vector3.up);
        float daylight = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(-0.12f, 0.2f, sunHeight));
        float sunsetBlend = (1f - Mathf.Clamp01(Mathf.Abs(sunHeight) / 0.35f)) * daylight;
        Color sunlightColor = Color.Lerp(daySunColor, sunsetSunColor, sunsetBlend);

        sunLight.intensity = Mathf.Lerp(nightSunIntensity, daySunIntensity, daylight);
        sunLight.color = Color.Lerp(nightSunColor, sunlightColor, daylight);

        RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientIntensity = Mathf.Lerp(ambientNightIntensity, ambientDayIntensity, daylight);
        RenderSettings.ambientSkyColor = Color.Lerp(nightAmbientSky, dayAmbientSky, daylight);
        RenderSettings.ambientEquatorColor = Color.Lerp(nightAmbientEquator, dayAmbientEquator, daylight);
        RenderSettings.ambientGroundColor = Color.Lerp(nightAmbientGround, dayAmbientGround, daylight);
        RenderSettings.reflectionIntensity = Mathf.Lerp(
            reflectionNightIntensity,
            reflectionDayIntensity,
            daylight);

        if (controlArtificialLights)
        {
            ApplyArtificialLights(1f - daylight);
        }
    }

    private void CacheArtificialLights()
    {
        Light[] sceneLights = FindObjectsByType<Light>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);
        List<ControlledLight> controlledLights = new();

        foreach (Light sceneLight in sceneLights)
        {
            if (sceneLight != sunLight && sceneLight.type != LightType.Directional)
            {
                controlledLights.Add(new ControlledLight(sceneLight, sceneLight.intensity));
            }
        }

        artificialLights = controlledLights.ToArray();
    }

    private void ApplyArtificialLights(float darkness)
    {
        float lightBlend = Mathf.SmoothStep(
            0f,
            1f,
            Mathf.InverseLerp(artificialLightsOnDarkness, 1f, darkness));

        foreach (ControlledLight controlledLight in artificialLights)
        {
            if (controlledLight.Light == null)
            {
                continue;
            }

            controlledLight.Light.enabled = lightBlend > 0.01f;
            controlledLight.Light.intensity = controlledLight.BaseIntensity * lightBlend;
        }
    }

    private readonly struct ControlledLight
    {
        public ControlledLight(Light light, float baseIntensity)
        {
            Light = light;
            BaseIntensity = baseIntensity;
        }

        public Light Light { get; }
        public float BaseIntensity { get; }
    }
}
