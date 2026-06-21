using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

public sealed class FishTankWaterQuality : MonoBehaviour
{
    [Header("Water and Display Settings")]
    [SerializeField] private Collider waterCollider;
    [SerializeField] private Renderer waterRenderer;
    [SerializeField] private Material cleanMaterial;
    [SerializeField] private Material dirtyMaterial;

    [Header("Cycle Settings (Seconds)")]
    [SerializeField] private float timeToGetDirty = 180f; // 3 minutes to get fully dirty
    [SerializeField] private float timeToLoseOxygen = 120f; // 2 minutes to lose all oxygen if aerator is off

    [Header("Oxygen Capacity")]
    [SerializeField] private float oxygenUsePerFish = 1f;
    [SerializeField] private int aeratorLevel = 1;
    [SerializeField] private int maxAeratorLevel = 3;
    [SerializeField] private float baseAeratorFishCapacity = 3f;
    [SerializeField] private float aeratorCapacityPerUpgrade = 2f;
    [SerializeField] private int aeratorUpgradeCost = 300;
    [SerializeField] private Renderer aeratorRenderer;
    [SerializeField] private Transform aeratorVisualRoot;

    [Header("Aerator Bubble Effect")]
    [SerializeField] private ParticleSystem bubbleEffect;

    [Header("Starter Oxygen Plants")]
    [SerializeField] private bool createStarterPlants = true;
    [SerializeField] private int starterPlantCount = 3;

    [Header("Interaction Settings")]
    [SerializeField] private float interactionDistance = 3.5f;

    [Header("Bucket Prefabs Reference")]
    [SerializeField] private GameObject emptyBucketPrefab;
    [SerializeField] private GameObject waterBucketPrefab;

    // Water stats (0.0 to 1.0)
    public float Cleanliness { get; private set; } = 1.0f;
    public float Oxygen { get; private set; } = 1.0f;
    public float Chlorine { get; private set; } = 0.0f;
    public bool IsAeratorOn { get; private set; } = true;
    public int FishCount { get; private set; }
    public float FishOxygenLoad { get; private set; }
    public float AeratorOxygenCapacity { get; private set; }
    public float PlantOxygenCapacity { get; private set; }
    public float TotalOxygenCapacity => AeratorOxygenCapacity + PlantOxygenCapacity;

    private Transform player;
    private SimpleInventory playerInventory;
    private bool isPlayerNear;
    private GUIStyle labelStyle;
    private GUIStyle boxStyle;
    private GUIStyle warningStyle;
    private Vector3 aeratorBaseScale = Vector3.one;

    private void Start()
    {
        FindPlayer();
#if UNITY_EDITOR
        AutoFindPrefabs();
#endif

        // 1. Auto-detect Water Collider and Renderer if not set
        if (waterCollider == null)
        {
            waterCollider = GetComponentInChildren<Collider>();
        }
        if (waterRenderer == null)
        {
            foreach (Renderer r in GetComponentsInChildren<Renderer>())
            {
                if (r.gameObject.name.ToLower().Contains("surface") || r.gameObject.name.ToLower().Contains("inside") || r.gameObject.name.ToLower().Contains("water"))
                {
                    waterRenderer = r;
                    break;
                }
            }
        }

        // 2. Auto-detect LocNuoc filter to attach bubble particle system
        AutoFindAeratorVisual();

        if (bubbleEffect == null)
        {
            bubbleEffect = GetComponentInChildren<ParticleSystem>();
            if (bubbleEffect == null)
            {
                Transform locNuocTransform = null;
                foreach (Transform child in GetComponentsInChildren<Transform>(true))
                {
                    string name = child.gameObject.name.ToLower();
                    if (name.Contains("loc") || name.Contains("filter") || name.Contains("aerator"))
                    {
                        locNuocTransform = child;
                        break;
                    }
                }

                if (locNuocTransform != null)
                {
                    Renderer ren = locNuocTransform.GetComponentInChildren<Renderer>();
                    Vector3 spawnPos = ren != null ? ren.bounds.center : locNuocTransform.position;
                    CreateDefaultBubbleEffect(spawnPos + Vector3.up * 0.05f);
                }
                else
                {
                    Vector3 spawnPos = waterCollider != null ? waterCollider.bounds.center : transform.position;
                    if (waterCollider != null)
                    {
                        // Spawn bubbles from the bottom of the tank
                        spawnPos.y = waterCollider.bounds.min.y + 0.02f;
                    }
                    CreateDefaultBubbleEffect(spawnPos);
                }
            }
        }

        aeratorLevel = Mathf.Clamp(aeratorLevel, 1, maxAeratorLevel);
        if (aeratorVisualRoot != null)
        {
            aeratorBaseScale = aeratorVisualRoot.localScale;
        }
        EnsureStarterOxygenPlants();
        UpdateAeratorVisuals();
    }

