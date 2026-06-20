using UnityEngine;
using UnityEngine.InputSystem;

public sealed class WaterSource : MonoBehaviour
{
    [Header("Interaction Settings")]
    [SerializeField] private float interactionDistance = 2.5f;
    [SerializeField] private string fillPrompt = "Press F to fill bucket with water";

    [Header("Bucket Prefabs Reference")]
    [SerializeField] private GameObject waterBucketPrefab;
    [SerializeField] private GameObject emptyBucketPrefab;

    private Transform player;
    private SimpleInventory playerInventory;
    private bool isPlayerNear;
    private GUIStyle promptStyle;

    private void Start()
    {
        FindPlayer();
#if UNITY_EDITOR
        AutoFindPrefabs();
#endif
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
        if (PauseMenuManager.GameIsPaused) return;

        if (player == null || playerInventory == null)
        {
            FindPlayer();
            return;
        }

        // Measure distance between player and water source
        float distance = Vector3.Distance(transform.position, player.position);
        isPlayerNear = distance <= interactionDistance;

        if (isPlayerNear)
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.fKey.wasPressedThisFrame)
            {
                // Case 1: Player is holding an empty bucket
                SimpleInventory.InventoryItem selectedItem = playerInventory.GetSelectedItem();
                if (selectedItem != null && SimpleInventory.IsEmptyBucket(selectedItem.Name))
                {
                    // Convert empty bucket to water bucket
                    SimpleInventory.InventoryItem waterBucket = new SimpleInventory.InventoryItem(
                        "WaterBucket",
                        Color.white,
                        waterBucketPrefab != null ? waterBucketPrefab : selectedItem.DropPrefab,
                        selectedItem.SceneTemplate, // Preserve empty bucket template
                        selectedItem.BaseRotation
                    );

                    playerInventory.ReplaceSelectedItem(waterBucket);
                    Debug.Log("Successfully filled hand-held bucket!");
                    return;
                }

                // Case 2: There is a dropped empty bucket near the water source
                InventoryPickupItem nearestDroppedBucket = FindNearestDroppedEmptyBucket(transform.position, interactionDistance);
                if (nearestDroppedBucket != null)
                {
                    Vector3 originalPosition = nearestDroppedBucket.transform.position;
                    Quaternion originalRotation = nearestDroppedBucket.transform.rotation;

                    if (waterBucketPrefab != null)
                    {
                        Destroy(nearestDroppedBucket.gameObject);

                        GameObject newWaterBucket = Instantiate(waterBucketPrefab, originalPosition, originalRotation);
                        newWaterBucket.name = "WaterBucket";

                        InventoryPickupItem newPickup = newWaterBucket.GetComponent<InventoryPickupItem>();
                        if (newPickup == null)
                        {
                            newPickup = newWaterBucket.AddComponent<InventoryPickupItem>();
                        }
                        newPickup.Configure("WaterBucket", Color.white, waterBucketPrefab);

                        // Đảm bảo bật mesh nước của xô nước mới tạo ra
                        foreach (Transform child in newWaterBucket.GetComponentsInChildren<Transform>(true))
                        {
                            if (child.gameObject.name.ToLower().Contains("water"))
                            {
                                child.gameObject.SetActive(true);
                            }
                        }
                    }
                    else
                    {
                        // Fallback if prefab is missing
                        nearestDroppedBucket.Configure(
                            "WaterBucket",
                            new Color(0.2f, 0.6f, 1.0f, 1.0f),
                            nearestDroppedBucket.DropPrefab
                        );

                        Renderer[] renderers = nearestDroppedBucket.GetComponentsInChildren<Renderer>(true);
                        foreach (Renderer r in renderers)
                        {
                            if (r.material.HasProperty("_BaseColor"))
                                r.material.SetColor("_BaseColor", new Color(0.2f, 0.6f, 1.0f, 1.0f));
                            else
                                r.material.color = new Color(0.2f, 0.6f, 1.0f, 1.0f);
                        }

                        // Đảm bảo bật mesh nước ở dạng fallback
                        foreach (Transform child in nearestDroppedBucket.GetComponentsInChildren<Transform>(true))
                        {
                            if (child.gameObject.name.ToLower().Contains("water"))
                            {
                                child.gameObject.SetActive(true);
                            }
                        }
                    }

                    Debug.Log("Successfully filled dropped bucket!");
                }
            }
        }
    }

    private void OnGUI()
    {
        if (PauseMenuManager.GameIsPaused || !isPlayerNear || playerInventory == null) return;

        // Check if player is holding an empty bucket
        SimpleInventory.InventoryItem selectedItem = playerInventory.GetSelectedItem();
        bool hasHeldEmptyBucket = selectedItem != null && SimpleInventory.IsEmptyBucket(selectedItem.Name);

        // Find empty bucket dropped nearby if not holding one
        InventoryPickupItem nearestDroppedBucket = null;
        if (!hasHeldEmptyBucket)
        {
            nearestDroppedBucket = FindNearestDroppedEmptyBucket(transform.position, interactionDistance);
        }

        // Show fill prompt if player has or is near an empty bucket
        if (hasHeldEmptyBucket || nearestDroppedBucket != null)
        {
            EnsureStyles();
            float width = 320f;
            float height = 42f;
            Rect rect = new((Screen.width - width) * 0.5f, Screen.height - 160f, width, height);
            GUI.Box(rect, fillPrompt, promptStyle);
        }
    }

    private InventoryPickupItem FindNearestDroppedEmptyBucket(Vector3 center, float maxDistance)
    {
        InventoryPickupItem nearest = null;
        float bestDist = maxDistance;

        foreach (InventoryPickupItem item in FindObjectsByType<InventoryPickupItem>(FindObjectsSortMode.None))
        {
            if (SimpleInventory.IsEmptyBucket(item.ItemName))
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
        if (promptStyle == null)
        {
            promptStyle = new GUIStyle(GUI.skin.box)
            {
                alignment = TextAnchor.MiddleCenter,
                fontSize = 16,
                fontStyle = FontStyle.Bold
            };
            promptStyle.normal.textColor = Color.white;
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        AutoFindPrefabs();
    }

    private void AutoFindPrefabs()
    {
        if (waterBucketPrefab == null)
        {
            string[] guids = UnityEditor.AssetDatabase.FindAssets("WaterBucket t:Prefab");
            if (guids.Length > 0)
            {
                string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
                waterBucketPrefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>(path);
            }
        }
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
    }
#endif
}
