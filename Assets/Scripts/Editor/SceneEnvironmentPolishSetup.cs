using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class SceneEnvironmentPolishSetup
{
    private const string SessionKey = "AquariumSimulator.SceneEnvironmentPolish.v5";
    private const string ScenePath = "Assets/Scenes/SampleScene.unity";
    private const string ParkMaterialPath = "Assets/Park/Materials/Park_Material.mat";
    private const string ParkTexturePath = "Assets/Park/Textures/Park-Texture.tga";
    private const string ParkSpecularPath = "Assets/Park/Textures/Park-Specular.tga";
    private const string BrickWallMaterialPath = "Assets/FencesPack/Materials/BrickRed.mat";
    private const string BoundaryWallMaterialPath =
        "Assets/TextureHaven/bricks_4k/bricks_4k_materials/castle_brick_07.mat";
    private const string BoundaryWallName = "Boundary Wall";
    private const string BoundaryCornerContainerName = "Boundary Wall Corner Fillers";
    private const string BoundaryCornerPrefix = "Boundary Wall Corner ";
    private const string GeneratedRoofMaterialFolder = "Assets/GeneratedMaterials/Roofs";
    private const string StreetSpotLightName = "Street Spot Light";
    private const string CharacterHouseName = "CharacterHouse (1)";

    private static readonly string[] CharacterNames =
    {
        "Linh",
        "Minh",
        "MissLan",
        "Tuan",
        "UncleHung",
        "Ch01_nonPBR",
        "Ch02_nonPBR",
        "Ch06_nonPBR",
        "Ch07_nonPBR",
        "Ch08_nonPBR",
        "Ch21_nonPBR",
        "Ch22_nonPBR",
        "Ch23_nonPBR",
        "Ch26_nonPBR",
        "Ch27_nonPBR",
        "Ch28_nonPBR",
        "Ch31_nonPBR",
        "Ch33_nonPBR",
        "Ch37_nonPBR",
        "Ch41_nonPBR",
        "Ch42_nonPBR",
        "Remy"
    };

    private static readonly string[] DecorativeColliderKeywords =
    {
        "grasse",
        "flower",
        "leaf",
        "foliage",
        "water",
        "sky",
        "roof",
        "celling",
        "ceiling",
        "windowcolor",
        "window_color"
    };

    static SceneEnvironmentPolishSetup()
    {
        EditorApplication.delayCall += TryAutoApply;
        EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
    }

    [MenuItem("Tools/Aquarium Simulator/Synchronize Scene Environment")]
    public static void Apply()
    {
        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() || !scene.isLoaded)
        {
            Debug.LogWarning("Scene environment setup skipped because no scene is loaded.");
            return;
        }

        int alignedRootCount = AlignSceneRoots();
        int addedColliderCount = SetupEnvironmentColliders();
        int characterHouseColliderCount = SetupCharacterHouseColliders();
        int removedColliderCount = RemoveDecorativeMeshColliders();
        int groundedPropCount = GroundParkProps();
        int rendererCount = ImproveRendererQuality();
        ImproveParkMaterial();
        ImproveOpaqueWallMaterials();
        int roofRendererCount = SetupOpaqueRoofMaterials();
        ImproveTextureImport(ParkTexturePath);
        ImproveTextureImport(ParkSpecularPath);
        SetupDirectionalLight();
        int streetSpotLightCount = SetupStreetSpotLights();
        int boundaryCornerCount = SetupBoundaryWallCorners();
        SetupParkReflectionProbe();

        EditorSceneManager.MarkSceneDirty(scene);
        if (scene.path.StartsWith("Temp/__Backupscenes", StringComparison.OrdinalIgnoreCase))
        {
            EditorSceneManager.SaveScene(scene, ScenePath);
        }
        else
        {
            EditorSceneManager.SaveOpenScenes();
        }
        AssetDatabase.SaveAssets();
        SessionState.SetBool(SessionKey, true);

        Debug.Log(
            "Scene environment synchronized. " +
            $"Aligned roots: {alignedRootCount}, " +
            $"added colliders: {addedColliderCount}, " +
            $"character house colliders: {characterHouseColliderCount}, " +
            $"removed decorative colliders: {removedColliderCount}, " +
            $"grounded props: {groundedPropCount}, " +
            $"street spot lights: {streetSpotLightCount}, " +
            $"boundary corners: {boundaryCornerCount}, " +
            $"opaque roofs: {roofRendererCount}, " +
            $"quality renderers: {rendererCount}.");
    }

    public static void ApplyBatch()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Apply();
    }

    public static void DumpBrickWallLayoutBatch()
    {
        EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        Transform[] wallRoots = UnityEngine.Object.FindObjectsByType<Transform>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None)
            .Where(transform => IsBrickWallRoot(transform.name))
            .OrderBy(transform => transform.name)
            .ToArray();

        foreach (Transform wallRoot in wallRoots)
        {
            if (!TryGetCombinedBounds(wallRoot.gameObject, out Bounds bounds))
            {
                continue;
            }

            bool runsAlongX = bounds.size.x >= bounds.size.z;
            Vector3 direction = runsAlongX ? Vector3.right : Vector3.forward;
            float extent = runsAlongX ? bounds.extents.x : bounds.extents.z;
            Vector3 firstEndpoint = bounds.center - direction * extent;
            Vector3 secondEndpoint = bounds.center + direction * extent;

            Debug.Log(
                $"Brick layout: {wallRoot.name}; " +
                $"position={wallRoot.position:F3}; rotation={wallRoot.eulerAngles:F1}; scale={wallRoot.localScale:F3}; " +
                $"boundsCenter={bounds.center:F3}; boundsSize={bounds.size:F3}; " +
                $"endpoints={firstEndpoint:F3}|{secondEndpoint:F3}.");
        }

        GameObject boundaryWall = GameObject.Find(BoundaryWallName);
        if (boundaryWall == null)
        {
            return;
        }

        foreach (Renderer renderer in GetBoundaryWallRenderers(boundaryWall))
        {
            Bounds bounds = renderer.bounds;
            Debug.Log(
                $"Boundary layout: {renderer.name}; " +
                $"position={renderer.transform.position:F3}; rotation={renderer.transform.eulerAngles:F1}; " +
                $"scale={renderer.transform.localScale:F3}; boundsCenter={bounds.center:F3}; boundsSize={bounds.size:F3}; " +
                $"boundsMin={bounds.min:F3}; boundsMax={bounds.max:F3}.");
        }
    }

    private static void TryAutoApply()
    {
        if (SessionState.GetBool(SessionKey, false) ||
            EditorApplication.isPlayingOrWillChangePlaymode ||
            EditorApplication.isCompiling ||
            EditorApplication.isUpdating)
        {
            return;
        }

        Scene scene = SceneManager.GetActiveScene();
        if (!scene.IsValid() ||
            !scene.isLoaded ||
            (scene.path != ScenePath &&
             !scene.path.StartsWith("Temp/__Backupscenes", StringComparison.OrdinalIgnoreCase)))
        {
            return;
        }

        Apply();
    }

    private static void OnPlayModeStateChanged(PlayModeStateChange state)
    {
        if (state == PlayModeStateChange.EnteredEditMode)
        {
            EditorApplication.delayCall += TryAutoApply;
        }
    }

    private static int AlignSceneRoots()
    {
        int count = 0;
        count += SetRootElevation("road3", 0f);
        count += SetRootElevation("road4", 0f);
        count += SetRootElevation("curb", 0.5f);
        count += AlignParkSurfaceToCurb();
        return count;
    }

    private static int AlignParkSurfaceToCurb()
    {
        GameObject park = GameObject.Find("CustomPark");
        GameObject curb = GameObject.Find("curb");
        if (park == null || curb == null)
        {
            return 0;
        }

        float[] parkSurfaceHeights = park.GetComponentsInChildren<Renderer>(true)
            .Where(renderer => IsParkSurface(renderer.gameObject))
            .Select(renderer => renderer.bounds.max.y)
            .ToArray();
        float[] curbHeights = curb.GetComponentsInChildren<Renderer>(true)
            .Select(renderer => renderer.bounds.max.y)
            .ToArray();

        if (parkSurfaceHeights.Length == 0 || curbHeights.Length == 0)
        {
            return 0;
        }

        float offset = Median(curbHeights) - Median(parkSurfaceHeights);
        if (Mathf.Abs(offset) < 0.001f)
        {
            return 0;
        }

        Undo.RecordObject(park.transform, "Align park surface to curb");
        park.transform.position += Vector3.up * offset;
        return 1;
    }

    private static int SetRootElevation(string objectName, float y)
    {
        GameObject gameObject = GameObject.Find(objectName);
        if (gameObject == null || Mathf.Approximately(gameObject.transform.position.y, y))
        {
            return 0;
        }

        Undo.RecordObject(gameObject.transform, $"Align {objectName}");
        Vector3 position = gameObject.transform.position;
        position.y = y;
        gameObject.transform.position = position;
        return 1;
    }

    private static int SetupEnvironmentColliders()
    {
        int count = 0;
        MeshFilter[] meshFilters = UnityEngine.Object.FindObjectsByType<MeshFilter>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        foreach (MeshFilter meshFilter in meshFilters)
        {
            GameObject gameObject = meshFilter.gameObject;
            if (meshFilter.sharedMesh == null ||
                gameObject.GetComponent<Collider>() != null ||
                IsPartOfCharacter(gameObject) ||
                IsDecorative(gameObject))
            {
                continue;
            }

            MeshCollider collider = Undo.AddComponent<MeshCollider>(gameObject);
            collider.sharedMesh = meshFilter.sharedMesh;
            count++;
        }

        return count;
    }

    private static int RemoveDecorativeMeshColliders()
    {
        int count = 0;
        MeshCollider[] colliders = UnityEngine.Object.FindObjectsByType<MeshCollider>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        foreach (MeshCollider collider in colliders)
        {
            if (!IsDecorative(collider.gameObject))
            {
                continue;
            }

            Undo.DestroyObjectImmediate(collider);
            count++;
        }

        return count;
    }

    private static int SetupCharacterHouseColliders()
    {
        GameObject characterHouse = GameObject.Find(CharacterHouseName);
        if (characterHouse == null)
        {
            return 0;
        }

        int count = 0;
        Type collisionGuardType = Type.GetType("CharacterHouseCollisionGuard, Assembly-CSharp");
        if (collisionGuardType != null && characterHouse.GetComponent(collisionGuardType) == null)
        {
            Undo.AddComponent(characterHouse, collisionGuardType);
            count++;
        }

        MeshFilter[] meshFilters = characterHouse.GetComponentsInChildren<MeshFilter>(true);

        foreach (MeshFilter meshFilter in meshFilters)
        {
            if (meshFilter.sharedMesh == null)
            {
                continue;
            }

            GameObject gameObject = meshFilter.gameObject;
            string path = GetHierarchyPath(gameObject.transform).ToLowerInvariant();
            bool shouldBlock =
                path.Contains("/floors") ||
                path.Contains("/wall") ||
                path.Contains("/door");

            if (!shouldBlock || gameObject.GetComponent<Collider>() != null)
            {
                continue;
            }

            MeshCollider collider = Undo.AddComponent<MeshCollider>(gameObject);
            collider.sharedMesh = meshFilter.sharedMesh;
            count++;
        }

        Transform floors = characterHouse.transform.Find("Floors");
        if (floors != null && floors.GetComponent<Collider>() == null)
        {
            Renderer[] floorRenderers = floors.GetComponentsInChildren<Renderer>(true)
                .Where(renderer => renderer.name.StartsWith("Floor", StringComparison.OrdinalIgnoreCase))
                .ToArray();

            if (floorRenderers.Length > 0)
            {
                Bounds bounds = floorRenderers[0].bounds;
                foreach (Renderer renderer in floorRenderers.Skip(1))
                {
                    bounds.Encapsulate(renderer.bounds);
                }

                BoxCollider floorCollider = Undo.AddComponent<BoxCollider>(floors.gameObject);
                floorCollider.center = floors.InverseTransformPoint(bounds.center);
                floorCollider.size = floors.InverseTransformVector(bounds.size + new Vector3(0f, 0.1f, 0f));
                count++;
            }
        }

        return count;
    }

    private static int GroundParkProps()
    {
        GameObject park = GameObject.Find("CustomPark");
        if (park == null)
        {
            return 0;
        }

        Physics.SyncTransforms();
        int count = 0;
        foreach (Transform child in park.transform)
        {
            if (!ShouldGroundParkProp(child.gameObject) ||
                !TryGetCombinedBounds(child.gameObject, out Bounds bounds))
            {
                continue;
            }

            RaycastHit[] hits = Physics.RaycastAll(
                    new Vector3(bounds.center.x, bounds.max.y + 20f, bounds.center.z),
                    Vector3.down,
                    100f,
                    Physics.DefaultRaycastLayers,
                    QueryTriggerInteraction.Ignore)
                .OrderBy(hit => hit.distance)
                .ToArray();

            foreach (RaycastHit hit in hits)
            {
                if (hit.transform == null ||
                    hit.transform.IsChildOf(child) ||
                    IsPartOfCharacter(hit.transform.gameObject) ||
                    IsDecorative(hit.transform.gameObject))
                {
                    continue;
                }

                float offset = hit.point.y - bounds.min.y;
                if (Mathf.Abs(offset) < 0.001f)
                {
                    break;
                }

                Undo.RecordObject(child, $"Ground {child.name}");
                child.position += Vector3.up * offset;
                count++;
                Physics.SyncTransforms();
                break;
            }
        }

        return count;
    }

    private static int ImproveRendererQuality()
    {
        int count = 0;
        Renderer[] renderers = UnityEngine.Object.FindObjectsByType<Renderer>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        foreach (Renderer renderer in renderers)
        {
            if (IsPartOfCharacter(renderer.gameObject))
            {
                continue;
            }

            Undo.RecordObject(renderer, "Improve renderer quality");
            renderer.receiveShadows = true;
            renderer.shadowCastingMode =
                IsDecorative(renderer.gameObject) ? ShadowCastingMode.Off : ShadowCastingMode.On;
            renderer.lightProbeUsage = LightProbeUsage.BlendProbes;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.BlendProbes;
            count++;
        }

        QualitySettings.shadows = ShadowQuality.All;
        QualitySettings.shadowResolution = ShadowResolution.VeryHigh;
        QualitySettings.shadowDistance = 100f;
        QualitySettings.antiAliasing = 4;
        QualitySettings.anisotropicFiltering = AnisotropicFiltering.ForceEnable;
        return count;
    }

    private static void ImproveParkMaterial()
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(ParkMaterialPath);
        if (material == null)
        {
            return;
        }

        Undo.RecordObject(material, "Improve park material");
        material.enableInstancing = true;
        material.SetFloat("_Metallic", 0f);
        material.SetFloat("_Smoothness", 0.35f);
        material.SetFloat("_Glossiness", 0.35f);
        EditorUtility.SetDirty(material);
    }

    private static void ImproveOpaqueWallMaterials()
    {
        ConfigureOpaqueDoubleSidedMaterial(BrickWallMaterialPath);
        ConfigureOpaqueDoubleSidedMaterial(BoundaryWallMaterialPath);
    }

    private static void ConfigureOpaqueDoubleSidedMaterial(string assetPath)
    {
        Material material = AssetDatabase.LoadAssetAtPath<Material>(assetPath);
        if (material == null)
        {
            return;
        }

        ConfigureOpaqueDoubleSidedMaterial(material, "Configure opaque double-sided wall material");
    }

    private static void ConfigureOpaqueDoubleSidedMaterial(Material material, string undoName)
    {
        Undo.RecordObject(material, undoName);
        material.SetOverrideTag("RenderType", "Opaque");
        material.renderQueue = -1;
        material.doubleSidedGI = true;
        material.SetFloat("_Surface", 0f);
        material.SetFloat("_AlphaClip", 0f);
        material.SetFloat("_Cull", 0f);
        material.SetFloat("_ZWrite", 1f);
        material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.DisableKeyword("_ALPHATEST_ON");
        EditorUtility.SetDirty(material);
    }

    [MenuItem("Tools/Aquarium Simulator/Fix Roof Undersides")]
    public static void FixRoofUndersides()
    {
        int count = SetupOpaqueRoofMaterials();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();
        Debug.Log($"Opaque roof undersides configured: {count}.");
    }

    private static int SetupOpaqueRoofMaterials()
    {
        Renderer[] roofRenderers = UnityEngine.Object.FindObjectsByType<Renderer>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None)
            .Where(renderer => IsRoofRenderer(renderer.gameObject))
            .ToArray();
        Dictionary<Material, Material> overrides = new();

        foreach (Renderer renderer in roofRenderers)
        {
            Material[] materials = renderer.sharedMaterials;
            bool changed = false;

            for (int index = 0; index < materials.Length; index++)
            {
                Material source = materials[index];
                if (source == null)
                {
                    continue;
                }

                if (!overrides.TryGetValue(source, out Material opaqueMaterial))
                {
                    opaqueMaterial = GetOrCreateOpaqueRoofMaterial(source);
                    overrides.Add(source, opaqueMaterial);
                }

                if (materials[index] == opaqueMaterial)
                {
                    continue;
                }

                materials[index] = opaqueMaterial;
                changed = true;
            }

            if (changed)
            {
                Undo.RecordObject(renderer, "Assign opaque roof materials");
                renderer.sharedMaterials = materials;
            }
        }

        return roofRenderers.Length;
    }

    private static Material GetOrCreateOpaqueRoofMaterial(Material source)
    {
        if (AssetDatabase.GetAssetPath(source).StartsWith(
                GeneratedRoofMaterialFolder,
                StringComparison.OrdinalIgnoreCase))
        {
            ConfigureOpaqueDoubleSidedMaterial(source, "Refresh opaque roof material");
            return source;
        }

        EnsureAssetFolder(GeneratedRoofMaterialFolder);
        AssetDatabase.TryGetGUIDAndLocalFileIdentifier(source, out string guid, out long localId);
        string materialName = SanitizeAssetName(source.name);
        string assetPath = $"{GeneratedRoofMaterialFolder}/{materialName}-{guid}-{localId}.mat";
        Material opaqueMaterial = AssetDatabase.LoadAssetAtPath<Material>(assetPath);

        if (opaqueMaterial == null)
        {
            opaqueMaterial = new Material(source)
            {
                name = source.name + " Opaque Roof"
            };
            AssetDatabase.CreateAsset(opaqueMaterial, assetPath);
        }

        ConfigureOpaqueDoubleSidedMaterial(opaqueMaterial, "Configure opaque roof material");
        return opaqueMaterial;
    }

    private static void EnsureAssetFolder(string assetFolder)
    {
        string[] parts = assetFolder.Split('/');
        string current = parts[0];

        for (int index = 1; index < parts.Length; index++)
        {
            string next = current + "/" + parts[index];
            if (!AssetDatabase.IsValidFolder(next))
            {
                AssetDatabase.CreateFolder(current, parts[index]);
            }

            current = next;
        }
    }

    private static string SanitizeAssetName(string assetName)
    {
        string sanitized = new(assetName
            .Select(character =>
                char.IsLetterOrDigit(character) || character == '-' || character == '_'
                    ? character
                    : '_')
            .ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? "RoofMaterial" : sanitized;
    }

    private static void ImproveTextureImport(string assetPath)
    {
        TextureImporter importer = AssetImporter.GetAtPath(assetPath) as TextureImporter;
        if (importer == null)
        {
            return;
        }

        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.filterMode = FilterMode.Trilinear;
        importer.anisoLevel = 8;
        importer.mipmapEnabled = true;
        importer.SaveAndReimport();
    }

    private static void SetupDirectionalLight()
    {
        Light directionalLight = UnityEngine.Object.FindObjectsByType<Light>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None)
            .FirstOrDefault(light => light.type == LightType.Directional);

        if (directionalLight == null)
        {
            return;
        }

        Undo.RecordObject(directionalLight, "Improve directional light");
        directionalLight.shadows = LightShadows.Soft;
        directionalLight.shadowStrength = 0.85f;
        directionalLight.intensity = 1.1f;
        directionalLight.color = new Color(1f, 0.956f, 0.88f);
        RenderSettings.sun = directionalLight;

        if (directionalLight.GetComponent<DayNightCycle>() == null)
        {
            Undo.AddComponent<DayNightCycle>(directionalLight.gameObject);
        }

        Type clockDisplayType = Type.GetType("GameClockDisplay, Assembly-CSharp");
        if (clockDisplayType != null && directionalLight.GetComponent(clockDisplayType) == null)
        {
            Undo.AddComponent(directionalLight.gameObject, clockDisplayType);
        }
    }

    [MenuItem("Tools/Aquarium Simulator/Configure Street Spot Lights")]
    public static void ConfigureStreetSpotLights()
    {
        int count = SetupStreetSpotLights();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        Debug.Log($"Street spot lights configured: {count}.");
    }

    private static int SetupStreetSpotLights()
    {
        Transform[] lampRoots = UnityEngine.Object.FindObjectsByType<Transform>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None)
            .Where(transform => IsStreetLampRoot(transform.name))
            .ToArray();

        int count = 0;
        foreach (Transform lampRoot in lampRoots)
        {
            bool isStreetLight = lampRoot.name.StartsWith("StreetLight", StringComparison.OrdinalIgnoreCase);
            Light spotLight = lampRoot.GetComponentsInChildren<Light>(true)
                .FirstOrDefault(light => light.type != LightType.Directional);

            if (spotLight == null)
            {
                GameObject spotObject = new(StreetSpotLightName);
                Undo.RegisterCreatedObjectUndo(spotObject, "Create street spot light");
                spotObject.transform.SetParent(lampRoot, false);
                spotLight = Undo.AddComponent<Light>(spotObject);

                if (TryGetCombinedBounds(lampRoot.gameObject, out Bounds bounds))
                {
                    spotObject.transform.position = new Vector3(
                        bounds.center.x,
                        bounds.max.y - Mathf.Max(0.12f, bounds.size.y * 0.05f),
                        bounds.center.z);
                }
                else
                {
                    spotObject.transform.localPosition = Vector3.up * 3f;
                }
            }

            Undo.RecordObject(spotLight.transform, "Aim street spot light");
            Undo.RecordObject(spotLight, "Configure street spot light");
            spotLight.transform.rotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);
            spotLight.type = LightType.Spot;
            spotLight.color = new Color(1f, 0.78f, 0.48f);
            spotLight.intensity = isStreetLight ? 18f : 10f;
            spotLight.range = isStreetLight ? 14f : 10f;
            spotLight.spotAngle = isStreetLight ? 68f : 74f;
            spotLight.innerSpotAngle = isStreetLight ? 42f : 48f;
            spotLight.shadows = LightShadows.None;
            count++;
        }

        return count;
    }

    [MenuItem("Tools/Aquarium Simulator/Seal Boundary Wall Corners")]
    public static void SealBoundaryWallCorners()
    {
        ImproveOpaqueWallMaterials();
        int count = SetupBoundaryWallCorners();
        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();
        Debug.Log($"Boundary wall corners sealed: {count}.");
    }

    private static int SetupBoundaryWallCorners()
    {
        GameObject boundaryWall = GameObject.Find(BoundaryWallName);
        if (boundaryWall == null)
        {
            return 0;
        }

        Renderer[] wallRenderers = GetBoundaryWallRenderers(boundaryWall);
        Renderer[] horizontalWalls = wallRenderers
            .Where(renderer => renderer.bounds.size.x >= renderer.bounds.size.z)
            .OrderBy(renderer => renderer.bounds.center.z)
            .ToArray();
        Renderer[] verticalWalls = wallRenderers
            .Where(renderer => renderer.bounds.size.z > renderer.bounds.size.x)
            .OrderBy(renderer => renderer.bounds.center.x)
            .ToArray();

        if (horizontalWalls.Length != 2 || verticalWalls.Length != 2)
        {
            Debug.LogWarning(
                $"Boundary corner setup skipped: expected 2 horizontal and 2 vertical walls, " +
                $"found {horizontalWalls.Length} horizontal and {verticalWalls.Length} vertical.");
            return 0;
        }

        float targetBottom = Median(wallRenderers.Select(renderer => renderer.bounds.min.y).ToArray());
        foreach (Renderer renderer in wallRenderers)
        {
            float offset = targetBottom - renderer.bounds.min.y;
            if (Mathf.Abs(offset) < 0.001f)
            {
                continue;
            }

            Undo.RecordObject(renderer.transform, "Align boundary wall elevation");
            renderer.transform.position += Vector3.up * offset;
        }

        Physics.SyncTransforms();
        float leftX = verticalWalls[0].bounds.center.x;
        float rightX = verticalWalls[1].bounds.center.x;
        float frontZ = horizontalWalls[0].bounds.center.z;
        float backZ = horizontalWalls[1].bounds.center.z;
        float width = Mathf.Max(0.5f, verticalWalls.Max(renderer => renderer.bounds.size.x));
        float depth = Mathf.Max(0.5f, horizontalWalls.Max(renderer => renderer.bounds.size.z));
        float height = wallRenderers.Min(renderer => renderer.bounds.size.y);
        Material material = wallRenderers
            .Select(renderer => renderer.sharedMaterial)
            .FirstOrDefault(sharedMaterial => sharedMaterial != null);

        Transform container = boundaryWall.transform.Find(BoundaryCornerContainerName);
        if (container == null)
        {
            GameObject containerObject = new(BoundaryCornerContainerName);
            Undo.RegisterCreatedObjectUndo(containerObject, "Create boundary corner container");
            container = containerObject.transform;
            container.SetParent(boundaryWall.transform, false);
        }

        Vector3[] cornerPositions =
        {
            new(leftX, targetBottom + height * 0.5f, frontZ),
            new(rightX, targetBottom + height * 0.5f, frontZ),
            new(leftX, targetBottom + height * 0.5f, backZ),
            new(rightX, targetBottom + height * 0.5f, backZ)
        };

        for (int index = 0; index < cornerPositions.Length; index++)
        {
            string cornerName = BoundaryCornerPrefix + (index + 1).ToString("00");
            Transform corner = container.Find(cornerName);
            if (corner == null)
            {
                GameObject cornerObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
                Undo.RegisterCreatedObjectUndo(cornerObject, "Create boundary wall corner filler");
                cornerObject.name = cornerName;
                corner = cornerObject.transform;
                corner.SetParent(container, true);
            }

            Undo.RecordObject(corner, "Configure boundary wall corner filler");
            corner.position = cornerPositions[index];
            corner.localScale = new Vector3(width, height, depth);

            Renderer renderer = corner.GetComponent<Renderer>();
            if (renderer != null && material != null)
            {
                Undo.RecordObject(renderer, "Assign boundary wall corner material");
                renderer.sharedMaterial = material;
            }
        }

        foreach (Transform child in container.Cast<Transform>().ToArray())
        {
            if (!child.name.StartsWith(BoundaryCornerPrefix, StringComparison.Ordinal))
            {
                continue;
            }

            string suffix = child.name.Substring(BoundaryCornerPrefix.Length);
            if (!int.TryParse(suffix, out int cornerNumber) ||
                cornerNumber < 1 ||
                cornerNumber > cornerPositions.Length)
            {
                Undo.DestroyObjectImmediate(child.gameObject);
            }
        }

        return cornerPositions.Length;
    }

    private static Renderer[] GetBoundaryWallRenderers(GameObject boundaryWall)
    {
        return boundaryWall.GetComponentsInChildren<Renderer>(true)
            .Where(renderer =>
                renderer.transform.parent == boundaryWall.transform &&
                renderer.name.StartsWith("Huge Wall", StringComparison.OrdinalIgnoreCase))
            .ToArray();
    }

    private static void SetupParkReflectionProbe()
    {
        const string probeName = "Park Reflection Probe";
        GameObject park = GameObject.Find("CustomPark");
        GameObject probeObject = GameObject.Find(probeName);

        if (probeObject == null)
        {
            probeObject = new GameObject(probeName);
            Undo.RegisterCreatedObjectUndo(probeObject, "Create park reflection probe");
        }

        ReflectionProbe probe = probeObject.GetComponent<ReflectionProbe>();
        if (probe == null)
        {
            probe = Undo.AddComponent<ReflectionProbe>(probeObject);
        }

        probeObject.transform.position =
            park != null ? park.transform.position + new Vector3(0f, 4f, 0f) : new Vector3(0f, 4f, 0f);
        probe.mode = ReflectionProbeMode.Realtime;
        probe.refreshMode = ReflectionProbeRefreshMode.OnAwake;
        probe.timeSlicingMode = ReflectionProbeTimeSlicingMode.AllFacesAtOnce;
        probe.size = new Vector3(85f, 16f, 85f);
        probe.intensity = 0.75f;
        probe.boxProjection = true;
    }

    private static bool IsPartOfCharacter(GameObject gameObject)
    {
        Transform current = gameObject.transform;
        while (current != null)
        {
            if (CharacterNames.Contains(current.name, StringComparer.OrdinalIgnoreCase))
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }

    private static bool IsDecorative(GameObject gameObject)
    {
        string hierarchyPath = GetHierarchyPath(gameObject.transform).ToLowerInvariant();
        return DecorativeColliderKeywords.Any(hierarchyPath.Contains);
    }

    private static bool IsParkSurface(GameObject gameObject)
    {
        string name = gameObject.name.ToLowerInvariant();
        return name.Contains("grasstile") ||
               name.Contains("tile_area") ||
               name.Contains("tile_road") ||
               name.Contains("tile_corner") ||
               name.Contains("tile_intersection") ||
               name.Contains("tile_high");
    }

    private static bool IsStreetLampRoot(string name)
    {
        return name.Equals("StreetLight", StringComparison.OrdinalIgnoreCase) ||
               name.StartsWith("StreetLight (", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("Lamp", StringComparison.OrdinalIgnoreCase) ||
               name.StartsWith("Lamp (", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsBrickWallRoot(string name)
    {
        return name.Equals("BrickWall", StringComparison.OrdinalIgnoreCase) ||
               name.StartsWith("BrickWall (", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsRoofRenderer(GameObject gameObject)
    {
        string name = gameObject.name;
        return name.Equals("Roof", StringComparison.OrdinalIgnoreCase) ||
               name.StartsWith("Roof (", StringComparison.OrdinalIgnoreCase) ||
               name.Equals("RoofMiddle", StringComparison.OrdinalIgnoreCase);
    }

    private static bool ShouldGroundParkProp(GameObject gameObject)
    {
        string name = gameObject.name.ToLowerInvariant();
        return !IsParkSurface(gameObject) &&
               (name.Contains("tree") ||
                name.Contains("bench") ||
                name.Contains("lamp") ||
                name.Contains("bush") ||
                name.Contains("flower") ||
                name.Contains("hovel") ||
                name.Contains("trash") ||
                name.Contains("rock") ||
                name.Contains("chair") ||
                name.Contains("fountain") ||
                name.Contains("bridge") ||
                name.Contains("boat") ||
                name.Contains("fence"));
    }

    private static bool TryGetCombinedBounds(GameObject gameObject, out Bounds bounds)
    {
        Renderer[] renderers = gameObject.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            bounds = default;
            return false;
        }

        bounds = renderers[0].bounds;
        foreach (Renderer renderer in renderers.Skip(1))
        {
            bounds.Encapsulate(renderer.bounds);
        }

        return true;
    }

    private static float Median(float[] values)
    {
        Array.Sort(values);
        int middle = values.Length / 2;
        return values.Length % 2 == 0
            ? (values[middle - 1] + values[middle]) * 0.5f
            : values[middle];
    }

    private static string GetHierarchyPath(Transform transform)
    {
        string path = transform.name;
        while (transform.parent != null)
        {
            transform = transform.parent;
            path = transform.name + "/" + path;
        }

        return path;
    }
}
