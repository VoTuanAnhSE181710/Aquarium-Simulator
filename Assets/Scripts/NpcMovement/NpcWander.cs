using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public sealed class NpcWander : MonoBehaviour
{
    private static readonly int IsWalkingHash = Animator.StringToHash("isWalking");

    [SerializeField] private float moveSpeed = 1.4f;
    [SerializeField] private float turnSpeed = 180f;
    [SerializeField] private float roamRadius = 12f;
    [SerializeField] private float directionChangeInterval = 3f;
    [SerializeField] private float obstacleCheckDistance = 1.2f;
    [SerializeField] private LayerMask obstacleMask = ~0;
    [SerializeField] private float gravity = -20f;
    [SerializeField] private float walkingSpeedThreshold = 0.05f;
    [SerializeField] private float walkingReleaseGraceTime = 0.08f;

    private CharacterController characterController;
    private CharacterGroundProbe groundProbe;
    private Animator animator;
    private Vector3 origin;
    private Vector3 heading;
    private float nextDirectionChangeTime;
    private float verticalVelocity;
    private float lastMovingTime = float.NegativeInfinity;

    private void Start()
    {
        characterController = GetComponent<CharacterController>();
        groundProbe = GetComponent<CharacterGroundProbe>();
        animator = GetComponent<Animator>();
        origin = transform.position;
        PickNewHeading();
    }

    private void Update()
    {
        bool isOutsideRoamArea = Vector3.Distance(transform.position, origin) > roamRadius;
        bool shouldChangeDirection = Time.time >= nextDirectionChangeTime || IsBlocked();

        if (isOutsideRoamArea)
        {
            heading = (origin - transform.position).normalized;
        }
        else if (shouldChangeDirection)
        {
            PickNewHeading();
        }

        if (heading.sqrMagnitude < 0.01f)
        {
            SetWalking(false);
            PickNewHeading();
            return;
        }

        Vector3 movementHeading = groundProbe != null ? groundProbe.ProjectOnGround(heading) : heading;
        if (movementHeading.sqrMagnitude < 0.01f)
        {
            movementHeading = Vector3.ProjectOnPlane(heading, Vector3.up).normalized;
        }

        Quaternion targetRotation = Quaternion.LookRotation(movementHeading, Vector3.up);
        transform.rotation = Quaternion.RotateTowards(
            transform.rotation,
            targetRotation,
            turnSpeed * Time.deltaTime);

        bool isGrounded = groundProbe != null ? groundProbe.IsGrounded : characterController.isGrounded;
        verticalVelocity = isGrounded ? -2f : verticalVelocity + gravity * Time.deltaTime;
        Vector3 velocity = movementHeading * moveSpeed + Vector3.up * verticalVelocity;
        characterController.Move(velocity * Time.deltaTime);

        float horizontalSpeed = Vector3.ProjectOnPlane(characterController.velocity, Vector3.up).magnitude;
        if (horizontalSpeed > walkingSpeedThreshold)
        {
            lastMovingTime = Time.time;
        }

        bool shouldWalk =
            horizontalSpeed > walkingSpeedThreshold ||
            Time.time - lastMovingTime <= walkingReleaseGraceTime;
        SetWalking(shouldWalk);
    }

    private bool IsBlocked()
    {
        Vector3 rayOrigin = transform.position + Vector3.up * 0.5f;
        return Physics.Raycast(
            rayOrigin,
            transform.forward,
            obstacleCheckDistance,
            obstacleMask,
            QueryTriggerInteraction.Ignore);
    }

    private void PickNewHeading()
    {
        Vector2 randomDirection = Random.insideUnitCircle.normalized;
        if (randomDirection.sqrMagnitude < 0.01f)
        {
            randomDirection = Vector2.up;
        }

        heading = new Vector3(randomDirection.x, 0f, randomDirection.y);
        nextDirectionChangeTime = Time.time + directionChangeInterval;
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
