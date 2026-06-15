using System;
using System.Linq;
using UnityEngine;

public sealed class CharacterHouseCollisionGuard : MonoBehaviour
{
    private static readonly string[] BlockingNameParts =
    {
        "floor",
        "wall",
        "door"
    };

    private void Awake()
    {
        EnsureBlockingColliders();
    }

    private void EnsureBlockingColliders()
    {
        Renderer[] renderers = GetComponentsInChildren<Renderer>(true)
            .Where(renderer => ShouldBlock(renderer.gameObject))
            .ToArray();

        foreach (Renderer renderer in renderers)
        {
            GameObject target = renderer.gameObject;
            if (target.GetComponent<Collider>() != null)
            {
                continue;
            }

            BoxCollider collider = target.AddComponent<BoxCollider>();
            Bounds bounds = renderer.bounds;
            collider.center = target.transform.InverseTransformPoint(bounds.center);
            collider.size = AbsVector(target.transform.InverseTransformVector(bounds.size));
        }

        Physics.SyncTransforms();
    }

    private static bool ShouldBlock(GameObject gameObject)
    {
        string path = GetHierarchyPath(gameObject.transform).ToLowerInvariant();
        return BlockingNameParts.Any(path.Contains);
    }

    private static Vector3 AbsVector(Vector3 value)
    {
        return new Vector3(
            Mathf.Abs(value.x),
            Mathf.Abs(value.y),
            Mathf.Abs(value.z));
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
