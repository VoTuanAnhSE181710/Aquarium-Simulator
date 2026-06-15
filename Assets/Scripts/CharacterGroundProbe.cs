using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public sealed class CharacterGroundProbe : MonoBehaviour
{
    [SerializeField] private LayerMask groundMask = ~0;
    [SerializeField] private float probeDistance = 0.35f;
    [SerializeField] private float probeRadiusPadding = 0.04f;
    [SerializeField] private float normalSharpness = 18f;

    private CharacterController characterController;
    private Vector3 groundNormal = Vector3.up;

    public bool IsGrounded { get; private set; }
    public bool HasGroundHit { get; private set; }
    public Vector3 GroundPoint { get; private set; }
    public Vector3 GroundNormal => groundNormal;

    private void Awake()
    {
        characterController = GetComponent<CharacterController>();
    }

    private void Update()
    {
        ProbeGround();
    }

    private void ProbeGround()
    {
        Bounds bounds = characterController.bounds;
        float radius = Mathf.Max(0.02f, characterController.radius * Mathf.Max(transform.lossyScale.x, transform.lossyScale.z) - probeRadiusPadding);
        Vector3 origin = new Vector3(bounds.center.x, bounds.min.y + radius + 0.08f, bounds.center.z);
        float distance = radius + probeDistance + Mathf.Max(characterController.skinWidth, 0.01f);

        HasGroundHit = Physics.SphereCast(
            origin,
            radius,
            Vector3.down,
            out RaycastHit hit,
            distance,
            groundMask,
            QueryTriggerInteraction.Ignore);

        if (HasGroundHit)
        {
            GroundPoint = hit.point;
            groundNormal = Vector3.Slerp(
                groundNormal,
                hit.normal,
                1f - Mathf.Exp(-normalSharpness * Time.deltaTime));
        }
        else
        {
            groundNormal = Vector3.Slerp(
                groundNormal,
                Vector3.up,
                1f - Mathf.Exp(-normalSharpness * Time.deltaTime));
        }

        IsGrounded = characterController.isGrounded || (HasGroundHit && hit.distance <= distance);
    }

    public Vector3 ProjectOnGround(Vector3 direction)
    {
        Vector3 projected = Vector3.ProjectOnPlane(direction, groundNormal);
        return projected.sqrMagnitude > 0.0001f ? projected.normalized : Vector3.zero;
    }
}
