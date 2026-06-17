using UnityEngine;

[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(Animator))]
public sealed class CharacterAnimatorDriver : MonoBehaviour
{
    private static readonly int SpeedHash = Animator.StringToHash("Speed");
    private static readonly int GroundedHash = Animator.StringToHash("Grounded");
    private static readonly int AirborneHash = Animator.StringToHash("Airborne");
    private static readonly int VerticalSpeedHash = Animator.StringToHash("VerticalSpeed");
    private static readonly int HardLandingHash = Animator.StringToHash("HardLanding");

    [SerializeField] private float fullWalkSpeed = 1.4f;
    [SerializeField] private float fullRunSpeed = 6.5f;
    [SerializeField] private float maxPlaybackSpeed = 1.35f;
    [SerializeField] private float dampTime = 0.12f;
    [SerializeField] private float hardLandingSpeed = -7f;

    private Animator animator;
    private CharacterController characterController;
    private CharacterGroundProbe groundProbe;
    private bool hasSpeedParameter;
    private bool hasGroundedParameter;
    private bool hasAirborneParameter;
    private bool hasVerticalSpeedParameter;
    private bool hasHardLandingParameter;
    private bool wasGrounded;
    private float lastAirborneVerticalSpeed;

    private void Start()
    {
        animator = GetComponent<Animator>();
        characterController = GetComponent<CharacterController>();
        if (animator == null || characterController == null)
        {
            enabled = false;
            return;
        }

        groundProbe = GetComponent<CharacterGroundProbe>();
        hasSpeedParameter = HasParameter(SpeedHash);
        hasGroundedParameter = HasParameter(GroundedHash);
        hasAirborneParameter = HasParameter(AirborneHash);
        hasVerticalSpeedParameter = HasParameter(VerticalSpeedHash);
        hasHardLandingParameter = HasParameter(HardLandingHash);
    }

    private void Update()
    {
        float horizontalSpeed = Vector3.ProjectOnPlane(characterController.velocity, Vector3.up).magnitude;
        if (hasSpeedParameter)
        {
            float normalizedSpeed = Mathf.Clamp01(horizontalSpeed / Mathf.Max(fullWalkSpeed, 0.01f));
            animator.SetFloat(SpeedHash, normalizedSpeed, dampTime, Time.deltaTime);

            float runBlend = Mathf.InverseLerp(fullWalkSpeed, fullRunSpeed, horizontalSpeed);
            animator.speed = Mathf.Lerp(1f, maxPlaybackSpeed, runBlend);
        }
        else
        {
            animator.speed = 1f;
        }

        bool isGrounded = groundProbe != null ? groundProbe.IsGrounded : characterController.isGrounded;
        float verticalSpeed = characterController.velocity.y;

        if (!isGrounded)
        {
            lastAirborneVerticalSpeed = verticalSpeed;
        }

        if (hasGroundedParameter)
        {
            animator.SetBool(GroundedHash, isGrounded);
        }

        if (hasAirborneParameter)
        {
            animator.SetBool(AirborneHash, !isGrounded);
        }

        if (hasVerticalSpeedParameter)
        {
            animator.SetFloat(VerticalSpeedHash, verticalSpeed, dampTime, Time.deltaTime);
        }

        if (!wasGrounded && isGrounded && lastAirborneVerticalSpeed <= hardLandingSpeed && hasHardLandingParameter)
        {
            animator.SetTrigger(HardLandingHash);
        }

        wasGrounded = isGrounded;
    }

    public void PlayFootstepSound()
    {
    }

    private bool HasParameter(int parameterHash)
    {
        foreach (AnimatorControllerParameter parameter in animator.parameters)
        {
            if (parameter.nameHash == parameterHash)
            {
                return true;
            }
        }

        return false;
    }
}
