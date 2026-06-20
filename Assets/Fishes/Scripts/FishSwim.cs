using UnityEngine;

public class FishSwim : MonoBehaviour
{
    public enum MovementType { Random, PingPong }
    public enum PatrolAxis { LocalX, LocalY, LocalZ }

    [Header("Cài đặt bể nước")]
    public Collider waterCollider; // Kéo WaterCube vào đây

    [Header("Cài đặt Nhóm loài (Để phân loại trong Shop)")]
    [Tooltip("Tên nhóm loài cá này (Ví dụ: Cá Rồng, Cá Cảnh, Cá Bảy Màu...)")]
    public string fishGroup = "Cá Cảnh";

    [Header("Chế độ di chuyển")]
    public MovementType movementType = MovementType.Random;
    [Tooltip("Trục tuần tra khi dùng chế độ PingPong (LocalX là chiều dài của bể)")]
    public PatrolAxis patrolAxis = PatrolAxis.LocalX;

    [Header("Cài đặt 8 hướng (Compass Movement)")]
    [Tooltip("Bắt buộc cá chỉ bơi theo 8 hướng chính (Đông, Tây, Nam, Bắc và các hướng chéo 45 độ)")]
    public bool use8Directions = true;

    [Header("Cài đặt bơi")]
    public float speed = 0.15f;       // Tốc độ di chuyển
    public float rotationSpeed = 2f;  // Tốc độ xoay đầu mượt mà

    [Header("Giới hạn vùng bơi tự chỉnh (Meters)")]
    [Tooltip("Thụt lề biên trái/phải (Trục X) để cá không thò đầu ra ngoài")]
    public float marginX = 0.2f;
    [Tooltip("Thụt lề biên dưới/trên (Trục Y) để cá không thò đầu lên mặt nước hoặc cắm xuống đáy")]
    public float marginY = 0.1f;
    [Tooltip("Thụt lề biên trước/sau (Trục Z) để cá không đâm vào kính trước sau")]
    public float marginZ = 0.2f;

    [Header("Cài đặt thời gian bơi (trước khi nghỉ)")]
    public bool enableResting = true;       // Bật chế độ nghỉ
    [Tooltip("Thời gian bơi tối thiểu trước khi con cá dừng lại nghỉ")]
    public float minSwimTime = 3f;          
    [Tooltip("Thời gian bơi tối đa trước khi con cá dừng lại nghỉ")]
    public float maxSwimTime = 8f;          

    [Header("Cài đặt thời gian nghỉ ngơi")]
    public float minRestTime = 2f;          // Thời gian nghỉ tối thiểu
    public float maxRestTime = 5f;          // Thời gian nghỉ tối đa

    [Header("Hiệu ứng sinh động (Premium)")]
    [Tooltip("Độ uốn mình (lắc lư) của cá khi đang bơi")]
    public float swimWobbleAmount = 15f; 
    [Tooltip("Tốc độ uốn mình (tần số lắc)")]
    public float swimWobbleSpeed = 4f;
    [Tooltip("Cho phép cá từ từ xoay đầu ngẫu nhiên nhìn xung quanh khi đang đứng yên nghỉ ngơi")]
    public bool enableIdleLookAround = true;
    public float idleLookSpeed = 1f;

    [Header("Tối ưu hóa chuyển động")]
    public bool lockVerticalRotation = true; // Khóa xoay lên/xuống (giúp cá không bị lộn)
    public float maxTimePerTarget = 18f;     // Thời gian tối đa cho 1 điểm

    private Vector3 targetPosition;
    private float targetTimer;
    private bool headingToPointB = true;

    private Vector3 pointA;
    private Vector3 pointB;
    private float dynamicReachDistance;

    // Biến điều khiển trạng thái nghỉ ngơi và bơi
    private bool isResting = false;
    private float restTimer = 0f;
    private float currentRestDuration = 0f;
    
    private float swimTimer = 0f;
    private float currentSwimDuration = 0f;

    private Quaternion idleTargetRotation;
    private float nextIdleLookTimer = 0f;

    // Khai báo Animator
    private Animator animator;
    private int speedParamHash;
    private bool hasSpeedParam = false;

