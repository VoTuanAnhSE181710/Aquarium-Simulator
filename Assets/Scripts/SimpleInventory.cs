using UnityEngine;
using UnityEngine.InputSystem;

public sealed class SimpleInventory : MonoBehaviour
{
    [System.Serializable]
    public sealed class InventoryItem
    {
        public string Name;
        public Color Color;
        public GameObject DropPrefab;
        public GameObject SceneTemplate;
        public Quaternion BaseRotation;

        public InventoryItem(
            string name,
            Color color,
            GameObject dropPrefab,
            GameObject sceneTemplate = null,
            Quaternion? baseRotation = null)
        {
            Name = string.IsNullOrWhiteSpace(name) ? "Item" : name;
            Color = color;
            DropPrefab = dropPrefab;
            SceneTemplate = sceneTemplate;
            BaseRotation = baseRotation ?? Quaternion.identity;
        }
    }

    private const int SlotCount = 3;

    [SerializeField] private float pickupDistance = 2.2f;
    [SerializeField] private float dropDistance = 1.35f;
    [SerializeField] private float dropHeight = 0.35f;
    [SerializeField] private float placementRotationStep = 15f;
    [SerializeField] private Vector3 heldItemLocalPosition = new(0.08f, 0.02f, 0.04f);
    [SerializeField] private Vector3 heldItemLocalEulerAngles = new(0f, 0f, 0f);
    [SerializeField] private LayerMask pickupMask = ~0;
    [SerializeField] private string pickupPrompt = "Press E to pick up";
    [SerializeField] private string dropPrompt = "Press G to drop";

    private readonly InventoryItem[] slots = new InventoryItem[SlotCount];
    private InventoryPickupItem nearestPickup;
    private Transform heldItemAnchor;
    private GameObject heldItemVisual;
    private GameObject placementPreview;
    private Material previewMaterial;
    private InventoryItem previewedItem;
    private InventoryItem visualizedItem;
    private GUIStyle slotStyle;
    private GUIStyle selectedSlotStyle;
    private GUIStyle labelStyle;
    private int selectedSlot;
    private float placementYaw;
    private string warningMessage = "";
    private float warningTimer = 0f;

    private void Update()
    {
        if (PauseMenuManager.GameIsPaused)
        {
            return;
        }

        CacheNearestPickup();

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return;
        }

        if (keyboard.digit1Key.wasPressedThisFrame)
        {
            selectedSlot = 0;
            RefreshCarriedItemVisual();
        }
        else if (keyboard.digit2Key.wasPressedThisFrame)
        {
            selectedSlot = 1;
            RefreshCarriedItemVisual();
        }
        else if (keyboard.digit3Key.wasPressedThisFrame)
        {
            selectedSlot = 2;
            RefreshCarriedItemVisual();
        }

        if (keyboard.eKey.wasPressedThisFrame)
        {
            TryPickupNearest();
        }

        if (keyboard.gKey.wasPressedThisFrame)
        {
            TryDropSelected();
        }

