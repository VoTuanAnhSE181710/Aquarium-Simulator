using System;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[InitializeOnLoad]
public static class InventorySceneSetup
{
    private const string SessionKey = "AquariumSimulator.InventorySceneSetup.v1";
    private const string InventoryTypeName = "SimpleInventory, Assembly-CSharp";
    private const string PickupTypeName = "InventoryPickupItem, Assembly-CSharp";
    private const string PickupContainerName = "Inventory Pickup Items";

    private static readonly (string Name, Color Color, Vector3 Offset)[] SampleItems =
    {
        ("Key", new Color(1f, 0.82f, 0.18f), new Vector3(1.4f, 0.8f, 0.8f)),
        ("Food", new Color(0.25f, 0.82f, 0.35f), new Vector3(1.9f, 0.8f, 0.1f)),
        ("Tool", new Color(0.22f, 0.55f, 1f), new Vector3(1.4f, 0.8f, -0.7f))
    };

    static InventorySceneSetup()
    {
        EditorApplication.delayCall += TryAutoApply;
    }

    [MenuItem("Tools/Aquarium Simulator/Setup Simple Inventory")]
    public static void Apply()
    {
        Type inventoryType = Type.GetType(InventoryTypeName);
        Type pickupType = Type.GetType(PickupTypeName);
        if (inventoryType == null || pickupType == null)
        {
            Debug.LogWarning("Inventory setup skipped because inventory scripts are not compiled yet.");
            return;
        }

        int changedCount = 0;
        GameObject minh = GameObject.Find("Minh");
        if (minh != null && minh.GetComponent(inventoryType) == null)
        {
            Undo.AddComponent(minh, inventoryType);
            changedCount++;
        }

        changedCount += SetupCharacterHouseFurniture(pickupType);

        Transform container = GetOrCreatePickupContainer();
        if (container.childCount == 0)
        {
            Vector3 basePosition = minh != null ? minh.transform.position : Vector3.zero;
            foreach ((string itemName, Color color, Vector3 offset) in SampleItems)
            {
                CreatePickupItem(container, pickupType, itemName, color, basePosition + offset);
                changedCount++;
            }
        }

        if (changedCount > 0)
        {
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            EditorSceneManager.SaveOpenScenes();
            Debug.Log($"Simple inventory setup complete. Changes: {changedCount}.");
        }

        SessionState.SetBool(SessionKey, true);
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
        if (!scene.IsValid() || !scene.isLoaded)
        {
            return;
        }

        Apply();
    }

    private static Transform GetOrCreatePickupContainer()
    {
        GameObject containerObject = GameObject.Find(PickupContainerName);
        if (containerObject == null)
        {
            containerObject = new GameObject(PickupContainerName);
            Undo.RegisterCreatedObjectUndo(containerObject, "Create inventory pickup container");
        }

        return containerObject.transform;
    }

    private static void CreatePickupItem(Transform container, Type pickupType, string itemName, Color color, Vector3 position)
    {
        GameObject itemObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        Undo.RegisterCreatedObjectUndo(itemObject, "Create inventory pickup item");
        itemObject.name = "Pickup " + itemName;
        itemObject.transform.SetParent(container, true);
        itemObject.transform.position = position;
        itemObject.transform.localScale = Vector3.one * 0.35f;

        Renderer renderer = itemObject.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"))
            {
                color = color
            };
        }

        Component pickup = Undo.AddComponent(itemObject, pickupType);
        MethodInfo configure = pickupType.GetMethod("Configure", BindingFlags.Instance | BindingFlags.Public);
        configure?.Invoke(pickup, new object[] { itemName, color, null });
    }

    private static int SetupCharacterHouseFurniture(Type pickupType)
    {
        GameObject characterHouse = GameObject.Find("CharacterHouse (1)");
        if (characterHouse == null)
        {
            return 0;
        }

        int count = 0;
        foreach (Transform child in characterHouse.GetComponentsInChildren<Transform>(true))
        {
            if (!IsFurniturePickup(child.name) || child.GetComponent(pickupType) != null)
            {
                continue;
            }

            Component pickup = Undo.AddComponent(child.gameObject, pickupType);
            MethodInfo configure = pickupType.GetMethod("Configure", BindingFlags.Instance | BindingFlags.Public);
            configure?.Invoke(pickup, new object[] { GetDisplayName(child.name), GetFurnitureColor(child.name), null });
            count++;
        }

        return count;
    }

    private static bool IsFurniturePickup(string objectName)
    {
        return objectName.StartsWith("Chair", StringComparison.OrdinalIgnoreCase) ||
               objectName.StartsWith("Table", StringComparison.OrdinalIgnoreCase) ||
               objectName.StartsWith("Bed", StringComparison.OrdinalIgnoreCase) ||
               objectName.StartsWith("Closet", StringComparison.OrdinalIgnoreCase);
    }

    private static string GetDisplayName(string objectName)
    {
        if (objectName.StartsWith("Chair", StringComparison.OrdinalIgnoreCase))
        {
            return "Chair";
        }

        if (objectName.StartsWith("Table", StringComparison.OrdinalIgnoreCase))
        {
            return "Table";
        }

        if (objectName.StartsWith("Bed", StringComparison.OrdinalIgnoreCase))
        {
            return "Bed";
        }

        if (objectName.StartsWith("Closet", StringComparison.OrdinalIgnoreCase))
        {
            return "Closet";
        }

        return objectName;
    }

    private static Color GetFurnitureColor(string objectName)
    {
        if (objectName.StartsWith("Bed", StringComparison.OrdinalIgnoreCase))
        {
            return new Color(0.18f, 0.35f, 0.95f);
        }

        if (objectName.StartsWith("Closet", StringComparison.OrdinalIgnoreCase))
        {
            return new Color(0.35f, 0.28f, 0.22f);
        }

        if (objectName.StartsWith("Table", StringComparison.OrdinalIgnoreCase))
        {
            return new Color(0.45f, 0.28f, 0.16f);
        }

        return new Color(0.5f, 0.35f, 0.22f);
    }
}
