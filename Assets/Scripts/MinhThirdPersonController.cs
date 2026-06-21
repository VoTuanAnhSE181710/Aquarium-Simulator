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
    private const string CameraTargetName = "Camera Target";

    [SerializeField] private Transform cameraTransform;
    [SerializeField] private bool useFirstPersonCamera = true;
    [SerializeField] private Transform firstPersonCameraTarget;
    [SerializeField] private Vector3 firstPersonCameraOffset = Vector3.zero;
    [SerializeField] private float mouseSensitivity = 0.12f;
    [SerializeField] private float minPitch = -70f;
    [SerializeField] private float maxPitch = 70f;
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
    [SerializeField] private float bedInteractionDistance = 2.4f;
    [SerializeField] private float sleepSkipHours = 6f;
    [SerializeField] private DayNightCycle dayNightCycle;
    [SerializeField] private float walkingReleaseGraceTime = 0.08f;
    [SerializeField] private string roomDoorPrompt = "Press F to come in";
    [SerializeField] private string houseDoorPrompt = "Press F to get out";
    [SerializeField] private string sleepPrompt = "Press F to sleep";

    [Header("Interaction Audio")]
    [SerializeField] private AudioClip doorOpenClip;
    [SerializeField, Range(0f, 1f)] private float doorVolume = 0.8f;

    [Header("Movement Audio")]
    [SerializeField] private AudioSource movementAudioSource;
    [SerializeField] private AudioClip[] walkFootstepClips;
    [SerializeField] private AudioClip[] runFootstepClips;
    [SerializeField] private AudioClip walkFootstepClip;
    [SerializeField] private AudioClip runFootstepClip;
    [SerializeField] private AudioClip jumpClip;
    [SerializeField] private AudioClip landingClip;
    [SerializeField] private float walkStepInterval = 0.48f;
    [SerializeField] private float runStepInterval = 0.32f;
    [SerializeField, Range(0f, 1f)] private float footstepVolume = 0.34f;
    [SerializeField, Range(0f, 1f)] private float jumpVolume = 0.5f;
    [SerializeField, Range(0f, 1f)] private float landingVolume = 0.42f;
    [SerializeField] private float minLandingSoundVelocity = 4f;

    private CharacterController characterController;
    private float currentMoveSpeed;
    private float verticalVelocity;
    private Transform roomDoorPlaque;
    private Transform roomDoorMesh;
    private Transform characterHouseRoot;
    private Transform characterHouseDoor;
    private Transform bedTarget;
    private bool canEnterCharacterHouse;
    private bool canExitCharacterHouse;
    private bool canSleep;
    private GUIStyle promptStyle;
    private Animator animator;
    private float lastMovementInputTime = float.NegativeInfinity;
    private float cameraYaw;
    private float cameraPitch;
    private bool wasGrounded;
    private float nextFootstepTime;
    private float previousVerticalVelocity;
    private int nextWalkFootstepIndex;
    private int nextRunFootstepIndex;

    public void SetCameraTransform(Transform newCameraTransform)
    {
        cameraTransform = newCameraTransform;
    }

    private void Start()
    {
        characterController = GetComponent<CharacterController>();
        animator = GetComponent<Animator>();
        currentMoveSpeed = moveSpeed;
        wasGrounded = characterController.isGrounded;
        EnsureMovementAudio();

        if (cameraTransform == null && Camera.main != null)
        {
            cameraTransform = Camera.main.transform;
        }

        CacheFirstPersonCameraTarget();
        InitializeFirstPersonCamera();
        if (dayNightCycle == null)
        {
            dayNightCycle = FindFirstObjectByType<DayNightCycle>();
        }

        CacheSceneTargets();
    }

    private void Update()
    {
        if (LoadingManager.IsLoading)
        {
            // 1. Ép buộc Animator chuyển về trạng thái đứng im (Idle) ngay lập tức
            SetWalking(false);

            // 2. Vẫn cho phép Trọng lực hoạt động để nhân vật tự động rớt xuống đất 
            // trong lúc màn hình xanh đang che giấu.
            if (characterController.isGrounded)
            {
                verticalVelocity = -2f;
            }
            else
            {
                verticalVelocity += gravity * Time.deltaTime;
            }
            characterController.Move(Vector3.up * verticalVelocity * Time.deltaTime);

            // 3. Cập nhật ngầm trạng thái chạm đất để KHÔNG phát ra tiếng động "oạch" khi vào game
            wasGrounded = characterController.isGrounded;
            previousVerticalVelocity = verticalVelocity;

            return; // Dừng tại đây, không cho đọc bàn phím hay di chuyển
        }

        if (AquariumDecorationMode.IsDecorationMode) return;

        Keyboard keyboard = Keyboard.current;

        if (keyboard == null || cameraTransform == null)
        {
            return;
        }

        UpdateFirstPersonLook();
        CacheSceneTargets();
        canEnterCharacterHouse = CanInteractWithCharacterHouseDoor();
        canExitCharacterHouse = CanInteractWithMainHouseDoor();
        canSleep = CanInteractWithBed();

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

        if (keyboard.fKey.wasPressedThisFrame && TrySleep())
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

        if (!useFirstPersonCamera && hasMovementInput)
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
                PlayMovementSound(jumpClip, jumpVolume);
            }
        }
        else
        {
            verticalVelocity += gravity * Time.deltaTime;
        }

        Vector3 velocity = movement * currentMoveSpeed + Vector3.up * verticalVelocity;
        characterController.Move(velocity * Time.deltaTime);
        UpdateMovementAudio(hasMovementInput, wantsToRun);
    }

    private void LateUpdate()
    {
        if (AquariumDecorationMode.IsDecorationMode) return;

        ApplyFirstPersonCamera();
    }

    private void CacheFirstPersonCameraTarget()
    {
        if (firstPersonCameraTarget != null)
        {
            return;
        }

        Transform[] childTransforms = GetComponentsInChildren<Transform>(true);
        firstPersonCameraTarget = childTransforms.FirstOrDefault(candidate => candidate.name == CameraTargetName);
    }

    private void InitializeFirstPersonCamera()
    {
        if (!useFirstPersonCamera || cameraTransform == null)
        {
            return;
        }

        Behaviour cinemachineBrain = cameraTransform.GetComponent("CinemachineBrain") as Behaviour;
        if (cinemachineBrain != null)
        {
            cinemachineBrain.enabled = false;
        }

        ThirdPersonCameraFollow legacyFollow = cameraTransform.GetComponent<ThirdPersonCameraFollow>();
        if (legacyFollow != null)
        {
            legacyFollow.enabled = false;
        }

        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        cameraYaw = transform.eulerAngles.y;
        cameraPitch = 0f;
        ApplyFirstPersonCamera();
    }

    private void UpdateFirstPersonLook()
    {
        if (!useFirstPersonCamera) return;

        if (AquariumDecorationMode.IsDecorationMode) return;

        Mouse mouse = Mouse.current;
        if (mouse != null && !PauseMenuManager.GameIsPaused)
        {
            Vector2 mouseDelta = mouse.delta.ReadValue();
            cameraYaw += mouseDelta.x * mouseSensitivity;
            cameraPitch = Mathf.Clamp(cameraPitch - mouseDelta.y * mouseSensitivity, minPitch, maxPitch);
        }

        transform.rotation = Quaternion.Euler(0f, cameraYaw, 0f);
    }

    private void ApplyFirstPersonCamera()
    {
        if (!useFirstPersonCamera || cameraTransform == null)
        {
            return;
        }

        CacheFirstPersonCameraTarget();
        Transform cameraTarget = firstPersonCameraTarget != null ? firstPersonCameraTarget : transform;
        cameraTransform.position = cameraTarget.position + transform.TransformVector(firstPersonCameraOffset);
        cameraTransform.rotation = Quaternion.Euler(cameraPitch, cameraYaw, 0f);
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
        PlayInteractionSound(doorOpenClip, doorVolume);
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
        PlayInteractionSound(doorOpenClip, doorVolume);
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

        if (bedTarget == null || !bedTarget.gameObject.activeInHierarchy)
        {
            bedTarget = FindNearestBed();
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

    private void PlayInteractionSound(AudioClip clip, float volume)
    {
        if (movementAudioSource == null || clip == null || volume <= 0f)
        {
            return;
        }

        // Đổi cao độ ngẫu nhiên một chút để tiếng mở cửa nghe tự nhiên hơn
        movementAudioSource.pitch = Random.Range(0.95f, 1.05f);
        movementAudioSource.PlayOneShot(clip, volume);
    }

    private Transform FindNearestBed()
    {
        Transform[] sceneTransforms = FindObjectsByType<Transform>(FindObjectsInactive.Exclude, FindObjectsSortMode.None);
        Transform nearestBed = null;
        float bestDistance = float.MaxValue;

        foreach (Transform candidate in sceneTransforms)
        {
            if (!candidate.name.StartsWith("Bed", System.StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            float distance = (candidate.position - transform.position).sqrMagnitude;
            if (distance >= bestDistance)
            {
                continue;
            }

            bestDistance = distance;
            nearestBed = candidate;
        }

        return nearestBed;
    }

    private bool CanInteractWithBed()
    {
        if (bedTarget == null)
        {
            return false;
        }

        Vector3 nearestBedPoint = GetNearestInteractionPoint(bedTarget, transform.position);
        return (nearestBedPoint - transform.position).sqrMagnitude <=
               bedInteractionDistance * bedInteractionDistance;
    }

    private bool TrySleep()
    {
        if (!canSleep)
        {
            return false;
        }

        if (dayNightCycle == null)
        {
            dayNightCycle = FindFirstObjectByType<DayNightCycle>();
        }

        if (dayNightCycle == null)
        {
            return false;
        }

        dayNightCycle.SkipHours(sleepSkipHours);
        return true;
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
        if (PauseMenuManager.GameIsPaused || AquariumDecorationMode.IsDecorationMode)
        {
            return;
        }

        string promptText = canExitCharacterHouse
            ? houseDoorPrompt
            : canEnterCharacterHouse
                ? roomDoorPrompt
                : canSleep
                    ? sleepPrompt
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

    private void EnsureMovementAudio()
    {
        if (movementAudioSource == null)
        {
            movementAudioSource = GetComponent<AudioSource>();
        }

        if (movementAudioSource == null)
        {
            movementAudioSource = gameObject.AddComponent<AudioSource>();
        }

        movementAudioSource.playOnAwake = false;
        movementAudioSource.loop = false;
        movementAudioSource.spatialBlend = 0f;

        LoadMovementAudioAssets();

        walkFootstepClip ??= GetFirstClip(walkFootstepClips) ?? CreateFootstepClip("Minh Walk Footstep", 105f, 13);
        runFootstepClip ??= GetFirstClip(runFootstepClips) ?? CreateFootstepClip("Minh Run Footstep", 130f, 23);
        jumpClip ??= LoadMovementClip("cloth1") ?? CreateJumpClip();
        landingClip ??= LoadMovementClip("dropLeather") ?? CreateLandingClip();
    }

    private void UpdateMovementAudio(bool hasMovementInput, bool wantsToRun)
    {
        EnsureMovementAudio();

        bool isGrounded = characterController.isGrounded;
        if (!wasGrounded && isGrounded && previousVerticalVelocity < -minLandingSoundVelocity)
        {
            PlayMovementSound(landingClip, landingVolume);
        }

        if (!isGrounded || !hasMovementInput)
        {
            if (!hasMovementInput)
            {
                nextFootstepTime = Time.time;
            }

            wasGrounded = isGrounded;
            previousVerticalVelocity = verticalVelocity;
            return;
        }

        if (Time.time >= nextFootstepTime)
        {
            AudioClip footstepClip = GetNextFootstepClip(wantsToRun);
            PlayMovementSound(footstepClip, footstepVolume);
            float interval = wantsToRun ? runStepInterval : walkStepInterval;
            nextFootstepTime = Time.time + Mathf.Max(0.08f, interval);
        }

        wasGrounded = isGrounded;
        previousVerticalVelocity = verticalVelocity;
    }

    private void LoadMovementAudioAssets()
    {
        if (walkFootstepClips == null || walkFootstepClips.Length == 0)
        {
            walkFootstepClips = new[]
            {
                LoadMovementClip("footstep00"),
                LoadMovementClip("footstep01"),
                LoadMovementClip("footstep02"),
                LoadMovementClip("footstep03"),
                LoadMovementClip("footstep04")
            }.Where(clip => clip != null).ToArray();
        }

        if (runFootstepClips == null || runFootstepClips.Length == 0)
        {
            runFootstepClips = new[]
            {
                LoadMovementClip("footstep05"),
                LoadMovementClip("footstep06"),
                LoadMovementClip("footstep07"),
                LoadMovementClip("footstep08"),
                LoadMovementClip("footstep09")
            }.Where(clip => clip != null).ToArray();
        }
    }

    private AudioClip GetNextFootstepClip(bool wantsToRun)
    {
        AudioClip[] clips = wantsToRun ? runFootstepClips : walkFootstepClips;
        if (clips == null || clips.Length == 0)
        {
            return wantsToRun ? runFootstepClip : walkFootstepClip;
        }

        int index = wantsToRun ? nextRunFootstepIndex : nextWalkFootstepIndex;
        AudioClip clip = clips[index % clips.Length];

        if (wantsToRun)
        {
            nextRunFootstepIndex = (nextRunFootstepIndex + 1) % clips.Length;
        }
        else
        {
            nextWalkFootstepIndex = (nextWalkFootstepIndex + 1) % clips.Length;
        }

        return clip;
    }

    private static AudioClip GetFirstClip(AudioClip[] clips)
    {
        return clips == null || clips.Length == 0 ? null : clips[0];
    }

    private static AudioClip LoadMovementClip(string clipName)
    {
        return Resources.Load<AudioClip>($"Audio/SFX/Movement/{clipName}");
    }

    private void PlayMovementSound(AudioClip clip, float volume)
    {
        if (movementAudioSource == null || clip == null || volume <= 0f)
        {
            return;
        }

        movementAudioSource.PlayOneShot(clip, volume);
    }

    private static AudioClip CreateFootstepClip(string clipName, float toneFrequency, int seed)
    {
        const int sampleRate = 44100;
        int sampleCount = Mathf.CeilToInt(sampleRate * 0.13f);
        float[] samples = new float[sampleCount];
        System.Random random = new(seed);

        for (int i = 0; i < sampleCount; i++)
        {
            float t = i / (float)sampleRate;
            float envelope = Mathf.Exp(-t * 24f);
            float lowTone = Mathf.Sin(2f * Mathf.PI * toneFrequency * t) * 0.45f;
            float noise = ((float)random.NextDouble() * 2f - 1f) * 0.55f;
            samples[i] = (lowTone + noise) * envelope;
        }

        return CreateClip(clipName, sampleRate, samples);
    }

    private static AudioClip CreateJumpClip()
    {
        const int sampleRate = 44100;
        int sampleCount = Mathf.CeilToInt(sampleRate * 0.16f);
        float[] samples = new float[sampleCount];

        for (int i = 0; i < sampleCount; i++)
        {
            float t = i / (float)sampleRate;
            float envelope = Mathf.Sin(Mathf.Clamp01(t / 0.16f) * Mathf.PI) * 0.7f;
            float frequency = Mathf.Lerp(180f, 420f, t / 0.16f);
            samples[i] = Mathf.Sin(2f * Mathf.PI * frequency * t) * envelope;
        }

        return CreateClip("Minh Jump", sampleRate, samples);
    }

    private static AudioClip CreateLandingClip()
    {
        const int sampleRate = 44100;
        int sampleCount = Mathf.CeilToInt(sampleRate * 0.18f);
        float[] samples = new float[sampleCount];
        System.Random random = new(31);

        for (int i = 0; i < sampleCount; i++)
        {
            float t = i / (float)sampleRate;
            float envelope = Mathf.Exp(-t * 18f);
            float thud = Mathf.Sin(2f * Mathf.PI * 75f * t) * 0.6f;
            float noise = ((float)random.NextDouble() * 2f - 1f) * 0.3f;
            samples[i] = (thud + noise) * envelope;
        }

        return CreateClip("Minh Landing", sampleRate, samples);
    }

    private static AudioClip CreateClip(string clipName, int sampleRate, float[] samples)
    {
        AudioClip clip = AudioClip.Create(clipName, samples.Length, 1, sampleRate, false);
        clip.SetData(samples, 0);
        return clip;
    }
}
