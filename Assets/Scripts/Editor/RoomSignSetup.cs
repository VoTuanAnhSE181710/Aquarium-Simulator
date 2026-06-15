using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

public static class RoomSignSetup
{
    private const string HouseLineName = "HouseLine";
    private const string ContainerName = "Room Door Numbers";
    private const string MaterialFolder = "Assets/GeneratedMaterials/RoomSigns";
    private const string MaterialPath = MaterialFolder + "/RoomSign_DarkBlue.mat";
    private static readonly Vector3 PlaquePositionOffset = new(-1.24f, 0.82f, -0.08f);
    private static readonly Vector3 PlaqueScale = new(0.42f, 0.18f, 0.016f);
    private static readonly Vector3 NumberPositionOffset = new(0f, 0f, -0.011f);
    private static readonly Vector3 NumberScale = new(0.022f, 0.022f, 0.022f);

    [MenuItem("Tools/Aquarium Simulator/Normalize Room Door Numbers")]
    public static void Apply()
    {
        GameObject houseLine = GameObject.Find(HouseLineName);
        if (houseLine == null)
        {
            Debug.LogWarning("Room sign setup skipped because HouseLine was not found.");
            return;
        }

        Material plaqueMaterial = GetOrCreatePlaqueMaterial();
        Transform[] modularDoors = houseLine.GetComponentsInChildren<Transform>(true)
            .Where(transform =>
                transform.name == "Door" &&
                transform.parent != null &&
                transform.parent.name.StartsWith("ModularHousePieces", StringComparison.Ordinal))
            .ToArray();

        RemoveExistingSigns(houseLine.transform, modularDoors);
        RemoveExistingContainer();

        Transform[] visibleDoors = modularDoors
            .GroupBy(transform => new Vector3Int(
                Mathf.RoundToInt(transform.position.x * 1000f),
                Mathf.RoundToInt(transform.position.y * 1000f),
                Mathf.RoundToInt(transform.position.z * 1000f)))
            .Select(group => group.OrderBy(transform => transform.GetInstanceID()).First())
            .OrderBy(transform => transform.position.x)
            .ThenBy(transform => transform.position.z)
            .ToArray();

        GameObject container = new(ContainerName);
        for (int index = 0; index < visibleDoors.Length; index++)
        {
            CreateRoomNumber(visibleDoors[index], index + 1, container.transform, plaqueMaterial);
        }

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        AssetDatabase.SaveAssets();

        Debug.Log(
            $"Room signs normalized. Modular doors: {modularDoors.Length}, " +
            $"visible door positions: {visibleDoors.Length}, " +
            $"labels: 01-{visibleDoors.Length:00}.");
    }

    private static Material GetOrCreatePlaqueMaterial()
    {
        EnsureAssetFolder(MaterialFolder);

        Material material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ??
                        Shader.Find("Universal Render Pipeline/Lit") ??
                        Shader.Find("Standard");
        if (material == null)
        {
            material = new Material(shader)
            {
                name = "RoomSign_DarkBlue"
            };
            AssetDatabase.CreateAsset(material, MaterialPath);
        }
        else
        {
            material.shader = shader;
        }

        Color color = new(0.035f, 0.12f, 0.22f, 1f);
        material.color = color;
        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }

        EditorUtility.SetDirty(material);
        return material;
    }

    private static void RemoveExistingSigns(Transform houseLine, Transform[] doors)
    {
        TextMesh[] allTextMeshes = houseLine.GetComponentsInChildren<TextMesh>(true);
        foreach (TextMesh textMesh in allTextMeshes)
        {
            UnityEngine.Object.DestroyImmediate(textMesh.gameObject);
        }

        foreach (Transform door in doors)
        {
            Transform[] oldSigns = door.Cast<Transform>()
                .Where(child =>
                    child.name.StartsWith("RoomSign", StringComparison.Ordinal) ||
                    child.name.StartsWith("DoorNumber", StringComparison.Ordinal))
                .ToArray();

            foreach (Transform oldSign in oldSigns)
            {
                UnityEngine.Object.DestroyImmediate(oldSign.gameObject);
            }
        }
    }

    private static void RemoveExistingContainer()
    {
        GameObject container = GameObject.Find(ContainerName);
        if (container != null)
        {
            UnityEngine.Object.DestroyImmediate(container);
        }
    }

    private static void CreateRoomNumber(
        Transform door,
        int roomNumber,
        Transform container,
        Material plaqueMaterial)
    {
        GameObject plaqueObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        plaqueObject.name = $"DoorNumberPlaque_{roomNumber:00}";
        plaqueObject.transform.SetParent(container, false);
        plaqueObject.transform.position = door.position + PlaquePositionOffset;
        plaqueObject.transform.rotation = Quaternion.identity;
        plaqueObject.transform.localScale = PlaqueScale;

        UnityEngine.Object.DestroyImmediate(plaqueObject.GetComponent<Collider>());
        MeshRenderer plaqueRenderer = plaqueObject.GetComponent<MeshRenderer>();
        plaqueRenderer.sharedMaterial = plaqueMaterial;
        plaqueRenderer.shadowCastingMode = ShadowCastingMode.Off;
        plaqueRenderer.receiveShadows = false;

        GameObject textObject = new($"DoorNumber_{roomNumber:00}");
        textObject.transform.SetParent(plaqueObject.transform, false);
        textObject.transform.localPosition = NumberPositionOffset;
        textObject.transform.localRotation = Quaternion.identity;
        textObject.transform.localScale = NumberScale;

        TextMesh textMesh = textObject.AddComponent<TextMesh>();
        textMesh.text = roomNumber.ToString("00");
        textMesh.fontSize = 100;
        textMesh.characterSize = 1f;
        textMesh.fontStyle = FontStyle.Bold;
        textMesh.anchor = TextAnchor.MiddleCenter;
        textMesh.alignment = TextAlignment.Center;
        textMesh.richText = false;
        textMesh.color = Color.white;

        MeshRenderer textRenderer = textObject.GetComponent<MeshRenderer>();
        textRenderer.shadowCastingMode = ShadowCastingMode.Off;
        textRenderer.receiveShadows = false;
    }

    private static void EnsureAssetFolder(string assetFolder)
    {
        string[] segments = assetFolder.Split('/');
        string currentPath = segments[0];

        for (int index = 1; index < segments.Length; index++)
        {
            string nextPath = currentPath + "/" + segments[index];
            if (!AssetDatabase.IsValidFolder(nextPath))
            {
                AssetDatabase.CreateFolder(currentPath, segments[index]);
            }

            currentPath = nextPath;
        }
    }
}