        UpdatePlacementRotation();
        UpdatePlacementPreview();
    }

    private void CacheNearestPickup()
    {
        nearestPickup = null;
        Collider[] colliders = Physics.OverlapSphere(
            transform.position,
            pickupDistance,
            pickupMask,
            QueryTriggerInteraction.Collide);

        float bestDistance = float.MaxValue;
        foreach (Collider collider in colliders)
        {
            InventoryPickupItem pickup = collider.GetComponentInParent<InventoryPickupItem>();
            if (pickup == null)
            {
                pickup = TryCreateFurniturePickup(collider.transform);
            }

            if (pickup == null || !pickup.gameObject.activeInHierarchy)
            {
                continue;
            }

            float distance = (pickup.transform.position - transform.position).sqrMagnitude;
            if (distance >= bestDistance)
            {
                continue;
            }

            bestDistance = distance;
            nearestPickup = pickup;
        }
    }

    private static InventoryPickupItem TryCreateFurniturePickup(Transform hitTransform)
    {
        Transform furnitureRoot = FindFurnitureRoot(hitTransform);
        if (furnitureRoot == null)
        {
            return null;
        }

        InventoryPickupItem pickup = furnitureRoot.gameObject.AddComponent<InventoryPickupItem>();
        pickup.Configure(GetFurnitureDisplayName(furnitureRoot.name), GetFurnitureColor(furnitureRoot.name), null);
        return pickup;
    }

    private static Transform FindFurnitureRoot(Transform start)
    {
        Transform current = start;
        Transform bestMatch = null;

        while (current != null)
        {
            if (IsFurnitureName(current.name))
            {
                bestMatch = current;
            }

            if (current.name == "CharacterHouse (1)")
            {
                break;
            }

            current = current.parent;
        }

        return bestMatch;
    }

    private static bool IsFurnitureName(string objectName)
    {
        return objectName.StartsWith("Chair", System.StringComparison.OrdinalIgnoreCase) ||
               objectName.StartsWith("Table", System.StringComparison.OrdinalIgnoreCase) ||
               objectName.StartsWith("Bed", System.StringComparison.OrdinalIgnoreCase) ||
               objectName.StartsWith("Closet", System.StringComparison.OrdinalIgnoreCase);
    }

    private static string GetFurnitureDisplayName(string objectName)
    {
        if (objectName.StartsWith("Chair", System.StringComparison.OrdinalIgnoreCase))
        {
            return "Chair";
        }

        if (objectName.StartsWith("Table", System.StringComparison.OrdinalIgnoreCase))
        {
            return "Table";
        }

        if (objectName.StartsWith("Bed", System.StringComparison.OrdinalIgnoreCase))
        {
            return "Bed";
        }

        if (objectName.StartsWith("Closet", System.StringComparison.OrdinalIgnoreCase))
        {
            return "Closet";
        }

        return objectName;
    }

    private static Color GetFurnitureColor(string objectName)
    {
        if (objectName.StartsWith("Bed", System.StringComparison.OrdinalIgnoreCase))
        {
            return new Color(0.18f, 0.35f, 0.95f);
        }

        if (objectName.StartsWith("Closet", System.StringComparison.OrdinalIgnoreCase))
        {
            return new Color(0.35f, 0.28f, 0.22f);
        }

        if (objectName.StartsWith("Table", System.StringComparison.OrdinalIgnoreCase))
        {
            return new Color(0.45f, 0.28f, 0.16f);
        }

        return new Color(0.5f, 0.35f, 0.22f);
    }

    private bool TryPickupNearest()
    {
        if (nearestPickup == null)
        {
            return false;
        }

        int emptySlot = GetFirstEmptySlot();
        if (emptySlot < 0)
        {
            return false;
        }

        slots[emptySlot] = nearestPickup.CreateInventoryItem();
        selectedSlot = emptySlot;
        Destroy(nearestPickup.gameObject);
        nearestPickup = null;
        RefreshCarriedItemVisual();
        return true;
    }

    private bool TryDropSelected()
    {
        InventoryItem item = slots[selectedSlot];
        if (item == null)
        {
            return false;
        }

        // Kiểm tra khoảng cách tới bể nước nếu vật phẩm thả là cá
        if (item.Name.StartsWith("Cá") || item.Name.StartsWith("Ca") || item.Name.ToLower().Contains("fish"))
        {
            Collider waterCol = null;
            // Tìm tất cả Collider trong scene, kiểm tra xem tên object hoặc cha của nó có chứa chữ liên quan bể cá không
            foreach (Collider col in FindObjectsByType<Collider>(FindObjectsSortMode.None))
            {
                if (col.isTrigger)
                {
                    string name = col.gameObject.name.ToLower();
                    string parentName = col.transform.parent != null ? col.transform.parent.gameObject.name.ToLower() : "";
                    
                    if (name.Contains("water") || name.Contains("tank") || name.Contains("inside") || name.Contains("aquarium") ||
                        parentName.Contains("water") || parentName.Contains("tank") || parentName.Contains("aquarium"))
                    {
                        waterCol = col;
                        break;
                    }
                }
            }

            if (waterCol != null)
            {
                float distToWater = Vector3.Distance(transform.position, waterCol.bounds.ClosestPoint(transform.position));
                if (distToWater > 3.8f) // Giới hạn khoảng cách thả cá trong khoảng 3.8 mét
                {
                    ShowWarning("Hãy lại gần bể cá để thả cá vào!");
                    return false;
                }
            }
            else
            {
                ShowWarning("Không tìm thấy bể cá nào trong phòng!");
                return false;
            }
        }

        Vector3 dropPosition = GetDropPosition();
        Quaternion dropRotation = GetDropRotation(item);
        GameObject dropped = item.DropPrefab != null
            ? Instantiate(item.DropPrefab, dropPosition, dropRotation)
            : item.SceneTemplate != null
                ? Instantiate(item.SceneTemplate, dropPosition, dropRotation)
            : GameObject.CreatePrimitive(PrimitiveType.Cube);

        dropped.SetActive(true);

        if (item.DropPrefab == null)
        {
            if (item.SceneTemplate == null)
            {
                dropped.transform.position = dropPosition;
                dropped.transform.rotation = dropRotation;
                dropped.transform.localScale = Vector3.one * 0.35f;
            }

            Renderer renderer = dropped.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = item.Color;
            }
        }

        dropped.name = item.Name;
        InventoryPickupItem pickup = dropped.GetComponent<InventoryPickupItem>();
        if (pickup == null)
        {
            pickup = dropped.AddComponent<InventoryPickupItem>();
        }

        pickup.Configure(item.Name, item.Color, item.DropPrefab);
        pickup.EnableDroppedPhysics();
        slots[selectedSlot] = null;
        RefreshCarriedItemVisual();
        return pickup != null;
    }

    private Vector3 GetDropPosition()
    {
        Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        if (forward.sqrMagnitude < 0.001f)
        {
            forward = Vector3.forward;
        }

        Vector3 position = transform.position + forward * dropDistance + Vector3.up * dropHeight;
        if (Physics.Raycast(position + Vector3.up * 2f, Vector3.down, out RaycastHit hit, 8f, ~0, QueryTriggerInteraction.Ignore))
        {
            position.y = hit.point.y + dropHeight;
        }

        return position;
    }

    private Quaternion GetDropRotation(InventoryItem item)
    {
        Quaternion yawRotation = Quaternion.AngleAxis(placementYaw, Vector3.up);
        return yawRotation * item.BaseRotation;
    }

    private void UpdatePlacementRotation()
    {
        if (slots[selectedSlot] == null || Mouse.current == null)
        {
            return;
        }

        float scrollY = Mouse.current.scroll.ReadValue().y;
        if (Mathf.Abs(scrollY) < 0.01f)
        {
            return;
        }

        placementYaw = Mathf.Repeat(
            placementYaw + Mathf.Sign(scrollY) * placementRotationStep,
            360f);
    }

    private void RefreshCarriedItemVisual()
    {
        if (heldItemVisual != null)
        {
            Destroy(heldItemVisual);
        }

        visualizedItem = slots[selectedSlot];
        if (visualizedItem == null)
        {
            return;
        }

        heldItemVisual = GameObject.CreatePrimitive(PrimitiveType.Cube);
        heldItemVisual.name = "Held " + visualizedItem.Name;
        heldItemVisual.transform.localScale = Vector3.one * 0.24f;

        Collider collider = heldItemVisual.GetComponent<Collider>();
        if (collider != null)
        {
            Destroy(collider);
        }

        Renderer renderer = heldItemVisual.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.material.color = visualizedItem.Color;
        }

        Transform anchor = GetHeldItemAnchor();
        heldItemVisual.transform.SetParent(anchor, false);
        heldItemVisual.transform.localPosition = heldItemLocalPosition;
        heldItemVisual.transform.localRotation = Quaternion.Euler(heldItemLocalEulerAngles);
    }

    private Transform GetHeldItemAnchor()
    {
        if (heldItemAnchor != null)
        {
            return heldItemAnchor;
        }

        heldItemAnchor =
            FindChild("mixamorig:RightHand") ??
            FindChild("RightHand") ??
            FindChild("mixamorig:RightForeArm") ??
            FindChild("RightForeArm") ??
            transform;
        return heldItemAnchor;
    }

    private Transform FindChild(string childName)
    {
        foreach (Transform child in GetComponentsInChildren<Transform>(true))
        {
            if (child.name == childName)
            {
                return child;
            }
        }

        return null;
    }

    private void UpdatePlacementPreview()
    {
        InventoryItem item = slots[selectedSlot];
        if (item == null)
        {
            if (placementPreview != null)
            {
                placementPreview.SetActive(false);
            }

            previewedItem = null;
            return;
        }

        if (placementPreview == null || previewedItem != item)
        {
            RebuildPlacementPreview(item);
        }

        placementPreview.SetActive(true);
        placementPreview.transform.position = GetDropPosition();
        placementPreview.transform.rotation = GetDropRotation(item);

        if (item.SceneTemplate == null && item.DropPrefab == null)
        {
            placementPreview.transform.localScale = Vector3.one * 0.36f;
        }

        if (previewMaterial != null)
        {
            Color color = item.Color;
            color.a = 0.35f;
            previewMaterial.color = color;
        }
    }

    private void RebuildPlacementPreview(InventoryItem item)
    {
        if (placementPreview != null)
        {
            Destroy(placementPreview);
        }

        GameObject source = item.DropPrefab != null ? item.DropPrefab : item.SceneTemplate;
        placementPreview = source != null
            ? Instantiate(source)
            : GameObject.CreatePrimitive(PrimitiveType.Cube);
        placementPreview.name = "Inventory Placement Preview";
        placementPreview.SetActive(true);

        foreach (Collider collider in placementPreview.GetComponentsInChildren<Collider>(true))
        {
            Destroy(collider);
        }

        foreach (Rigidbody rigidbody in placementPreview.GetComponentsInChildren<Rigidbody>(true))
        {
            Destroy(rigidbody);
        }

        foreach (InventoryPickupItem pickup in placementPreview.GetComponentsInChildren<InventoryPickupItem>(true))
        {
            Destroy(pickup);
        }

        previewMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        previewMaterial.SetFloat("_Surface", 1f);
        previewMaterial.SetFloat("_ZWrite", 0f);
        previewMaterial.renderQueue = 3000;
        previewMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");

        Renderer[] renderers = placementPreview.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length == 0)
        {
            Renderer renderer = placementPreview.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderers = new[] { renderer };
            }
        }

        foreach (Renderer renderer in renderers)
        {
            renderer.sharedMaterial = previewMaterial;
        }

        if (source == null)
        {
            placementPreview.transform.localScale = Vector3.one * 0.36f;
        }

        previewedItem = item;
    }

    private int GetFirstEmptySlot()
    {
        for (int index = 0; index < slots.Length; index++)
        {
            if (slots[index] == null)
            {
                return index;
            }
        }

        return -1;
    }

    private void OnGUI()
    {
        if (PauseMenuManager.GameIsPaused)
        {
            return;
        }

        EnsureStyles();
        DrawHotbar();
        DrawPrompt();

        // Vẽ cảnh báo nếu có
        if (!string.IsNullOrEmpty(warningMessage) && Time.time < warningTimer)
        {
            const float w = 350f;
            const float h = 42f;
            Rect rect = new((Screen.width - w) * 0.5f, Screen.height - 180f, w, h);
            GUIStyle warningStyle = new GUIStyle(slotStyle);
            warningStyle.normal.textColor = Color.red;
            GUI.Box(rect, warningMessage, warningStyle);
        }
    }

    private void DrawHotbar()
    {
        const float slotSize = 82f;
        const float gap = 10f;
        float totalWidth = SlotCount * slotSize + (SlotCount - 1) * gap;
        float startX = (Screen.width - totalWidth) * 0.5f;
        float y = Screen.height - slotSize - 24f;

        for (int index = 0; index < SlotCount; index++)
        {
            Rect rect = new(startX + index * (slotSize + gap), y, slotSize, slotSize);
            GUI.Box(rect, GUIContent.none, index == selectedSlot ? selectedSlotStyle : slotStyle);

            InventoryItem item = slots[index];
            string text = item == null ? "Empty" : item.Name;
            Rect labelRect = new(rect.x + 6f, rect.y + 14f, rect.width - 12f, rect.height - 20f);
            GUI.Label(labelRect, $"{index + 1}\n{text}", labelStyle);
        }
    }

    private void DrawPrompt()
    {
        string prompt = nearestPickup != null
            ? $"{pickupPrompt}: {nearestPickup.ItemName}"
            : slots[selectedSlot] != null
                ? dropPrompt
                : null;

        if (string.IsNullOrEmpty(prompt))
        {
            return;
        }

        const float width = 300f;
        const float height = 36f;
        Rect rect = new((Screen.width - width) * 0.5f, Screen.height - 138f, width, height);
        GUI.Box(rect, prompt, slotStyle);
    }

    private void EnsureStyles()
    {
        if (slotStyle != null)
        {
            return;
        }

        slotStyle = new GUIStyle(GUI.skin.box)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 14,
            fontStyle = FontStyle.Bold,
            normal = { textColor = Color.white },
            padding = new RectOffset(6, 6, 6, 6)
        };

        selectedSlotStyle = new GUIStyle(slotStyle);
        selectedSlotStyle.normal.textColor = Color.yellow;

        labelStyle = new GUIStyle(GUI.skin.label)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 13,
            fontStyle = FontStyle.Bold,
            wordWrap = true,
            normal = { textColor = Color.white }
        };
    }

    public bool IsFull()
    {
        return GetFirstEmptySlot() < 0;
    }

    public bool AddItem(string itemName, Color itemColor, GameObject dropPrefab, GameObject sceneTemplate = null)
    {
        int emptySlot = GetFirstEmptySlot();
        if (emptySlot < 0)
        {
            return false;
        }
        slots[emptySlot] = new InventoryItem(itemName, itemColor, dropPrefab, sceneTemplate);
        if (emptySlot == selectedSlot)
        {
            RefreshCarriedItemVisual();
        }
        return true;
    }

    private void ShowWarning(string msg)
    {
        warningMessage = msg;
        warningTimer = Time.time + 3.0f;
    }
}