    void Start()
    {
        animator = GetComponentInChildren<Animator>();
        if (animator != null)
        {
            foreach (AnimatorControllerParameter param in animator.parameters)
            {
                if (param.name == "Speed" || param.name == "speed")
                {
                    hasSpeedParam = true;
                    speedParamHash = Animator.StringToHash(param.name);
                    break;
                }
            }
        }

        // Tự động tìm Water Collider trong scene nếu chưa kéo thả
        if (waterCollider == null)
        {
            foreach (Collider col in FindObjectsByType<Collider>(FindObjectsSortMode.None))
            {
                if (col.isTrigger)
                {
                    string name = col.gameObject.name.ToLower();
                    string parentName = col.transform.parent != null ? col.transform.parent.gameObject.name.ToLower() : "";
                    
                    if (name.Contains("water") || name.Contains("tank") || name.Contains("inside") || name.Contains("aquarium") ||
                        parentName.Contains("water") || parentName.Contains("tank") || parentName.Contains("aquarium"))
                    {
                        waterCollider = col;
                        break;
                    }
                }
            }
        }

        // Đảm bảo cá không bị rơi xuống do trọng lực và có thể tự do di chuyển
        Rigidbody rb = GetComponent<Rigidbody>();
        if (rb != null)
        {
            rb.isKinematic = true;
            rb.useGravity = false;
        }

        // Tự động tính toán Margin an toàn dựa trên kích thước thật của model cá để tránh tràn viền/xuyên kính
        Renderer fishRenderer = GetComponentInChildren<Renderer>();
        if (fishRenderer != null)
        {
            float fishLength = Mathf.Max(fishRenderer.bounds.size.x, fishRenderer.bounds.size.z);
            float idealMargin = (fishLength * 0.5f) + 0.08f; // Nửa chiều dài cá + 8cm khoảng cách an toàn
            marginX = Mathf.Max(marginX, idealMargin);
            marginZ = Mathf.Max(marginZ, idealMargin);
            marginY = Mathf.Max(marginY, (fishRenderer.bounds.size.y * 0.5f) + 0.05f);
        }

        if (waterCollider != null)
        {
            // Giới hạn vị trí ban đầu của cá luôn nằm gọn trong bể nước
            Bounds bounds = waterCollider.bounds;
            Vector3 pos = transform.position;
            float clampMarginX = Mathf.Min(marginX, bounds.size.x * 0.45f);
            float clampMarginY = Mathf.Min(marginY, bounds.size.y * 0.45f);
            float clampMarginZ = Mathf.Min(marginZ, bounds.size.z * 0.45f);

            pos.x = Mathf.Clamp(pos.x, bounds.min.x + clampMarginX, bounds.max.x - clampMarginX);
            pos.y = Mathf.Clamp(pos.y, bounds.min.y + clampMarginY, bounds.max.y - clampMarginY);
            pos.z = Mathf.Clamp(pos.z, bounds.min.z + clampMarginZ, bounds.max.z - clampMarginZ);
            transform.position = pos;

            SetupPath();
            StartSwimming();
        }
        else
        {
            Debug.LogError("Vui lòng kéo WaterCube vào ô Water Collider của cá!");
        }
    }


    void Update()
    {
        if (waterCollider == null) return;

        // Xử lý khi cá đang NGHỈ NGƠI
        if (isResting)
        {
            HandleRestingState();
            return;
        }

        // Xử lý khi cá đang BƠI
        HandleSwimmingState();
    }

    void StartSwimming()
    {
        isResting = false;
        swimTimer = 0f;
        currentSwimDuration = Random.Range(minSwimTime, maxSwimTime);

        if (Vector3.Distance(transform.position, targetPosition) < dynamicReachDistance || targetPosition == Vector3.zero)
        {
            UpdateTarget();
        }
    }

    void StartResting()
    {
        isResting = true;
        restTimer = 0f;
        currentRestDuration = Random.Range(minRestTime, maxRestTime);
        idleTargetRotation = transform.rotation;
        nextIdleLookTimer = 0f;

        if (Vector3.Distance(transform.position, targetPosition) < dynamicReachDistance)
        {
            UpdateTarget();
        }
    }

