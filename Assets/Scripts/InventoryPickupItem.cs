using UnityEngine;

public sealed class InventoryPickupItem : MonoBehaviour
{
    [SerializeField] private string itemName = "Item";
    [SerializeField] private Color itemColor = Color.white;
    [SerializeField] private GameObject dropPrefab;

    public string ItemName
    {
        get
        {
            if (string.IsNullOrWhiteSpace(itemName) || itemName == "Item")
            {
                return gameObject.name;
            }
            return itemName;
        }
    }
    public Color ItemColor => itemColor;
    public GameObject DropPrefab => dropPrefab;

    public void Configure(string displayName, Color color, GameObject prefab)
    {
        itemName = string.IsNullOrWhiteSpace(displayName) ? gameObject.name : displayName;
        itemColor = color;
        dropPrefab = prefab;
        EnsurePickupPhysics(false);
    }

    public void EnableDroppedPhysics()
    {
        EnsurePickupPhysics(true);
    }

    private void Reset()
    {
        itemName = gameObject.name;
        EnsurePickupPhysics(false);
    }

    private void Awake()
    {
        EnsurePickupPhysics(false);
    }

    public SimpleInventory.InventoryItem CreateInventoryItem()
    {
        GameObject sceneTemplate = null;
        if (dropPrefab == null)
        {
            sceneTemplate = Instantiate(gameObject);
            sceneTemplate.name = ItemName + " Template";
            sceneTemplate.SetActive(false);
            DontDestroyOnLoad(sceneTemplate);
        }

        return new SimpleInventory.InventoryItem(ItemName, itemColor, dropPrefab, sceneTemplate, transform.rotation);
    }

    private void EnsurePickupPhysics(bool useDynamicPhysics)
    {
        SetStaticRecursively(transform, false);

        Collider collider = GetComponent<Collider>();
        if (collider == null)
        {
            BoxCollider boxCollider = gameObject.AddComponent<BoxCollider>();
            Renderer renderer = GetComponentInChildren<Renderer>();
            if (renderer != null)
            {
                Bounds bounds = renderer.bounds;
                boxCollider.center = transform.InverseTransformPoint(bounds.center);
                Vector3 size = AbsVector(transform.InverseTransformVector(bounds.size));
                
                // Nếu là cá, đảm bảo kích thước collider tối thiểu đủ lớn (0.8m) để người chơi dễ ngắm và nhặt lại
                if (GetComponentInChildren<FishSwim>(true) != null || ItemName.ToLower().Contains("fish") || ItemName.ToLower().Contains("ca"))
                {
                    size.x = Mathf.Max(size.x, 0.8f);
                    size.y = Mathf.Max(size.y, 0.8f);
                    size.z = Mathf.Max(size.z, 0.8f);
                }
                boxCollider.size = size;
            }
            collider = boxCollider;
        }

        // Nếu là cá, chuyển collider thành Trigger để không cản trở di chuyển vật lý của cá và dễ nhặt
        if (GetComponentInChildren<FishSwim>(true) != null || ItemName.ToLower().Contains("fish") || ItemName.ToLower().Contains("ca"))
        {
            collider.isTrigger = true;
        }

        Rigidbody rigidbody = GetComponent<Rigidbody>();
        if (rigidbody == null)
        {
            rigidbody = gameObject.AddComponent<Rigidbody>();
        }

        rigidbody.mass = 1f;
        
        // Nếu là cá, luôn giữ kinematic và không dùng gravity để cá tự do bơi lội
        if (GetComponentInChildren<FishSwim>(true) != null || ItemName.ToLower().Contains("fish") || ItemName.ToLower().Contains("ca"))
        {
            rigidbody.useGravity = false;
            rigidbody.isKinematic = true;
        }
        else if (IsFurnitureName(ItemName) || IsBucketName(ItemName))
        {
            rigidbody.useGravity = false;
            rigidbody.isKinematic = true;
        }
        else
        {
            rigidbody.useGravity = useDynamicPhysics;
            rigidbody.isKinematic = !useDynamicPhysics;
        }
        
        rigidbody.collisionDetectionMode = CollisionDetectionMode.Continuous;
        rigidbody.interpolation = RigidbodyInterpolation.Interpolate;

        if (useDynamicPhysics && !rigidbody.isKinematic)
        {
            rigidbody.WakeUp();
        }
    }

    private static Vector3 AbsVector(Vector3 value)
    {
        return new Vector3(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));
    }

    private static void SetStaticRecursively(Transform root, bool isStatic)
    {
        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            child.gameObject.isStatic = isStatic;
        }
    }

    private static bool IsFurnitureName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;
        string lower = name.ToLower().Trim();
        return lower.StartsWith("chair") ||
               lower.StartsWith("table") ||
               lower.StartsWith("bed") ||
               lower.StartsWith("closet");
    }

    private static bool IsBucketName(string name)
    {
        if (string.IsNullOrWhiteSpace(name)) return false;
        string lower = name.ToLower().Trim();
        return lower.Contains("bucket") || lower.Contains("xô") || lower.Contains("xo");
    }
}