    private void CreateDefaultBubbleEffect(Vector3 spawnPosition)
    {
        GameObject bubbleObj = new("TankBubbleEffect");
        bubbleObj.transform.position = spawnPosition;
        bubbleObj.transform.SetParent(transform, true);
        bubbleObj.transform.localScale = Vector3.one;

        bubbleEffect = bubbleObj.AddComponent<ParticleSystem>();
        
        var main = bubbleEffect.main;
        main.playOnAwake = true;
        main.startLifetime = new ParticleSystem.MinMaxCurve(1.5f, 2.5f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 0.6f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.02f, 0.05f);
        main.maxParticles = 150;
        main.simulationSpace = ParticleSystemSimulationSpace.World;

        var emission = bubbleEffect.emission;
        emission.enabled = true;
        emission.rateOverTime = 35;

        var shape = bubbleEffect.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 10f;
        shape.radius = 0.05f;

        var velocity = bubbleEffect.velocityOverLifetime;
        velocity.enabled = true;
        velocity.y = new ParticleSystem.MinMaxCurve(0.3f, 0.6f);

        // Size over lifetime: start smaller, grow, then shrink at surface
        var sizeOverLifetime = bubbleEffect.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        AnimationCurve sizeCurve = new AnimationCurve();
        sizeCurve.AddKey(0.0f, 0.5f);
        sizeCurve.AddKey(0.2f, 1.0f);
        sizeCurve.AddKey(1.0f, 0.3f);
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1.0f, sizeCurve);