    void HandleRestingState()
    {
        restTimer += Time.deltaTime;
        
        if (animator != null && hasSpeedParam)
        {
            animator.SetFloat(speedParamHash, 0f);
        }

        if (enableIdleLookAround)
        {
            nextIdleLookTimer -= Time.deltaTime;
            if (nextIdleLookTimer <= 0)
            {
                float randomYaw = transform.eulerAngles.y + Random.Range(-40f, 40f);
                idleTargetRotation = Quaternion.Euler(0, randomYaw, 0);
                nextIdleLookTimer = Random.Range(1f, 2.5f);
            }

            transform.rotation = Quaternion.Slerp(transform.rotation, idleTargetRotation, idleLookSpeed * Time.deltaTime);
        }

        if (restTimer >= currentRestDuration)
        {
            StartSwimming();
        }
    }

    void HandleSwimmingState()
    {
        targetTimer += Time.deltaTime;
        swimTimer += Time.deltaTime;

        Vector3 direction = targetPosition - transform.position;
        
        if (direction != Vector3.zero)
        {
            if (lockVerticalRotation)
            {
                direction.y = 0;
            }

            if (direction != Vector3.zero)
            {
                Quaternion baseRotation = Quaternion.LookRotation(direction);
                float wobble = Mathf.Sin(Time.time * swimWobbleSpeed) * swimWobbleAmount;
                Quaternion wobbleRotation = Quaternion.Euler(0, wobble, 0);

                transform.rotation = Quaternion.Slerp(transform.rotation, baseRotation * wobbleRotation, rotationSpeed * Time.deltaTime);
            }
        }

        transform.position = Vector3.MoveTowards(transform.position, targetPosition, speed * Time.deltaTime);

        if (animator != null && hasSpeedParam)
        {
            animator.SetFloat(speedParamHash, speed);
        }

        if (enableResting && swimTimer >= currentSwimDuration)
        {
            StartResting();
        }
        else if (Vector3.Distance(transform.position, targetPosition) < dynamicReachDistance || targetTimer >= maxTimePerTarget)
        {
            if (enableResting)
            {
                StartResting();
            }
            else
            {
                UpdateTarget();
            }
        }
    }

    void SetupPath()
    {
        if (movementType == MovementType.PingPong)
        {
            CalculatePingPongPoints();
            targetPosition = headingToPointB ? pointB : pointA;
        }
        else
        {
            GetNewRandomTarget();
        }
    }

    void UpdateTarget()
    {
        targetTimer = 0f;
        if (movementType == MovementType.PingPong)
        {
            headingToPointB = !headingToPointB;
            targetPosition = headingToPointB ? pointB : pointA;
        }
        else
        {
            GetNewRandomTarget();
        }
    }

    void CalculatePingPongPoints()
    {
        Vector3 center = waterCollider.bounds.center;
        Vector3 size = waterCollider.bounds.size;
        
        Vector3 directionVector = Vector3.right; // Local X
        float halfLength = size.x * 0.5f;
        float currentMargin = marginX;

        if (patrolAxis == PatrolAxis.LocalY)
        {
            directionVector = Vector3.up;
            halfLength = size.y * 0.5f;
            currentMargin = marginY;
        }
        else if (patrolAxis == PatrolAxis.LocalZ)
        {
            directionVector = Vector3.forward;
            halfLength = size.z * 0.5f;
            currentMargin = marginZ;
        }

        directionVector = waterCollider.transform.TransformDirection(directionVector).normalized;

        float travelDistance = Mathf.Max(0.01f, halfLength - currentMargin);

        float currentY = transform.position.y;
        
        pointA = center - directionVector * travelDistance;
        pointB = center + directionVector * travelDistance;

        pointA.y = currentY;
        pointB.y = currentY;

        dynamicReachDistance = Vector3.Distance(pointA, pointB) * 0.1f;
        dynamicReachDistance = Mathf.Clamp(dynamicReachDistance, 0.01f, 0.5f);
    }

