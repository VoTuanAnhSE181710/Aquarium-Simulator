using System.IO;
using UnityEditor;
using UnityEngine;

public static class AquariumPlantAssetBuilder
{
    private const string AssetFolder = "Assets/Aquarium/HomeTankPlants";
    [MenuItem("Tools/Tạo Cây Thủy Sinh (Build Plants)")]

    public static void Build()
    {
        Directory.CreateDirectory(AssetFolder);

        Material leafDark = CreateMaterial("MAT_AquariumPlant_DarkGreen", new Color(0.08f, 0.45f, 0.18f));
        Material leafLight = CreateMaterial("MAT_AquariumPlant_LightGreen", new Color(0.18f, 0.72f, 0.32f));
        Material leafRed = CreateMaterial("MAT_AquariumPlant_RedGreen", new Color(0.44f, 0.18f, 0.22f));
        Material stem = CreateMaterial("MAT_AquariumPlant_Stem", new Color(0.07f, 0.28f, 0.12f));
        Material stone = CreateMaterial("MAT_AquariumPlant_StoneBase", new Color(0.38f, 0.34f, 0.30f));

        SavePrefab(BuildGrassCluster(leafLight, stem, stone), "AquaticPlant_GrassCluster");
        SavePrefab(BuildBroadLeaf(leafDark, stem, stone), "AquaticPlant_BroadLeaf");
        SavePrefab(BuildRedStem(leafRed, stem, stone), "AquaticPlant_RedStem");

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static Material CreateMaterial(string name, Color color)
    {
        string path = $"{AssetFolder}/{name}.mat";
        Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (existing != null)
        {
            return existing;
        }

        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        Material material = new Material(shader)
        {
            name = name
        };

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }
        else
        {
            material.color = color;
        }

        AssetDatabase.CreateAsset(material, path);
        return material;
    }

    private static GameObject BuildGrassCluster(Material leaf, Material stem, Material stone)
    {
        GameObject root = CreateRoot("AquaticPlant_GrassCluster", 1.5f);
        CreateBase(root.transform, stone);

        for (int i = 0; i < 12; i++)
        {
            float angle = i * 30f;
            float height = 0.34f + (i % 4) * 0.055f;
            Vector3 localPos = Quaternion.Euler(0f, angle, 0f) * new Vector3(0.035f + (i % 3) * 0.01f, height * 0.5f, 0f);
            Transform blade = CreateCube("Blade", root.transform, leaf).transform;
            blade.localPosition = localPos;
            blade.localRotation = Quaternion.Euler(14f + (i % 5) * 4f, angle, 8f);
            blade.localScale = new Vector3(0.018f, height, 0.01f);
        }

        return root;
    }

    private static GameObject BuildBroadLeaf(Material leaf, Material stem, Material stone)
    {
        GameObject root = CreateRoot("AquaticPlant_BroadLeaf", 2f);
        CreateBase(root.transform, stone);

        for (int i = 0; i < 7; i++)
        {
            float angle = i * 51.4f;
            float height = 0.22f + (i % 3) * 0.045f;
            Transform stalk = CreateCube("Stem", root.transform, stem).transform;
            stalk.localPosition = Quaternion.Euler(0f, angle, 0f) * new Vector3(0.025f, height * 0.5f, 0f);
            stalk.localRotation = Quaternion.Euler(0f, angle, 6f);
            stalk.localScale = new Vector3(0.012f, height, 0.012f);

            Transform leafPart = CreateCube("Leaf", root.transform, leaf).transform;
            leafPart.localPosition = Quaternion.Euler(0f, angle, 0f) * new Vector3(0.055f, height + 0.045f, 0f);
            leafPart.localRotation = Quaternion.Euler(20f, angle, 28f);
            leafPart.localScale = new Vector3(0.07f, 0.018f, 0.035f);
        }

        return root;
    }

    private static GameObject BuildRedStem(Material leaf, Material stem, Material stone)
    {
        GameObject root = CreateRoot("AquaticPlant_RedStem", 2.5f);
        CreateBase(root.transform, stone);

        for (int i = 0; i < 5; i++)
        {
            float angle = i * 72f;
            float height = 0.38f + (i % 2) * 0.08f;
            Transform stalk = CreateCube("TallStem", root.transform, stem).transform;
            stalk.localPosition = Quaternion.Euler(0f, angle, 0f) * new Vector3(0.035f, height * 0.5f, 0f);
            stalk.localRotation = Quaternion.Euler(4f, angle, 4f);
            stalk.localScale = new Vector3(0.013f, height, 0.013f);

            for (int leafIndex = 0; leafIndex < 3; leafIndex++)
            {
                Transform leafPart = CreateCube("RedLeaf", root.transform, leaf).transform;
                leafPart.localPosition = Quaternion.Euler(0f, angle + leafIndex * 28f, 0f)
                    * new Vector3(0.055f, height * (0.45f + leafIndex * 0.18f), 0f);
                leafPart.localRotation = Quaternion.Euler(18f, angle + leafIndex * 28f, leafIndex % 2 == 0 ? 28f : -28f);
                leafPart.localScale = new Vector3(0.055f, 0.016f, 0.026f);
            }
        }

        return root;
    }

    private static GameObject CreateRoot(string name, float oxygenBonus)
    {
        GameObject root = new GameObject(name);
        root.layer = LayerMask.NameToLayer("Default");
        AquariumOxygenProvider provider = root.AddComponent<AquariumOxygenProvider>();
        SerializedObject serializedProvider = new SerializedObject(provider);
        serializedProvider.FindProperty("oxygenCapacityBonus").floatValue = oxygenBonus;
        serializedProvider.FindProperty("providerName").stringValue = name;
        serializedProvider.ApplyModifiedPropertiesWithoutUndo();

        InventoryPickupItem pickup = root.AddComponent<InventoryPickupItem>();
        pickup.Configure(name, new Color(0.18f, 0.72f, 0.32f), null);

        // --- THÊM ĐOẠN NÀY: Khống chế BoxCollider do InventoryPickupItem tự sinh ra ---
        BoxCollider autoCol = root.GetComponent<BoxCollider>();
        if (autoCol != null)
        {
            autoCol.size = new Vector3(0.01f, 0.01f, 0.01f); // Thu nhỏ bé tí hon
            autoCol.enabled = false; // Tắt luôn để không cản đường tia chuột
        }
        // -----------------------------------------------------------------------------

        return root;
    }

    private static void CreateBase(Transform parent, Material material)
    {
        Transform basePart = CreateCube("StoneBase", parent, material).transform;
        basePart.localPosition = new Vector3(0f, 0.018f, 0f);
        basePart.localScale = new Vector3(0.18f, 0.035f, 0.13f);
    }

    private static GameObject CreateCube(string name, Transform parent, Material material)
    {
        GameObject part = GameObject.CreatePrimitive(PrimitiveType.Cube);
        part.name = name;
        part.transform.SetParent(parent, false);

        // ĐÃ GIỮ LẠI COLLIDER CHO TỪNG CÀNH/LÁ BẰNG CÁCH XÓA DÒNG Object.DestroyImmediate

        Renderer renderer = part.GetComponent<Renderer>();
        renderer.sharedMaterial = material;
        return part;
    }

    private static void SavePrefab(GameObject root, string name)
    {
        string path = $"{AssetFolder}/{name}.prefab";
        PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
    }
}