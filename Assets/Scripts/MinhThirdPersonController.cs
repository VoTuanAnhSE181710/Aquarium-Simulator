using UnityEngine;
using UnityEngine.InputSystem;
using System.Linq;

[RequireComponent(typeof(CharacterController))]
public sealed class MinhThirdPersonController : MonoBehaviour
{
    private static readonly int IsWalkingHash = Animator.StringToHash("isWalking");

    private const string CharacterHouseName = "CharacterHouse (1)";
    private const string CharacterHouseDoorName = "Door Main";
    private const string CharacterHouseFallbackDoorName = "Door";
    private const string CharacterHouseDoorPath = "Floors/Door Main";
    private const string CharacterHouseFallbackDoorPath = "Floors/Door";
    private const string RoomDoorPlaqueName = "DoorNumberPlaque_07";
    private const string RoomDoorMeshName = "WallDoor";

    [SerializeField] private Transform cameraTransform;
    [SerializeField] private float moveSpeed = 4f;
    [SerializeField] private float runSpeed = 6.5f;
    [SerializeField] private float speedChangeRate = 10f;
    [SerializeField] private float turnSpeed = 540f;
    [SerializeField] private float obstacleCheckDistance = 0.6f;
    [SerializeField] private LayerMask obstacleMask = ~0;
    [SerializeField] private float gravity = -20f;
    [SerializeField] private float jumpHeight = 1.4f;
    [SerializeField] private float roomDoorInteractionDistance = 1.8f;
    [SerializeField] private float teleportLandingOffset = 1.1f;
    [SerializeField] private float houseDoorInteractionDistance = 3.2f;
    [SerializeField] private float houseExitLandingOffset = 1.3f;
    [SerializeField] private float walkingReleaseGraceTime = 0.08f;
    [SerializeField] private string roomDoorPrompt = "Press F to come in";
    [SerializeField] private string houseDoorPrompt = "Press F to get out";

    private CharacterController characterController;
    private float currentMoveSpeed;
    private float verticalVelocity;
    private Transform roomDoorPlaque;
    private Transform roomDoorMesh;
    private Transform characterHouseRoot;
    private Transform characterHouseDoor;
    private bool canEnterCharacterHouse;
    private bool canExitCharacterHouse;
    private GUIStyle promptStyle;
    private Animator animator;
    private float lastMovementInputTime = float.NegativeInfinity;

    public void SetCameraTransform(Transform newCameraTransform)
    {
        cameraTransform = newCameraTransform;
    }

    private void Start()
    {
        characterController = GetComponent<CharacterController>();
        animator = GetComponent<Animator>();
        currentMoveSpeed = moveSpeed;

        if (cameraTransform == null && Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }

        CacheSceneTargets();
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard == null || cameraTransform == null)
        {
            return;
        }

        CacheSceneTargets();
        canEnterCharacterHouse = CanInteractWithCharacterHouseDoor();
        canExitCharacterHouse = CanInteractWithMainHouseDoor();

        if (keyboard.fKey.wasPressedThisFrame && TryTeleportToCharacterHouse())
        {
            SetWalking(false);
            return;
        }

        if (keyboard.fKey.wasPressedThisFrame && TryTeleportBackToRoomDoor())
        {
            SetWalking(false);
            return;
        }

        Vector2 input = ReadMovementInput(keyboard);
        bool hasMovementInput = input.sqrMagnitude > 0.01f;
        if (hasMovementInput)
        {
            lastMovementInputTime = Time.time;
        }

        bool shouldWalk = hasMovementInput || Time.time - lastMovementInputTime <= walkingReleaseGraceTime;
        SetWalking(shouldWalk);
        bool wantsToRun = hasMovementInput && IsRunPressed(keyboard);
        float targetMoveSpeed = wantsToRun ? runSpeed : moveSpeed;
        currentMoveSpeed = Mathf.MoveTowards(
            currentMoveSpeed,
            targetMoveSpeed,
            speedChangeRate * Time.deltaTime);

