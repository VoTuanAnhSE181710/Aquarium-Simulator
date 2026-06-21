using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;

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
    private bool showDebugHUD = true;
    private InventoryPickupItem selectedDecorItem;
    private Dictionary<Renderer, Color> originalDecorColors = new Dictionary<Renderer, Color>();


    private void Update()
    {
        if (PauseMenuManager.GameIsPaused)
        {
            return;
        }

        if (AquariumDecorationMode.IsTransitioning)
        {
            return;
        }

        CacheNearestPickup();

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return;
        }

        if (keyboard.qKey.wasPressedThisFrame)
        {
            showDebugHUD = !showDebugHUD;
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
            if (AquariumDecorationMode.IsDecorationMode)
            {
                // Đang ở trong chế độ Decor -> Bấm G để THOÁT
                if (AquariumDecorationMode.Instance != null)
                {
                    AquariumDecorationMode.Instance.ExitDecorationMode();
                    DeselectDecorItem();
                }
            }
            else
            {
                // Đang ở ngoài -> Kiểm tra xem có đủ gần bể để VÀO không
                if (AquariumDecorationMode.Instance != null && AquariumDecorationMode.Instance.CanEnterMode())
                {
                    AquariumDecorationMode.Instance.EnterDecorationMode();
                }
                else
                {
                    // Nếu đứng xa bể cá -> Thực hiện thả đồ (Drop) như bình thường
                    TryDropSelected();
                }
            }
        }

        if (AquariumDecorationMode.IsDecorationMode)
        {
            HandleDecorationMode();
            return;
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

            // Sử dụng tâm của Collider (hoặc tâm của Mesh Renderer) thay vì pivot của transform 
            // để tránh lỗi model có pivot bị đặt lệch quá xa so với thực tế của lưới mesh
            Collider col = pickup.GetComponent<Collider>();
            Vector3 targetPos = col != null ? col.bounds.center : pickup.transform.position;
            float distance = (targetPos - transform.position).sqrMagnitude;

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

        // Kiểm tra xem vật phẩm thả có phải là cá hay không bằng component hoặc tên hiển thị
        bool isFish = false;
        if (item.DropPrefab != null && item.DropPrefab.GetComponentInChildren<FishSwim>(true) != null)
        {
            isFish = true;
        }
        else if (item.SceneTemplate != null && item.SceneTemplate.GetComponentInChildren<FishSwim>(true) != null)
        {
            isFish = true;
        }
        else if (item.Name.StartsWith("Cá") || item.Name.StartsWith("Ca") || item.Name.ToLower().Contains("fish"))
        {
            isFish = true;
        }

        if (isFish)
        {
            Collider nearestWaterCol = null;
            float closestDistance = float.MaxValue;

            // Tìm tất cả Collider trong scene, chọn cái GẦN NHẤT
            foreach (Collider col in FindObjectsByType<Collider>(FindObjectsSortMode.None))
            {
                if (col.isTrigger)
                {
                    string name = col.gameObject.name.ToLower();
                    string parentName = col.transform.parent != null ? col.transform.parent.gameObject.name.ToLower() : "";
                    
                    if (name.Contains("water") || name.Contains("tank") || name.Contains("inside") || name.Contains("aquarium") ||
                        parentName.Contains("water") || parentName.Contains("tank") || parentName.Contains("aquarium"))
                    {
                        float dist = Vector3.Distance(transform.position, col.bounds.ClosestPoint(transform.position));
                        if (dist < closestDistance)
                        {
                            closestDistance = dist;
                            nearestWaterCol = col;
                        }
                    }
                }
            }

            if (nearestWaterCol != null)
            {
                if (closestDistance > 4.5f) // Giới hạn khoảng cách thả cá (tăng lên 4.5 mét cho người chơi dễ thả)
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

        Vector3 floorPos = GetFloorPosition();
        Quaternion dropRotation = GetDropRotation(item);
        GameObject dropped = item.DropPrefab != null
            ? Instantiate(item.DropPrefab, floorPos, dropRotation)
            : item.SceneTemplate != null
                ? Instantiate(item.SceneTemplate, floorPos, dropRotation)
            : GameObject.CreatePrimitive(PrimitiveType.Cube);

        // Adjust position so the bottom of the object aligns perfectly with the floor
        float bottomOffset = GetBottomOffset(dropped);
        dropped.transform.position = floorPos + Vector3.up * bottomOffset;

        // Nếu là cá, gán Water Collider của bể nước gần vị trí thả nhất trước khi Active để Start() chạy đúng
        if (isFish)
        {
            FishSwim swim = dropped.GetComponentInChildren<FishSwim>(true);
            if (swim != null)
            {
                Collider nearestWaterCol = null;
                float closestDistance = float.MaxValue;
                foreach (Collider col in FindObjectsByType<Collider>(FindObjectsSortMode.None))
                {
                    if (col.isTrigger)
                    {
                        string name = col.gameObject.name.ToLower();
                        string parentName = col.transform.parent != null ? col.transform.parent.gameObject.name.ToLower() : "";
                        
                        if (name.Contains("water") || name.Contains("tank") || name.Contains("inside") || name.Contains("aquarium") ||
                            parentName.Contains("water") || parentName.Contains("tank") || parentName.Contains("aquarium"))
                        {
                            float dist = Vector3.Distance(floorPos, col.bounds.ClosestPoint(floorPos));
                            if (dist < closestDistance)
                            {
                                closestDistance = dist;
                                nearestWaterCol = col;
                            }
                        }
                    }
                }

                if (nearestWaterCol != null)
                {
                    swim.waterCollider = nearestWaterCol;
                }
            }
        }

        dropped.SetActive(true);

        if (item.DropPrefab == null)
        {
            if (item.SceneTemplate == null)
            {
                dropped.transform.localScale = Vector3.one * 0.35f;
            }

            Renderer renderer = dropped.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.material.color = item.Color;
            }
        }

        dropped.name = item.Name;

        // Bật/tắt mesh nước của xô khi thả xuống sàn tùy theo trạng thái chứa nước hay rỗng
        bool isDroppedWater = IsWaterBucket(item.Name);
        if (IsEmptyBucket(item.Name) || isDroppedWater)
        {
            foreach (Transform child in dropped.GetComponentsInChildren<Transform>(true))
            {
                if (child.gameObject.name.ToLower().Contains("water"))
                {
                    child.gameObject.SetActive(isDroppedWater);
                }
            }
        }

        InventoryPickupItem pickup = dropped.GetComponent<InventoryPickupItem>();
        if (pickup == null)
        {
            pickup = dropped.AddComponent<InventoryPickupItem>();
        }

        pickup.Configure(item.Name, item.Color, item.DropPrefab);

        // Nếu là cá, không kích hoạt trọng lực/kinematic rơi tự do để tránh làm cá rơi xuống đáy và lỗi bơi
        if (isFish)
        {
            Rigidbody rb = dropped.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true;
                rb.useGravity = false;
            }
        }
        else
        {
            pickup.EnableDroppedPhysics();
        }
        slots[selectedSlot] = null;
        RefreshCarriedItemVisual();
        return pickup != null;
    }

    private void HandleDecorationMode()
    {
        Keyboard keyboard = Keyboard.current;
        Mouse mouse = Mouse.current;
        InventoryItem item = slots[selectedSlot];

        // ==========================================
        // TRƯỜNG HỢP 1: TAY ĐANG TRỐNG -> CHỌN / THU HỒI / DI CHUYỂN
        // ==========================================
        if (item == null)
        {
            if (placementPreview != null) placementPreview.SetActive(false);

            // 1. Click chuột trái để CHỌN vật thể trong bể
            if (mouse != null && mouse.leftButton.wasPressedThisFrame)
            {
                Ray ray = Camera.main.ScreenPointToRay(mouse.position.ReadValue());

                // Bắn tia xuyên qua tất cả mọi thứ để tìm đồ vật
                if (Physics.Raycast(ray, out RaycastHit hit, 15f))
                {
                    InventoryPickupItem clickedItem = hit.collider.GetComponentInParent<InventoryPickupItem>();
                    if (clickedItem != null)
                    {
                        Collider tankVolume = AquariumDecorationMode.Instance.GetPlacementVolume();
                        // Chỉ cho phép chọn đồ vật đang nằm TRONG bể
                        if (tankVolume != null && tankVolume.bounds.Contains(clickedItem.transform.position))
                        {
                            SelectDecorItem(clickedItem);
                            return;
                        }
                    }
                }
                // Nếu click ra chỗ trống không trúng đồ vật nào -> Bỏ chọn
                DeselectDecorItem();
            }

            // 2. Xử lý phím X và F cho vật đang được chọn
            if (selectedDecorItem != null && keyboard != null)
            {
                // PHÍM X: THU HỒI
                if (keyboard.xKey.wasPressedThisFrame)
                {
                    int emptySlot = GetFirstEmptySlot();
                    if (emptySlot >= 0)
                    {
                        slots[emptySlot] = selectedDecorItem.CreateInventoryItem();
                        Destroy(selectedDecorItem.gameObject);
                        DeselectDecorItem();
                        RefreshCarriedItemVisual();
                    }
                    else
                    {
                        ShowWarning("Túi đồ đã đầy, không thể thu hồi!");
                    }
                }
                // PHÍM F: DI CHUYỂN (Chuyển thành Hologram để đặt lại)
                else if (keyboard.fKey.wasPressedThisFrame)
                {
                    // Lấy đồ nhét ngay vào ô trống đang chọn
                    slots[selectedSlot] = selectedDecorItem.CreateInventoryItem();
                    Destroy(selectedDecorItem.gameObject);
                    DeselectDecorItem();
                    RefreshCarriedItemVisual();
                    // Ngay lập tức Hologram sẽ hiện ra vì slot[selectedSlot] giờ đã có đồ!
                }
            }
            return;
        }

        // ==========================================
        // TRƯỜNG HỢP 2: TAY ĐANG CẦM ĐỒ -> ĐẶT ĐỒ (Logic cũ)
        // ==========================================
        DeselectDecorItem(); // Đang cầm đồ trên tay thì không được phép highlight cái khác

        if (Camera.main == null || mouse == null) return;

        if (placementPreview == null || previewedItem != item)
        {
            RebuildPlacementPreview(item);
        }
        placementPreview.SetActive(true);

        UpdatePlacementRotation();

        Vector2 mousePos = mouse.position.ReadValue();
        Ray decorRay = Camera.main.ScreenPointToRay(mousePos);
        LayerMask mask = AquariumDecorationMode.Instance.GetPlacementMask();

        bool isHit = Physics.Raycast(decorRay, out RaycastHit placeHit, 10f, mask, QueryTriggerInteraction.Ignore);
        bool isValidPlacement = isHit && !IsWaterBucket(item.Name) && !IsEmptyBucket(item.Name);

        bool isFish = false;
        if (item.DropPrefab != null && item.DropPrefab.GetComponentInChildren<FishSwim>(true) != null) isFish = true;
        else if (item.SceneTemplate != null && item.SceneTemplate.GetComponentInChildren<FishSwim>(true) != null) isFish = true;
        else if (item.Name.ToLower().Contains("fish") || item.Name.ToLower().Contains("cá") || item.Name.ToLower().Contains("ca")) isFish = true;

        if (isHit)
        {
            placementPreview.transform.position = placeHit.point;
            Quaternion yawRotation = Quaternion.AngleAxis(placementYaw, Vector3.up);

            if (!isFish)
            {
                Quaternion alignToSurface = Quaternion.FromToRotation(Vector3.up, placeHit.normal);
                placementPreview.transform.rotation = alignToSurface * yawRotation * item.BaseRotation;
            }
            else
            {
                placementPreview.transform.rotation = yawRotation * item.BaseRotation;
            }

            float bottomOffset = GetBottomOffset(placementPreview);
            placementPreview.transform.position = placeHit.point + placeHit.normal * bottomOffset;

            Collider tankVolume = AquariumDecorationMode.Instance.GetPlacementVolume();
            if (tankVolume != null && isValidPlacement)
            {
                Bounds itemBounds = GetTotalBounds(placementPreview);
                Bounds tankBounds = tankVolume.bounds;
                tankBounds.Expand(0.02f);

                bool isInsideX = itemBounds.min.x >= tankBounds.min.x && itemBounds.max.x <= tankBounds.max.x;
                bool isInsideZ = itemBounds.min.z >= tankBounds.min.z && itemBounds.max.z <= tankBounds.max.z;
                bool isInsideY = itemBounds.max.y <= tankBounds.max.y + 0.1f;

                if (!isInsideX || !isInsideZ || !isInsideY)
                {
                    isValidPlacement = false;
                }
            }

            if (isValidPlacement)
            {
                SetPreviewColor(item.Color, 0.45f);
                if (mouse.leftButton.wasPressedThisFrame)
                {
                    PlaceItemAtDecor(item, isFish);
                }
            }
            else
            {
                SetPreviewColor(Color.red, 0.6f);
                if (mouse.leftButton.wasPressedThisFrame)
                {
                    ShowWarning("Vật phẩm bị chạm vào kính hoặc không hợp lệ!");
                }
            }
        }
        else
        {
            SetPreviewColor(Color.red, 0.6f);
            if (mouse.leftButton.wasPressedThisFrame)
            {
                ShowWarning("Hãy chỉ chuột vào mặt đáy bên trong bể cá!");
            }
        }
    }

    private void PlaceItemAtDecor(InventoryItem item, bool isFish)
    {
        // Lấy chính xác vị trí và góc quay của Hologram hiện tại
        Vector3 placePosition = placementPreview.transform.position;
        Quaternion placeRotation = placementPreview.transform.rotation;

        GameObject placedObj = item.DropPrefab != null
            ? Instantiate(item.DropPrefab, placePosition, placeRotation)
            : item.SceneTemplate != null
                ? Instantiate(item.SceneTemplate, placePosition, placeRotation)
                : GameObject.CreatePrimitive(PrimitiveType.Cube);

        placedObj.SetActive(true);
        placedObj.name = item.Name;

        if (isFish)
        {
            FishSwim swim = placedObj.GetComponentInChildren<FishSwim>(true);
            if (swim != null)
            {
                Collider tankCol = AquariumDecorationMode.Instance.GetComponentInChildren<Collider>();
                if (tankCol != null) swim.waterCollider = tankCol;
            }
            Rigidbody rb = placedObj.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true;
                rb.useGravity = false;
            }
        }
        else
        {
            Rigidbody rb = placedObj.GetComponent<Rigidbody>();
            if (rb != null)
            {
                rb.isKinematic = true;
                rb.useGravity = false;
            }
        }

        InventoryPickupItem pickup = placedObj.GetComponent<InventoryPickupItem>();
        if (pickup == null) pickup = placedObj.AddComponent<InventoryPickupItem>();
        pickup.Configure(item.Name, item.Color, item.DropPrefab);

        // Xóa đồ khỏi Hotbar
        slots[selectedSlot] = null;
        RefreshCarriedItemVisual();

        // Ẩn Hologram đi vì ô chứa đồ đã trống
        if (placementPreview != null) placementPreview.SetActive(false);
    }

    private void SetPreviewColor(Color c, float alpha)
    {
        if (previewMaterial != null)
        {
            Color color = c;
            color.a = alpha;
            if (previewMaterial.HasProperty("_BaseColor"))
            {
                previewMaterial.SetColor("_BaseColor", color);
            }
            else
            {
                previewMaterial.color = color;
            }
        }
    }

    private void SelectDecorItem(InventoryPickupItem item)
    {
        DeselectDecorItem(); // Bỏ chọn cái cũ (nếu có)
        if (item == null) return;

        selectedDecorItem = item;

        // Quét tất cả Mesh và đổi màu sang Xanh lơ (Cyan)
        foreach (Renderer r in item.GetComponentsInChildren<Renderer>(true))
        {
            if (r.material.HasProperty("_BaseColor"))
            {
                originalDecorColors[r] = r.material.GetColor("_BaseColor");
                r.material.SetColor("_BaseColor", Color.cyan);
            }
            else if (r.material.HasProperty("_Color"))
            {
                originalDecorColors[r] = r.material.color;
                r.material.color = Color.cyan;
            }
        }
    }

    private void DeselectDecorItem()
    {
        if (selectedDecorItem != null)
        {
            // Trả lại toàn bộ màu gốc đã lưu
            foreach (var kvp in originalDecorColors)
            {
                if (kvp.Key != null)
                {
                    if (kvp.Key.material.HasProperty("_BaseColor")) kvp.Key.material.SetColor("_BaseColor", kvp.Value);
                    else if (kvp.Key.material.HasProperty("_Color")) kvp.Key.material.color = kvp.Value;
                }
            }
            originalDecorColors.Clear();
            selectedDecorItem = null;
        }
    }

    private Vector3 GetFloorPosition()
    {
        Vector3 forward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
        if (forward.sqrMagnitude < 0.001f)
        {
            forward = Vector3.forward;
        }

        Vector3 position = transform.position + forward * dropDistance;
        Vector3 rayStart = position;
        rayStart.y = transform.position.y + 1.2f;

        if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, 5f, ~0, QueryTriggerInteraction.Ignore))
        {
            return hit.point;
        }

        return position;
    }

    private float GetBottomOffset(GameObject obj)
    {
        if (obj == null) return 0f;

        // Ép Unity cập nhật lại transform trước khi đo đạc
        Physics.SyncTransforms();

        // Đo bằng Renderer (độ chính xác hiển thị cao nhất)
        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length > 0)
        {
            Bounds bounds = renderers[0].bounds;
            foreach (Renderer r in renderers)
            {
                bounds.Encapsulate(r.bounds);
            }
            // Khoảng cách từ tâm vật thể đến điểm thấp nhất của nó
            return obj.transform.position.y - bounds.min.y;
        }

        // Nếu vật không có hình ảnh (chỉ có Collider)
        Collider[] colliders = obj.GetComponentsInChildren<Collider>(true);
        if (colliders.Length > 0)
        {
            Bounds bounds = colliders[0].bounds;
            foreach (Collider c in colliders)
            {
                bounds.Encapsulate(c.bounds);
            }
            return obj.transform.position.y - bounds.min.y;
        }

        return 0f;
    }

    private Bounds GetTotalBounds(GameObject obj)
    {
        // Ép Unity cập nhật Transform ngay lập tức để đo chính xác
        Physics.SyncTransforms();

        Bounds bounds = new Bounds(obj.transform.position, Vector3.zero);
        bool hasBounds = false;

        // Ưu tiên đo bằng hình ảnh (Mesh)
        Renderer[] renderers = obj.GetComponentsInChildren<Renderer>(true);
        if (renderers.Length > 0)
        {
            bounds = renderers[0].bounds;
            foreach (Renderer r in renderers)
            {
                bounds.Encapsulate(r.bounds);
            }
            hasBounds = true;
        }

        // Nếu không có Mesh, đo bằng Collider
        if (!hasBounds)
        {
            Collider[] colliders = obj.GetComponentsInChildren<Collider>(true);
            if (colliders.Length > 0)
            {
                bounds = colliders[0].bounds;
                foreach (Collider c in colliders)
                {
                    bounds.Encapsulate(c.bounds);
                }
            }
        }

        return bounds;
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

        // Ưu tiên instantiate mô hình 3D thực tế của vật phẩm nếu có
        if (visualizedItem.DropPrefab != null)
        {
            heldItemVisual = Instantiate(visualizedItem.DropPrefab);
        }
        else if (visualizedItem.SceneTemplate != null)
        {
            heldItemVisual = Instantiate(visualizedItem.SceneTemplate);
        }
        else
        {
            heldItemVisual = GameObject.CreatePrimitive(PrimitiveType.Cube);
        }

        heldItemVisual.name = "Held " + visualizedItem.Name;
        heldItemVisual.SetActive(true);

        // Vô hiệu hóa tất cả Colliders và Rigidbody trên heldItemVisual để tránh lỗi vật lý trong tay
        foreach (Collider col in heldItemVisual.GetComponentsInChildren<Collider>(true))
        {
            col.enabled = false; // Vô hiệu hóa ngay lập tức!
            Destroy(col);
        }
        foreach (Rigidbody rb in heldItemVisual.GetComponentsInChildren<Rigidbody>(true))
        {
            rb.isKinematic = true; // Thiết lập kinematic và tắt trọng lực ngay lập tức!
            rb.useGravity = false;
            rb.detectCollisions = false;
            Destroy(rb);
        }
        
        // Vô hiệu hóa và hủy bỏ tất cả các script logic trên đối tượng cầm trên tay để tránh chạy code thừa gây lỗi
        foreach (MonoBehaviour mono in heldItemVisual.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (mono != null)
            {
                Destroy(mono);
            }
        }

        // Vô hiệu hóa Animator trên đối tượng cầm tay để tránh xung đột hoạt ảnh xương
        foreach (Animator animator in heldItemVisual.GetComponentsInChildren<Animator>(true))
        {
            animator.enabled = false;
        }

        Transform anchor = GetHeldItemAnchor();

        // Xác định world scale mục tiêu cho vật thể cầm trên tay
        Vector3 targetScale = Vector3.one;
        GameObject source = visualizedItem.DropPrefab != null ? visualizedItem.DropPrefab : visualizedItem.SceneTemplate;
        if (source != null)
        {
            targetScale = source.transform.localScale;
        }

        if (IsEmptyBucket(visualizedItem.Name) || IsWaterBucket(visualizedItem.Name))
        {
            targetScale = Vector3.one * 0.4f;
        }
        else if (source == null)
        {
            targetScale = Vector3.one * 0.24f;
        }

        // Thiết lập kích thước, vị trí và góc quay thế giới trước, sau đó dùng SetParent(anchor, true)
        // để Unity tự động tính toán bù trừ tỷ lệ scale xương hoàn hảo
        heldItemVisual.transform.localScale = targetScale;
        heldItemVisual.transform.position = anchor.position + anchor.rotation * heldItemLocalPosition;
        heldItemVisual.transform.rotation = anchor.rotation * Quaternion.Euler(heldItemLocalEulerAngles);
        
        heldItemVisual.transform.SetParent(anchor, true);

        // Bật/tắt mesh nước của xô cầm tay tùy theo trạng thái chứa nước hay rỗng
        bool isWater = IsWaterBucket(visualizedItem.Name);
        if (IsEmptyBucket(visualizedItem.Name) || isWater)
        {
            foreach (Transform child in heldItemVisual.GetComponentsInChildren<Transform>(true))
            {
                if (child.gameObject.name.ToLower().Contains("water"))
                {
                    child.gameObject.SetActive(isWater);
                }
            }
        }

        // Chỉnh màu sắc nếu là xô nước hoặc cube
        Renderer[] renderers = heldItemVisual.GetComponentsInChildren<Renderer>(true);
        foreach (Renderer r in renderers)
        {
            if (IsWaterBucket(visualizedItem.Name))
            {
                if (r.material.HasProperty("_BaseColor"))
                {
                    r.material.SetColor("_BaseColor", visualizedItem.Color);
                }
                else
                {
                    r.material.color = visualizedItem.Color;
                }
            }
            else if (source == null)
            {
                if (r.material.HasProperty("_BaseColor"))
                {
                    r.material.SetColor("_BaseColor", visualizedItem.Color);
                }
                else
                {
                    r.material.color = visualizedItem.Color;
                }
            }
        }
    }

    private Transform GetHeldItemAnchor()
    {
        if (heldItemAnchor != null)
        {
            return heldItemAnchor;
        }

        // Tìm xương bàn tay phải không phân biệt chữ hoa thường
        foreach (Transform child in GetComponentsInChildren<Transform>(true))
        {
            string nameLower = child.name.ToLower();
            if (nameLower.Contains("righthand") || nameLower.EndsWith("righthand") || nameLower.Contains("right_hand"))
            {
                heldItemAnchor = child;
                return heldItemAnchor;
            }
        }

        // Fallback tìm cẳng tay phải
        foreach (Transform child in GetComponentsInChildren<Transform>(true))
        {
            string nameLower = child.name.ToLower();
            if (nameLower.Contains("rightforearm") || nameLower.Contains("right_forearm") || nameLower.Contains("rightarm") || nameLower.Contains("right_arm"))
            {
                heldItemAnchor = child;
                return heldItemAnchor;
            }
        }

        heldItemAnchor = transform;
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
        Vector3 floorPos = GetFloorPosition();
        placementPreview.transform.rotation = GetDropRotation(item);
        placementPreview.transform.position = floorPos;

        float bottomOffset = GetBottomOffset(placementPreview);
        placementPreview.transform.position = floorPos + Vector3.up * bottomOffset;

        if (item.SceneTemplate == null && item.DropPrefab == null)
        {
            placementPreview.transform.localScale = Vector3.one * 0.36f;
        }
        else
        {
            GameObject source = item.DropPrefab != null ? item.DropPrefab : item.SceneTemplate;
            if (source != null)
            {
                placementPreview.transform.localScale = source.transform.localScale;
            }
        }

        SetPreviewColor(item.Color, 0.45f);
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
            collider.enabled = false; // Vô hiệu hóa ngay lập tức!
            Destroy(collider);
        }

        foreach (Rigidbody rigidbody in placementPreview.GetComponentsInChildren<Rigidbody>(true))
        {
            rigidbody.isKinematic = true; // Thiết lập kinematic và tắt trọng lực ngay lập tức!
            rigidbody.useGravity = false;
            rigidbody.detectCollisions = false;
            Destroy(rigidbody);
        }

        foreach (InventoryPickupItem pickup in placementPreview.GetComponentsInChildren<InventoryPickupItem>(true))
        {
            Destroy(pickup);
        }

        // Vô hiệu hóa và hủy bỏ tất cả các script logic trên đối tượng xem trước để tránh gây lỗi
        foreach (MonoBehaviour mono in placementPreview.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (mono != null)
            {
                Destroy(mono);
            }
        }

        // Vô hiệu hóa Animator trên đối tượng xem trước để đối tượng đứng yên khi ướm thử
        foreach (Animator animator in placementPreview.GetComponentsInChildren<Animator>(true))
        {
            animator.enabled = false;
        }

        // Tạo chất liệu bán trong suốt đồng nhất chuẩn URP Lit cho Hologram preview
        previewMaterial = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        previewMaterial.SetFloat("_Surface", 1f); // Transparent
        previewMaterial.SetFloat("_Blend", 0f); // Alpha Blend
        previewMaterial.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
        previewMaterial.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        previewMaterial.SetFloat("_ZWrite", 0f);
        previewMaterial.DisableKeyword("_ALPHATEST_ON");
        previewMaterial.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        previewMaterial.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        
        Color holoColor = item.Color;
        holoColor.a = 0.45f; // Độ mờ 45%

        if (previewMaterial.HasProperty("_BaseColor"))
        {
            previewMaterial.SetColor("_BaseColor", holoColor);
        }
        else
        {
            previewMaterial.color = holoColor;
        }

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
            // Thay thế tất cả chất liệu bằng chất liệu hologram để hiển thị đẹp và chuẩn URP
            Material[] mats = new Material[renderer.sharedMaterials.Length];
            for (int i = 0; i < mats.Length; i++)
            {
                mats[i] = previewMaterial;
            }
            renderer.materials = mats;
        }

        if (source == null)
        {
            placementPreview.transform.localScale = Vector3.one * 0.36f;
        }
        else
        {
            placementPreview.transform.localScale = source.transform.localScale;
        }

        // Bật/tắt mesh nước của xô khi hiển thị hologram ướm thử vị trí đặt
        bool isPreviewWater = IsWaterBucket(item.Name);
        if (IsEmptyBucket(item.Name) || isPreviewWater)
        {
            foreach (Transform child in placementPreview.GetComponentsInChildren<Transform>(true))
            {
                if (child.gameObject.name.ToLower().Contains("water"))
                {
                    child.gameObject.SetActive(isPreviewWater);
                }
            }
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
        if (showDebugHUD)
        {
            DrawDebugHUD();
        }

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

        if (AquariumDecorationMode.IsDecorationMode)
        {
            const float w = 620f;
            const float h = 36f;
            Rect decorRect = new((Screen.width - w) * 0.5f, 40f, w, h);
            GUIStyle decorStyle = new GUIStyle(slotStyle);

            string instructions = "";

            if (slots[selectedSlot] != null)
            {
                decorStyle.normal.textColor = Color.cyan;
                instructions = "DECORATION MODE: Chuột trái để đặt đồ | Ấn G để thoát";
            }
            else if (selectedDecorItem != null)
            {
                decorStyle.normal.textColor = Color.yellow;
                instructions = $"ĐÃ CHỌN: {selectedDecorItem.ItemName} | [X] Thu hồi | [F] Di chuyển | Chuột trái để hủy";
            }
            else
            {
                decorStyle.normal.textColor = Color.white;
                instructions = "Tay đang trống. Click chuột trái vào đồ vật trong bể để chọn | Ấn G để thoát";
            }

            GUI.Box(decorRect, instructions, decorStyle);
        }
    }

    private void DrawDebugHUD()
    {
        const float w = 320f;
        const float h = 135f;
        Rect rect = new(16f, Screen.height - h - 16f, w, h);
        
        string nearestName = nearestPickup != null ? nearestPickup.ItemName : "None";
        
        Vector3 targetPos = nearestPickup != null ? (nearestPickup.GetComponent<Collider>() != null ? nearestPickup.GetComponent<Collider>().bounds.center : nearestPickup.transform.position) : transform.position;
        float dist = nearestPickup != null ? Vector3.Distance(transform.position, targetPos) : 0f;
        string distText = nearestPickup != null ? $"{dist:F2}m" : "N/A";
        
        string debugContent = $"<b>[ INVENTORY DEBUG HUD ]</b>\n" +
                              $"- Slot 1: {(slots[0] == null ? "Empty" : slots[0].Name)}\n" +
                              $"- Slot 2: {(slots[1] == null ? "Empty" : slots[1].Name)}\n" +
                              $"- Slot 3: {(slots[2] == null ? "Empty" : slots[2].Name)}\n" +
                              $"- Selected: Slot {selectedSlot + 1}\n" +
                              $"- Nearest Item: {nearestName} ({distText})\n" +
                              $"<i>(E: Pick up, G: Drop, Q: Toggle HUD)</i>";
                              
        GUIStyle debugStyle = new GUIStyle(GUI.skin.box)
        {
            alignment = TextAnchor.UpperLeft,
            fontSize = 12,
            padding = new RectOffset(10, 10, 8, 8),
            wordWrap = true
        };
        debugStyle.normal.textColor = Color.yellow;
        
        GUI.Box(rect, debugContent, debugStyle);
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
            
            // Highlight màu vàng cho chữ của ô đang được chọn
            GUIStyle currentLabelStyle = new GUIStyle(labelStyle);
            if (index == selectedSlot)
            {
                currentLabelStyle.normal.textColor = Color.yellow;
            }
            
            // Vẽ số ô kèm dấu ngoặc vuông để hiển thị rõ ô đang chọn, ví dụ: [1] thay vì 1
            string slotNumText = index == selectedSlot ? $"[{index + 1}]" : (index + 1).ToString();
            GUI.Label(labelRect, $"{slotNumText}\n{text}", currentLabelStyle);
        }
    }

    private void DrawPrompt()
    {
        if (AquariumDecorationMode.IsDecorationMode) return;

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

    public InventoryItem GetSelectedItem()
    {
        return slots[selectedSlot];
    }

    public void ReplaceSelectedItem(InventoryItem newItem)
    {
        slots[selectedSlot] = newItem;
        RefreshCarriedItemVisual();
    }

    private void ShowWarning(string msg)
    {
        warningMessage = msg;
        warningTimer = Time.time + 3.0f;
    }

    public static bool IsWaterBucket(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;
        string lower = name.ToLower().Trim();
        bool isBucket = lower.Contains("xô") || lower.Contains("xo") || lower.Contains("bucket");
        bool isWater = lower.Contains("nước") || lower.Contains("nuoc") || lower.Contains("water") || lower.Contains("wet");
        return isBucket && isWater;
    }

    public static bool IsEmptyBucket(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;
        string lower = name.ToLower().Trim();
        bool isBucket = lower.Contains("xô") || lower.Contains("xo") || lower.Contains("bucket");
        return isBucket && !IsWaterBucket(name);
    }
}