    void GetNewRandomTarget()
    {
        targetTimer = 0f;
        Bounds bounds = waterCollider.bounds;
        
        float clampMarginX = Mathf.Min(marginX, bounds.size.x * 0.45f);
        float clampMarginY = Mathf.Min(marginY, bounds.size.y * 0.45f);
        float clampMarginZ = Mathf.Min(marginZ, bounds.size.z * 0.45f);

        if (use8Directions)
        {
            // Bơi theo 8 hướng la bàn (0, 45, 90, 135, 180, 225, 270, 315 độ)
            int randomAngleIndex = Random.Range(0, 8);
            float angle = randomAngleIndex * 45f;
            float rad = angle * Mathf.Deg2Rad;
            Vector3 dir = new Vector3(Mathf.Cos(rad), 0, Mathf.Sin(rad));

            // Xác định điểm xuất phát hiện tại của cá (giới hạn trong bể)
            Vector3 startPos = transform.position;
            startPos.x = Mathf.Clamp(startPos.x, bounds.min.x + clampMarginX, bounds.max.x - clampMarginX);
            startPos.z = Mathf.Clamp(startPos.z, bounds.min.z + clampMarginZ, bounds.max.z - clampMarginZ);

            // Tính khoảng cách tối đa đến vách bể theo hướng đi dir
            float tX = (dir.x > 0) ? (bounds.max.x - clampMarginX - startPos.x) / dir.x : (bounds.min.x + clampMarginX - startPos.x) / dir.x;
            float tZ = (dir.z > 0) ? (bounds.max.z - clampMarginZ - startPos.z) / dir.z : (bounds.min.z + clampMarginZ - startPos.z) / dir.z;

            if (Mathf.Abs(dir.x) < 0.0001f) tX = float.MaxValue;
            if (Mathf.Abs(dir.z) < 0.0001f) tZ = float.MaxValue;

            float maxDist = Mathf.Min(Mathf.Abs(tX), Mathf.Abs(tZ));

            // Chọn khoảng cách bơi ngẫu nhiên từ 40% đến 100% khoảng cách đến vách bể
            float swimDistance = maxDist * Random.Range(0.4f, 1f);
            swimDistance = Mathf.Max(swimDistance, 0.1f); // Đảm bảo bơi tối thiểu 10cm

            targetPosition = startPos + dir * swimDistance;
            
            // Random nhẹ độ cao Y trong giới hạn bể
            targetPosition.y = Random.Range(bounds.min.y + clampMarginY, bounds.max.y - clampMarginY);
        }
        else
        {
            // Bơi ngẫu nhiên hoàn toàn
            float randomX = Random.Range(bounds.min.x + clampMarginX, bounds.max.x - clampMarginX);
            float randomY = Random.Range(bounds.min.y + clampMarginY, bounds.max.y - clampMarginY);
            float randomZ = Random.Range(bounds.min.z + clampMarginZ, bounds.max.z - clampMarginZ);

            targetPosition = new Vector3(randomX, randomY, randomZ);
        }

        float minDimension = Mathf.Min(bounds.size.x, Mathf.Min(bounds.size.y, bounds.size.z));
        dynamicReachDistance = minDimension * 0.1f;
        dynamicReachDistance = Mathf.Clamp(dynamicReachDistance, 0.01f, 0.2f);
    }

    void OnDrawGizmosSelected()
    {
        if (waterCollider != null)
        {
            if (movementType == MovementType.PingPong)
            {
                CalculatePingPongPoints();
                Gizmos.color = Color.green;
                Gizmos.DrawLine(pointA, pointB);
                Gizmos.DrawSphere(pointA, dynamicReachDistance);
                Gizmos.DrawSphere(pointB, dynamicReachDistance);
            }
            
            Bounds bounds = waterCollider.bounds;
            Vector3 center = bounds.center;
            
            float sizeX = Mathf.Max(0.01f, bounds.size.x - (marginX * 2f));
            float sizeY = Mathf.Max(0.01f, bounds.size.y - (marginY * 2f));
            float sizeZ = Mathf.Max(0.01f, bounds.size.z - (marginZ * 2f));
            
            Gizmos.color = Color.yellow;
            Gizmos.DrawWireCube(center, new Vector3(sizeX, sizeY, sizeZ));
        }
    }
}