        // Color over lifetime: fade out at the surface
        var colorOverLifetime = bubbleEffect.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new GradientColorKey[] { new GradientColorKey(Color.white, 0.0f), new GradientColorKey(Color.white, 1.0f) },
            new GradientAlphaKey[] { new GradientAlphaKey(1.0f, 0.0f), new GradientAlphaKey(0.8f, 0.7f), new GradientAlphaKey(0.0f, 1.0f) }
        );
        colorOverLifetime.color = gradient;

        var renderer = bubbleObj.GetComponent<ParticleSystemRenderer>();
        // Load default Unity particle material (soft circular dot)
        Material defaultMat = Resources.GetBuiltinResource<Material>("Default-Particle.mat");
        if (defaultMat != null)
        {
            renderer.material = defaultMat;
        }
        else
        {
            Shader particleShader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (particleShader == null)
            {
                particleShader = Shader.Find("Sprites/Default");
            }
            renderer.material = new Material(particleShader);
        }

        bubbleEffect.Play();
    }

    private void FindPlayer()
    {
        MinhThirdPersonController controller = FindFirstObjectByType<MinhThirdPersonController>();
        if (controller != null)
        {
            player = controller.transform;
            playerInventory = controller.GetComponent<SimpleInventory>();
        }
    }

    private void Update()
    {
        if (PauseMenuManager.GameIsPaused || AquariumDecorationMode.IsDecorationMode) return;

        if (player == null || playerInventory == null)
        {
            FindPlayer();
            return;
        }

        // Cleanliness decays over time
        Cleanliness = Mathf.Max(0f, Cleanliness - (Time.deltaTime / timeToGetDirty));

        UpdateOxygenCapacity();

        // Oxygen depends on living fish load, aerator level, and aquarium plants.
        if (IsAeratorOn && TotalOxygenCapacity >= FishOxygenLoad)
        {
            float surplusCapacity = TotalOxygenCapacity - FishOxygenLoad;
            float recoverySpeed = 0.08f + surplusCapacity * 0.015f;
            Oxygen = Mathf.Min(1f, Oxygen + Time.deltaTime * recoverySpeed);
        }
        else
        {
            float shortage = IsAeratorOn
                ? Mathf.Max(0.25f, FishOxygenLoad - TotalOxygenCapacity)
                : Mathf.Max(0.5f, FishOxygenLoad);
            float lossPerSecond = (1f / Mathf.Max(1f, timeToLoseOxygen)) * shortage;
            Oxygen = Mathf.Max(0f, Oxygen - Time.deltaTime * lossPerSecond);
        }

        // Chlorine evaporates gradually (2% per second)
        if (Chlorine > 0f)
        {
            Chlorine = Mathf.Max(0f, Chlorine - (Time.deltaTime * 0.02f));
        }

        // Update water material based on cleanliness
        if (waterRenderer != null)
        {
            if (Cleanliness < 0.4f && dirtyMaterial != null)
            {
                if (waterRenderer.sharedMaterial != dirtyMaterial)
                    waterRenderer.material = dirtyMaterial;
            }
            else if (cleanMaterial != null)
            {
                if (waterRenderer.sharedMaterial != cleanMaterial)
                    waterRenderer.material = cleanMaterial;
            }
        }

        // Check distance to player
        float distance = Vector3.Distance(transform.position, player.position);
        isPlayerNear = distance <= interactionDistance;

        if (isPlayerNear)
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null)
            {
                // Key T: Toggle aerator
                if (keyboard.tKey.wasPressedThisFrame)
                {
                    IsAeratorOn = !IsAeratorOn;
                    UpdateAeratorVisuals();
                }

                if (keyboard.uKey.wasPressedThisFrame)
                {
                    TryUpgradeAerator();
                }

                // Key R: Dechlorinate (costs 50 VND)
                if (keyboard.rKey.wasPressedThisFrame && Chlorine > 0f)
                {
                    if (PlayerMoneyDisplay.TrySpendMoney(50))
                    {
                        Chlorine = 0f;
                        Debug.Log("Successfully dechlorinated the water!");
                    }
                }

                // Key F: Pour clean water to clean the tank
                if (keyboard.fKey.wasPressedThisFrame)
                {
                    // Case 1: Player is holding a water bucket in hand
                    SimpleInventory.InventoryItem selectedItem = playerInventory.GetSelectedItem();
                    if (selectedItem != null && SimpleInventory.IsWaterBucket(selectedItem.Name))
                    {
                        Cleanliness = 1.0f;
                        Chlorine = 1.0f; // Tap water has chlorine

                        // Convert to empty bucket on hand
                        SimpleInventory.InventoryItem emptyBucket = new SimpleInventory.InventoryItem(
                            "Bucket",
                            Color.white,
                            emptyBucketPrefab, // Nullable empty bucket prefab
                            selectedItem.SceneTemplate, // Restore original empty bucket template
                            selectedItem.BaseRotation
                        );
                        playerInventory.ReplaceSelectedItem(emptyBucket);
                        Debug.Log("Successfully poured clean water into the fish tank from hand-held bucket!");
                        return;
                    }

                    // Case 2: There is a water bucket dropped near the tank
                    InventoryPickupItem nearestDroppedWaterBucket = FindNearestDroppedWaterBucket(transform.position, interactionDistance);
                    if (nearestDroppedWaterBucket != null)
                    {
                        Cleanliness = 1.0f;
                        Chlorine = 1.0f;

                        Vector3 originalPosition = nearestDroppedWaterBucket.transform.position;
                        Quaternion originalRotation = nearestDroppedWaterBucket.transform.rotation;

                        if (emptyBucketPrefab != null)
                        {
                            Destroy(nearestDroppedWaterBucket.gameObject);

                            GameObject newEmptyBucket = Instantiate(emptyBucketPrefab, originalPosition, originalRotation);
                            newEmptyBucket.name = "Bucket";

                            InventoryPickupItem newPickup = newEmptyBucket.GetComponent<InventoryPickupItem>();
                            if (newPickup == null)
                            {
                                newPickup = newEmptyBucket.AddComponent<InventoryPickupItem>();
                            }
                            newPickup.Configure("Bucket", Color.white, emptyBucketPrefab);

                            // Đảm bảo ẩn mesh nước của xô rỗng mới tạo ra (nếu có)
                            foreach (Transform child in newEmptyBucket.GetComponentsInChildren<Transform>(true))
                            {
                                if (child.gameObject.name.ToLower().Contains("water"))
                                {
                                    child.gameObject.SetActive(false);
                                }
                            }
                        }
                        else
                        {
                            // Convert dropped water bucket to empty bucket (fallback)
                            nearestDroppedWaterBucket.Configure(
                                "Bucket",
                                new Color(0.7f, 0.7f, 0.7f, 1.0f), // Grey color of empty bucket
                                nearestDroppedWaterBucket.DropPrefab
                            );

                            Renderer[] renderers = nearestDroppedWaterBucket.GetComponentsInChildren<Renderer>(true);
                            foreach (Renderer r in renderers)
                            {
                                if (r.material.HasProperty("_BaseColor"))
                                    r.material.SetColor("_BaseColor", new Color(0.7f, 0.7f, 0.7f, 1.0f));
                                else
                                    r.material.color = new Color(0.7f, 0.7f, 0.7f, 1.0f);
                            }

                            // Đảm bảo ẩn mesh nước ở dạng fallback
                            foreach (Transform child in nearestDroppedWaterBucket.GetComponentsInChildren<Transform>(true))
                            {
                                if (child.gameObject.name.ToLower().Contains("water"))
                                {
                                    child.gameObject.SetActive(false);
                                }
                            }
                        }

                        Debug.Log("Successfully poured clean water into the fish tank from dropped bucket!");
                    }
                }
            }
        }
    }

    private void UpdateAeratorVisuals()
    {
        if (bubbleEffect != null)
        {
            if (IsAeratorOn)
            {
                if (!bubbleEffect.isPlaying) bubbleEffect.Play();
            }
            else
            {
                if (bubbleEffect.isPlaying) bubbleEffect.Stop();
            }
        }

        if (aeratorVisualRoot != null)
        {
            float scaleBonus = 1f + ((aeratorLevel - 1) * 0.12f);
            aeratorVisualRoot.localScale = aeratorBaseScale * scaleBonus;
        }

        if (aeratorRenderer != null)
        {
            Color levelColor = aeratorLevel switch
            {
                1 => new Color(0.15f, 0.65f, 1f),
                2 => new Color(0.25f, 1f, 0.55f),
                _ => new Color(1f, 0.75f, 0.25f)
            };

            if (aeratorRenderer.material.HasProperty("_BaseColor"))
            {
                aeratorRenderer.material.SetColor("_BaseColor", levelColor);
            }
            else
            {
                aeratorRenderer.material.color = levelColor;
            }
        }
    }

    private void UpdateOxygenCapacity()
    {
        FishCount = 0;
        FishOxygenLoad = 0f;
        PlantOxygenCapacity = 0f;
        AeratorOxygenCapacity = IsAeratorOn
            ? baseAeratorFishCapacity + Mathf.Max(0, aeratorLevel - 1) * aeratorCapacityPerUpgrade
            : 0f;

        if (waterCollider != null)
        {
            Bounds waterBounds = waterCollider.bounds;
            foreach (FishSwim fish in FindObjectsByType<FishSwim>(FindObjectsSortMode.None))
            {
                if (fish == null || fish.isDead)
                {
                    continue;
                }

                bool usesThisWater = fish.waterCollider == waterCollider;
                if (!usesThisWater && waterBounds.Contains(fish.transform.position))
                {
                    usesThisWater = true;
                }

                if (!usesThisWater)
                {
                    continue;
                }

                FishCount++;
                FishOxygenLoad += Mathf.Max(0f, oxygenUsePerFish);
            }

            foreach (AquariumOxygenProvider provider in FindObjectsByType<AquariumOxygenProvider>(FindObjectsSortMode.None))
            {
                if (provider != null && IsProviderInWater(provider, waterBounds))
                {
                    PlantOxygenCapacity += provider.OxygenCapacityBonus;
                }
            }
        }
    }

    private bool IsProviderInWater(AquariumOxygenProvider provider, Bounds waterBounds)
    {
        if (waterBounds.Contains(provider.transform.position))
        {
            return true;
        }

        foreach (Renderer renderer in provider.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer != null && waterBounds.Intersects(renderer.bounds))
            {
                return true;
            }
        }

        foreach (Collider collider in provider.GetComponentsInChildren<Collider>(true))
        {
            if (collider != null && waterBounds.Intersects(collider.bounds))
            {
                return true;
            }
        }

        return false;
    }

    private void EnsureStarterOxygenPlants()
    {
        if (!createStarterPlants || starterPlantCount <= 0 || waterCollider == null)
        {
            return;
        }

        Bounds waterBounds = waterCollider.bounds;
        int existingPlants = 0;
        foreach (AquariumOxygenProvider provider in FindObjectsByType<AquariumOxygenProvider>(FindObjectsSortMode.None))
        {
            if (provider != null && IsProviderInWater(provider, waterBounds))
            {
                existingPlants++;
            }
        }

        if (existingPlants > 0)
        {
            return;
        }

        Material darkGreen = CreateRuntimePlantMaterial(new Color(0.08f, 0.45f, 0.18f));
        Material lightGreen = CreateRuntimePlantMaterial(new Color(0.18f, 0.72f, 0.32f));
        Material redGreen = CreateRuntimePlantMaterial(new Color(0.44f, 0.18f, 0.22f));
        Material stemMaterial = CreateRuntimePlantMaterial(new Color(0.07f, 0.28f, 0.12f));
        Material stoneMaterial = CreateRuntimePlantMaterial(new Color(0.38f, 0.34f, 0.30f));

        int count = Mathf.Clamp(starterPlantCount, 1, 3);
        for (int i = 0; i < count; i++)
        {
            float xOffset = count == 1 ? 0f : Mathf.Lerp(-0.28f, 0.28f, i / Mathf.Max(1f, count - 1f));
            Vector3 position = new(
                waterBounds.center.x + xOffset,
                waterBounds.min.y + 0.02f,
                waterBounds.center.z - 0.12f + i * 0.08f);

            if (i == 0)
            {
                CreateGrassCluster(position, lightGreen, stemMaterial, stoneMaterial);
            }
            else if (i == 1)
            {
                CreateBroadLeafPlant(position, darkGreen, stemMaterial, stoneMaterial);
            }
            else
            {
                CreateRedStemPlant(position, redGreen, stemMaterial, stoneMaterial);
            }
        }
    }

    private Material CreateRuntimePlantMaterial(Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        Material material = new(shader);
        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }
        else
        {
            material.color = color;
        }
        return material;
    }

    private GameObject CreatePlantRoot(string plantName, Vector3 position, float oxygenBonus)
    {
        GameObject root = new(plantName);
        root.transform.SetParent(transform, true);
        root.transform.position = position;

        AquariumOxygenProvider provider = root.AddComponent<AquariumOxygenProvider>();
        InventoryPickupItem pickup = root.AddComponent<InventoryPickupItem>();
        pickup.Configure(plantName, new Color(0.18f, 0.72f, 0.32f), null);

        // --- THÊM ĐOẠN NÀY: Khống chế BoxCollider khổng lồ tự sinh ---
        BoxCollider autoCol = root.GetComponent<BoxCollider>();
        if (autoCol != null)
        {
            autoCol.size = new Vector3(0.01f, 0.01f, 0.01f);
            autoCol.enabled = false;
        }
        // -----------------------------------------------------------

        provider.Configure(plantName, oxygenBonus);
        return root;
    }

    private void CreateGrassCluster(Vector3 position, Material leaf, Material stem, Material stone)
    {
        GameObject root = CreatePlantRoot("AquaticPlant_GrassCluster", position, 1.5f);
        CreatePlantPart("StoneBase", root.transform, stone, new Vector3(0f, 0.018f, 0f), Quaternion.identity, new Vector3(0.18f, 0.035f, 0.13f));

        for (int i = 0; i < 12; i++)
        {
            float angle = i * 30f;
            float height = 0.34f + (i % 4) * 0.055f;
            Vector3 localPos = Quaternion.Euler(0f, angle, 0f) * new Vector3(0.035f + (i % 3) * 0.01f, height * 0.5f, 0f);
            CreatePlantPart("Blade", root.transform, leaf, localPos, Quaternion.Euler(14f + (i % 5) * 4f, angle, 8f), new Vector3(0.018f, height, 0.01f));
        }
    }

    private void CreateBroadLeafPlant(Vector3 position, Material leaf, Material stem, Material stone)
    {
        GameObject root = CreatePlantRoot("AquaticPlant_BroadLeaf", position, 2f);
        CreatePlantPart("StoneBase", root.transform, stone, new Vector3(0f, 0.018f, 0f), Quaternion.identity, new Vector3(0.18f, 0.035f, 0.13f));

        for (int i = 0; i < 7; i++)
        {
            float angle = i * 51.4f;
            float height = 0.22f + (i % 3) * 0.045f;
            CreatePlantPart("Stem", root.transform, stem, Quaternion.Euler(0f, angle, 0f) * new Vector3(0.025f, height * 0.5f, 0f), Quaternion.Euler(0f, angle, 6f), new Vector3(0.012f, height, 0.012f));
            CreatePlantPart("Leaf", root.transform, leaf, Quaternion.Euler(0f, angle, 0f) * new Vector3(0.055f, height + 0.045f, 0f), Quaternion.Euler(20f, angle, 28f), new Vector3(0.07f, 0.018f, 0.035f));
        }
    }

    private void CreateRedStemPlant(Vector3 position, Material leaf, Material stem, Material stone)
    {
        GameObject root = CreatePlantRoot("AquaticPlant_RedStem", position, 2.5f);
        CreatePlantPart("StoneBase", root.transform, stone, new Vector3(0f, 0.018f, 0f), Quaternion.identity, new Vector3(0.18f, 0.035f, 0.13f));

        for (int i = 0; i < 5; i++)
        {
            float angle = i * 72f;
            float height = 0.38f + (i % 2) * 0.08f;
            CreatePlantPart("TallStem", root.transform, stem, Quaternion.Euler(0f, angle, 0f) * new Vector3(0.035f, height * 0.5f, 0f), Quaternion.Euler(4f, angle, 4f), new Vector3(0.013f, height, 0.013f));

            for (int leafIndex = 0; leafIndex < 3; leafIndex++)
            {
                CreatePlantPart(
                    "RedLeaf",
                    root.transform,
                    leaf,
                    Quaternion.Euler(0f, angle + leafIndex * 28f, 0f) * new Vector3(0.055f, height * (0.45f + leafIndex * 0.18f), 0f),
                    Quaternion.Euler(18f, angle + leafIndex * 28f, leafIndex % 2 == 0 ? 28f : -28f),
                    new Vector3(0.055f, 0.016f, 0.026f));
            }
        }
    }

    private void CreatePlantPart(string partName, Transform parent, Material material, Vector3 localPosition, Quaternion localRotation, Vector3 localScale)
    {
        GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
        part.name = partName;
        part.transform.SetParent(parent, false);
        part.transform.localPosition = localPosition;
        part.transform.localRotation = localRotation;
        part.transform.localScale = localScale;

        Renderer renderer = part.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = material;
        }
    }

    private void TryUpgradeAerator()
    {
        if (aeratorLevel >= maxAeratorLevel)
        {
            Debug.Log("Aerator is already at max level.");
            return;
        }

        int upgradeCost = aeratorUpgradeCost * aeratorLevel;
        if (!PlayerMoneyDisplay.TrySpendMoney(upgradeCost))
        {
            Debug.Log($"Not enough money to upgrade aerator. Need {upgradeCost} VND.");
            return;
        }

        aeratorLevel++;
        UpdateAeratorVisuals();
        Debug.Log($"Aerator upgraded to level {aeratorLevel}.");
    }

    private void AutoFindAeratorVisual()
    {
        if (aeratorVisualRoot == null || aeratorRenderer == null)
        {
            foreach (Transform child in GetComponentsInChildren<Transform>(true))
            {
                string name = child.gameObject.name.ToLower();
                if (!name.Contains("loc") && !name.Contains("filter") && !name.Contains("aerator") && !name.Contains("oxi"))
                {
                    continue;
                }

                if (aeratorVisualRoot == null)
                {
                    aeratorVisualRoot = child;
                }

                if (aeratorRenderer == null)
                {
                    aeratorRenderer = child.GetComponentInChildren<Renderer>();
                }

                if (aeratorVisualRoot != null && aeratorRenderer != null)
                {
                    break;
                }
            }
        }
    }

    private void OnGUI()
    {
        if (PauseMenuManager.GameIsPaused || !isPlayerNear || playerInventory == null || AquariumDecorationMode.IsDecorationMode) return;

        EnsureStyles();

        // 1. Draw fish tank stats box at the bottom
        float width = 360f;
        float height = 150f;
        Rect rect = new((Screen.width - width) * 0.5f, Screen.height - 280f, width, height);
        
        string cleanPercent = Mathf.RoundToInt(Cleanliness * 100f) + "%";
        string oxyPercent = Mathf.RoundToInt(Oxygen * 100f) + "%";
        string clorPercent = Mathf.RoundToInt(Chlorine * 100f) + "%";
        string sActive = IsAeratorOn ? "ON" : "OFF";

        string content = $"<b>[ HOME FISH TANK ]</b>\n" +
                         $"- Water Cleanliness: {cleanPercent}\n" +
                         $"- Dissolved Oxygen: {oxyPercent} (Aerator L{aeratorLevel}: {sActive} - Key [T])\n" +
                         $"- Fish/Oxygen: {FishCount} fish | Load {FishOxygenLoad:0.#}/{TotalOxygenCapacity:0.#} (Plants +{PlantOxygenCapacity:0.#})\n" +
                         $"- Chlorine Level: {clorPercent} (Dechlorinate: Key [R] - 50 VND)\n" +
                         $"- Upgrade aerator: Key [U] - {GetNextAeratorCostText()}";

        GUI.Box(rect, content, boxStyle);

        // 2. Draw refill/pour prompt instructions
        SimpleInventory.InventoryItem selectedItem = playerInventory.GetSelectedItem();
        bool hasHeldWaterBucket = selectedItem != null && SimpleInventory.IsWaterBucket(selectedItem.Name);

        InventoryPickupItem nearestDroppedWaterBucket = null;
        if (!hasHeldWaterBucket)
        {
            nearestDroppedWaterBucket = FindNearestDroppedWaterBucket(transform.position, interactionDistance);
        }

        if (hasHeldWaterBucket || nearestDroppedWaterBucket != null)
        {
            Rect interactRect = new((Screen.width - 320f) * 0.5f, Screen.height - 160f, 320f, 42f);
            GUI.Box(interactRect, "Press [F] to pour clean water into tank", warningStyle);
        }
        else
        {
            if (Cleanliness < 0.4f)
            {
                Rect warningRect = new((Screen.width - 360f) * 0.5f, Screen.height - 160f, 360f, 42f);
                GUI.Box(warningRect, "Dirty water! Get a clean water bucket from NVS to refill!", warningStyle);
            }
        }
    }

    private string GetNextAeratorCostText()
    {
        return aeratorLevel >= maxAeratorLevel ? "MAX" : $"{aeratorUpgradeCost * aeratorLevel} VND";
    }

    private InventoryPickupItem FindNearestDroppedWaterBucket(Vector3 center, float maxDistance)
    {
        InventoryPickupItem nearest = null;
        float bestDist = maxDistance;

        foreach (InventoryPickupItem item in FindObjectsByType<InventoryPickupItem>(FindObjectsSortMode.None))
        {
            if (SimpleInventory.IsWaterBucket(item.ItemName))
            {
                Collider col = item.GetComponent<Collider>();
                Vector3 targetPos = col != null ? col.bounds.center : item.transform.position;
                float dist = Vector3.Distance(center, targetPos);
                if (dist < bestDist)
                {
                    bestDist = dist;
                    nearest = item;
                }
            }
        }

        return nearest;
    }

    private void EnsureStyles()
    {
        if (boxStyle == null)
        {
            boxStyle = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.UpperLeft,
                fontSize = 13,
                padding = new RectOffset(12, 12, 10, 10)
            };
            boxStyle.normal.textColor = Color.white;
        }
        if (warningStyle == null)
        {
            warningStyle = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 15,
                fontStyle = FontStyle.Bold
            };
            warningStyle.normal.textColor = new Color(1.0f, 0.8f, 0.2f);
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        AutoFindPrefabs();
    }

    private void AutoFindPrefabs()
    {
        if (emptyBucketPrefab == null)
        {
            string[] guids = UnityEditor.AssetDatabase.FindAssets("Bucket t:Prefab");
            if (guids.Length > 0)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                emptyBucketPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }
            if (emptyBucketPrefab == null)
            {
                guids = UnityEditor.AssetDatabase.FindAssets("Bucket t:Model");
                if (guids.Length > 0)
                {
                    string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                    emptyBucketPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
                }
            }
        }
        if (waterBucketPrefab == null)
        {
            string[] guids = UnityEditor.AssetDatabase.FindAssets("WaterBucket t:Prefab");
            if (guids.Length > 0)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                waterBucketPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }
        }
    }
#endif
}