        Vector3 cameraForward = Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up).normalized;
        Vector3 cameraRight = Vector3.ProjectOnPlane(cameraTransform.right, Vector3.up).normalized;
        Vector3 movement = (cameraForward * input.y + cameraRight * input.x).normalized;

        if (hasMovementInput)
        {
            Quaternion targetRotation = Quaternion.LookRotation(movement, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(
                transform.rotation,
                targetRotation,
                turnSpeed * Time.deltaTime);
        }

        if (characterController.isGrounded)
        {
            verticalVelocity = -2f;

            if (keyboard.spaceKey.wasPressedThisFrame)
            {
                verticalVelocity = Mathf.Sqrt(jumpHeight * -2f * gravity);
            }
        }
        else
        {
            verticalVelocity += gravity * Time.deltaTime;
        }

        Vector3 velocity = movement * currentMoveSpeed + Vector3.up * verticalVelocity;
        characterController.Move(velocity * Time.deltaTime);
    }

    private bool TryTeleportToCharacterHouse()
    {
        if (!canEnterCharacterHouse || characterHouseRoot == null)
        {
            return false;
        }

        Vector3 destination = GetCharacterHouseDestination();
        characterController.enabled = false;
        transform.position = destination;
        transform.rotation = GetCharacterHouseFacing(destination);
        characterController.enabled = true;
        verticalVelocity = 0f;
        return true;
    }

    private bool TryTeleportBackToRoomDoor()
    {
        if (!canExitCharacterHouse || roomDoorMesh == null)
        {
            return false;
        }

        Vector3 destination = GetRoomDoorDestination();
        characterController.enabled = false;
        transform.position = destination;
        transform.rotation = GetRoomDoorFacing(destination);
        characterController.enabled = true;
        verticalVelocity = 0f;
        return true;
    }

    private bool CanInteractWithCharacterHouseDoor()
    {
        Transform interactionTarget = roomDoorMesh != null ? roomDoorMesh : roomDoorPlaque;
        if (interactionTarget == null)
        {
            return false;
        }

        Vector3 nearestDoorPoint = GetNearestInteractionPoint(interactionTarget, transform.position);
        return (nearestDoorPoint - transform.position).sqrMagnitude <=
               roomDoorInteractionDistance * roomDoorInteractionDistance;
    }

    private bool CanInteractWithMainHouseDoor()
    {
        if (characterHouseDoor == null)
        {
            return false;
        }

        Vector3 nearestDoorPoint = GetNearestInteractionPoint(characterHouseDoor, transform.position);
        return (nearestDoorPoint - transform.position).sqrMagnitude <=
               houseDoorInteractionDistance * houseDoorInteractionDistance;
    }

    private void CacheSceneTargets()
    {
        if (roomDoorPlaque == null)
        {
            GameObject plaqueObject = GameObject.Find(RoomDoorPlaqueName);
            if (plaqueObject != null)
            {
                roomDoorPlaque = plaqueObject.transform;
            }
        }

        if (roomDoorMesh == null)
        {
            Transform[] wallDoors = FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None)
                .Where(candidate => candidate.name == RoomDoorMeshName)
                .ToArray();
            if (wallDoors.Length > 0)
            {
                roomDoorMesh = roomDoorPlaque == null
                    ? wallDoors[0]
                    : wallDoors.OrderBy(candidate =>
                        (candidate.position - roomDoorPlaque.position).sqrMagnitude).First();
            }
        }

        if (characterHouseRoot == null)
        {
            GameObject houseObject = GameObject.Find(CharacterHouseName);
            if (houseObject != null)
            {
                characterHouseRoot = houseObject.transform;
            }
        }

        if (characterHouseRoot != null && characterHouseDoor == null)
        {
            characterHouseDoor = FindCharacterHouseDoor();
        }
    }

    private Transform FindCharacterHouseDoor()
    {
        Transform door = characterHouseRoot.Find(CharacterHouseDoorPath);
        if (door != null)
        {
            return door;
        }

        door = characterHouseRoot.Find(CharacterHouseFallbackDoorPath);
        if (door != null)
        {
            return door;
        }

        Transform[] houseTransforms = characterHouseRoot.GetComponentsInChildren<Transform>(true);
        door = houseTransforms.FirstOrDefault(candidate => candidate.name == CharacterHouseDoorName);
        if (door != null)
        {
            return door;
        }

        return houseTransforms.FirstOrDefault(candidate => candidate.name == CharacterHouseFallbackDoorName);
    }

    private Vector3 GetCharacterHouseDestination()
    {
        Transform anchor = characterHouseDoor != null ? characterHouseDoor : characterHouseRoot;
        Vector3 anchorPosition = anchor.position;
        Vector3 doorNormal = Vector3.ProjectOnPlane(anchor.forward, Vector3.up);
        if (doorNormal.sqrMagnitude < 0.0001f)
        {
            doorNormal = Vector3.ProjectOnPlane(anchor.right, Vector3.up);
        }

        doorNormal = doorNormal.sqrMagnitude < 0.0001f
            ? Vector3.forward
            : doorNormal.normalized;

        Vector3 candidateA = anchorPosition + doorNormal * teleportLandingOffset;
        Vector3 candidateB = anchorPosition - doorNormal * teleportLandingOffset;
        Vector3 houseReference = characterHouseRoot != null ? characterHouseRoot.position : anchorPosition;
        Vector3 destination = Vector3.Distance(candidateA, houseReference) <= Vector3.Distance(candidateB, houseReference)
            ? candidateA
            : candidateB;

        destination.y = GetGroundedY(destination, characterHouseRoot != null ? characterHouseRoot.position.y : transform.position.y);
        return destination;
    }

    private Quaternion GetCharacterHouseFacing(Vector3 destination)
    {
        Transform anchor = characterHouseDoor != null ? characterHouseDoor : characterHouseRoot;
        Vector3 lookDirection = Vector3.ProjectOnPlane(anchor.position - destination, Vector3.up);
        if (lookDirection.sqrMagnitude < 0.0001f)
        {
            lookDirection = Vector3.ProjectOnPlane(characterHouseRoot.forward, Vector3.up);
        }

        return lookDirection.sqrMagnitude < 0.0001f
            ? transform.rotation
            : Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
    }

    private Vector3 GetRoomDoorDestination()
    {
        Transform anchor = roomDoorMesh != null ? roomDoorMesh : roomDoorPlaque;
        Vector3 anchorPosition = anchor.position;
        Vector3 doorNormal = Vector3.ProjectOnPlane(anchor.forward, Vector3.up);
        if (doorNormal.sqrMagnitude < 0.0001f)
        {
            doorNormal = Vector3.ProjectOnPlane(anchor.right, Vector3.up);
        }

        doorNormal = doorNormal.sqrMagnitude < 0.0001f
            ? Vector3.forward
            : doorNormal.normalized;

        Vector3 candidateA = anchorPosition + doorNormal * houseExitLandingOffset;
        Vector3 candidateB = anchorPosition - doorNormal * houseExitLandingOffset;
        Vector3 plaqueReference = roomDoorPlaque != null ? roomDoorPlaque.position : anchorPosition;
        Vector3 destination = Vector3.Distance(candidateA, plaqueReference) <= Vector3.Distance(candidateB, plaqueReference)
            ? candidateA
            : candidateB;

        destination.y = GetGroundedY(destination, transform.position.y);
        return destination;
    }

    private Quaternion GetRoomDoorFacing(Vector3 destination)
    {
        Transform anchor = roomDoorMesh != null ? roomDoorMesh : roomDoorPlaque;
        Vector3 lookDirection = Vector3.ProjectOnPlane(anchor.position - destination, Vector3.up);

        return lookDirection.sqrMagnitude < 0.0001f
            ? transform.rotation
            : Quaternion.LookRotation(lookDirection.normalized, Vector3.up);
    }

    private float GetGroundedY(Vector3 destination, float fallbackY)
    {
        Vector3 rayOrigin = destination + Vector3.up * 4f;
        if (Physics.Raycast(rayOrigin, Vector3.down, out RaycastHit hit, 10f, obstacleMask, QueryTriggerInteraction.Ignore))
        {
            return hit.point.y;
        }

        return fallbackY;
    }

    private static Vector3 GetNearestInteractionPoint(Transform target, Vector3 playerPosition)
    {
        Collider targetCollider = target.GetComponent<Collider>();
        if (targetCollider != null)
        {
            return targetCollider.bounds.ClosestPoint(playerPosition);
        }

        Renderer targetRenderer = target.GetComponent<Renderer>();
        if (targetRenderer != null)
        {
            return targetRenderer.bounds.ClosestPoint(playerPosition);
        }

        return target.position;
    }

    private GUIStyle CreatePromptStyle()
    {
        return new GUIStyle(GUI.skin.box)
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 18,
            fontStyle = FontStyle.Bold,
            normal =
            {
                textColor = Color.white
            },
            padding = new RectOffset(16, 16, 10, 10)
        };
    }

    private void OnGUI()
    {
        if (PauseMenuManager.GameIsPaused)
        {
            return;
        }

        string promptText = canExitCharacterHouse
            ? houseDoorPrompt
            : canEnterCharacterHouse
                ? roomDoorPrompt
                : null;

        if (string.IsNullOrEmpty(promptText))
        {
            return;
        }

        if (promptStyle == null)
        {
            promptStyle = CreatePromptStyle();
        }

        const float width = 260f;
        const float height = 42f;
        Rect promptRect = new(
            (Screen.width - width) * 0.5f,
            Screen.height - 110f,
            width,
            height);
        GUI.Box(promptRect, promptText, promptStyle);
    }

    private bool IsBlocked(Vector3 movement)
    {
        Vector3 rayOrigin = transform.position + Vector3.up * 0.5f;
        return Physics.Raycast(
            rayOrigin,
            movement,
            obstacleCheckDistance,
            obstacleMask,
            QueryTriggerInteraction.Ignore);
    }

    private static Vector2 ReadMovementInput(Keyboard keyboard)
    {
        float horizontal = 0f;
        float vertical = 0f;

        if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed)
        {
            horizontal -= 1f;
        }

        if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed)
        {
            horizontal += 1f;
        }

        if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed)
        {
            vertical -= 1f;
        }

        if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed)
        {
            vertical += 1f;
        }

        return Vector2.ClampMagnitude(new Vector2(horizontal, vertical), 1f);
    }

    private static bool IsRunPressed(Keyboard keyboard)
    {
        return keyboard.leftShiftKey.isPressed || keyboard.rightShiftKey.isPressed;
    }

    private void SetWalking(bool isWalking)
    {
        if (animator == null)
        {
            return;
        }

        animator.SetBool(IsWalkingHash, isWalking);
    }
}
