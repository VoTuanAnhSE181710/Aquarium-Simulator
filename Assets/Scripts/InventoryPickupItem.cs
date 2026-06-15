using UnityEngine;

public sealed class InventoryPickupItem : MonoBehaviour
{
    [SerializeField] private string itemName = "Item";
    [SerializeField] private Color itemColor = Color.white;
    [SerializeField] private GameObject dropPrefab;

    public string ItemName => string.IsNullOrWhiteSpace(itemName) ? gameObject.name : itemName;
    public Color ItemColor => itemColor;
    public GameObject DropPrefab => dropPrefab;

    public void Configure(string displayName, Color color, GameObject prefab)
    {
        itemName = string.IsNullOrWhiteSpace(displayName) ? gameObject.name : displayName;
        itemColor = color;
        dropPrefab = prefab;
        EnsurePickupPhysics();
    }

    private void Reset()
    {
        itemName = gameObject.name;
        EnsurePickupPhysics();
    }

    private void Awake()
    {
        EnsurePickupPhysics();
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

    private void EnsurePickupPhysics()
    {
        gameObject.isStatic = false;

        if (GetComponent<Collider>() == null)
        {
            BoxCollider collider = gameObject.AddComponent<BoxCollider>();
            Renderer renderer = GetComponentInChildren<Renderer>();
            if (renderer != null)
            {
                Bounds bounds = renderer.bounds;
                collider.center = transform.InverseTransformPoint(bounds.center);
                collider.size = AbsVector(transform.InverseTransformVector(bounds.size));
            }
        }

        Rigidbody rigidbody = GetComponent<Rigidbody>();
        if (rigidbody == null)
        {
            rigidbody = gameObject.AddComponent<Rigidbody>();
        }

        rigidbody.mass = 1f;
        rigidbody.isKinematic = true;
        rigidbody.collisionDetectionMode = CollisionDetectionMode.Continuous;
    }

    private static Vector3 AbsVector(Vector3 value)
    {
        return new Vector3(Mathf.Abs(value.x), Mathf.Abs(value.y), Mathf.Abs(value.z));
    }
}
