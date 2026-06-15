using UnityEngine;
using UnityEngine.InputSystem;

public sealed class DoorInteraction : MonoBehaviour
{
    [SerializeField] private Transform player;
    [SerializeField] private Transform doorLeaf;
    [SerializeField] private Collider interactionCollider;
    [SerializeField] private float interactionDistance = 1f;
    [SerializeField] private float openAngle = 95f;
    [SerializeField] private float rotationSpeed = 180f;
    [SerializeField] private bool startOpen;

    private Transform rotationPivot;
    private Quaternion closedRotation;
    private Quaternion openRotation;
    private bool isOpen;

    private void Awake()
    {
        FindDoorLeaf();
        FindInteractionCollider();
        rotationPivot = CreateEdgePivot();
        closedRotation = rotationPivot.localRotation;
        openRotation = closedRotation * Quaternion.Euler(0f, openAngle, 0f);
        isOpen = startOpen;
        rotationPivot.localRotation = isOpen ? openRotation : closedRotation;

        FindPlayer();
    }

    private void Update()
    {
        if (PauseMenuManager.GameIsPaused)
        {
            return;
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || !keyboard.fKey.wasPressedThisFrame)
        {
            AnimateDoor();
            return;
        }

        if (player == null)
        {
            FindPlayer();
        }

        if (player != null && IsWithinInteractionDistance(player.position) && IsClosestDoorToPlayer())
        {
            isOpen = !isOpen;
        }

        AnimateDoor();
    }

    private void AnimateDoor()
    {
        Quaternion targetRotation = isOpen ? openRotation : closedRotation;
        rotationPivot.localRotation = Quaternion.RotateTowards(
            rotationPivot.localRotation,
            targetRotation,
            rotationSpeed * Time.deltaTime);
    }

    private bool IsClosestDoorToPlayer()
    {
        float ownDistance = GetSqrDistance(player.position);
        DoorInteraction[] doors = FindObjectsByType<DoorInteraction>(FindObjectsSortMode.None);

        foreach (DoorInteraction door in doors)
        {
            if (door != this &&
                door.player != null &&
                door.IsWithinInteractionDistance(player.position) &&
                door.GetSqrDistance(player.position) < ownDistance)
            {
                return false;
            }
        }

        return true;
    }

    private bool IsWithinInteractionDistance(Vector3 playerPosition)
    {
        return GetSqrDistance(playerPosition) <= interactionDistance * interactionDistance;
    }

    private float GetSqrDistance(Vector3 playerPosition)
    {
        Vector3 nearestPoint = interactionCollider != null
            ? interactionCollider.bounds.ClosestPoint(playerPosition)
            : transform.position;
        return (nearestPoint - playerPosition).sqrMagnitude;
    }

    private void FindDoorLeaf()
    {
        if (doorLeaf == null)
        {
            doorLeaf = transform.Find("Door");
        }
    }

    private void FindInteractionCollider()
    {
        if (interactionCollider == null && doorLeaf != null)
        {
            interactionCollider = doorLeaf.GetComponent<Collider>();
        }

        if (interactionCollider == null)
        {
            interactionCollider = GetComponentInChildren<BoxCollider>();
        }
    }

    private Transform CreateEdgePivot()
    {
        BoxCollider doorCollider = doorLeaf != null
            ? doorLeaf.GetComponent<BoxCollider>()
            : null;
        if (doorLeaf == null || doorCollider == null)
        {
            return transform;
        }

        Vector3 widthAxis = doorCollider.size.x > doorCollider.size.z
            ? Vector3.right
            : Vector3.forward;
        float halfWidth = Vector3.Scale(doorCollider.size, widthAxis).magnitude * 0.5f;
        Vector3 negativeEdge = doorCollider.transform.TransformPoint(
            doorCollider.center - widthAxis * halfWidth);
        Vector3 positiveEdge = doorCollider.transform.TransformPoint(
            doorCollider.center + widthAxis * halfWidth);
        Vector3 edgePosition = (negativeEdge - transform.position).sqrMagnitude <
                               (positiveEdge - transform.position).sqrMagnitude
            ? negativeEdge
            : positiveEdge;
        edgePosition.y = transform.position.y;

        GameObject pivotObject = new($"{doorLeaf.name} Edge Pivot");
        Transform pivot = pivotObject.transform;
        pivot.SetParent(transform, false);
        pivot.position = edgePosition;
        doorLeaf.SetParent(pivot, true);
        return pivot;
    }

    private void FindPlayer()
    {
        GameObject minh = GameObject.Find("Minh");
        if (minh != null)
        {
            player = minh.transform;
        }
    }
}
