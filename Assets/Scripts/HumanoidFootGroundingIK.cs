using UnityEngine;

[RequireComponent(typeof(Animator))]
public sealed class HumanoidFootGroundingIK : MonoBehaviour
{
    [SerializeField] private LayerMask groundMask = ~0;
    [SerializeField] private float rayHeight = 0.45f;
    [SerializeField] private float rayDistance = 1.1f;
    [SerializeField] private float footOffset = 0.035f;
    [SerializeField] private float maxPelvisAdjustment = 0.18f;
    [SerializeField] private float positionWeight = 0.85f;
    [SerializeField] private float rotationWeight = 0.65f;
    [SerializeField] private float ikSharpness = 20f;
    [SerializeField] private float pelvisSharpness = 14f;

    private Animator animator;
    private CharacterGroundProbe groundProbe;
    private FootTarget leftFoot;
    private FootTarget rightFoot;
    private float pelvisOffset;

    private struct FootTarget
    {
        public bool hasHit;
        public Vector3 position;
        public Quaternion rotation;
        public float heightDelta;
    }

    private void Awake()
    {
        animator = GetComponent<Animator>();
        groundProbe = GetComponent<CharacterGroundProbe>();
    }

    private void OnAnimatorIK(int layerIndex)
    {
        if (animator == null || !animator.isHuman)
        {
            return;
        }

        float groundedWeight = groundProbe == null || groundProbe.IsGrounded ? 1f : 0f;
        UpdateFoot(AvatarIKGoal.LeftFoot, ref leftFoot, groundedWeight);
        UpdateFoot(AvatarIKGoal.RightFoot, ref rightFoot, groundedWeight);
        ApplyPelvisOffset(groundedWeight);
        ApplyFoot(AvatarIKGoal.LeftFoot, leftFoot, groundedWeight);
        ApplyFoot(AvatarIKGoal.RightFoot, rightFoot, groundedWeight);
    }

    private void UpdateFoot(AvatarIKGoal goal, ref FootTarget target, float groundedWeight)
    {
        Transform foot = animator.GetBoneTransform(
            goal == AvatarIKGoal.LeftFoot ? HumanBodyBones.LeftFoot : HumanBodyBones.RightFoot);

        if (foot == null || groundedWeight <= 0f)
        {
            target.hasHit = false;
            return;
        }

        Vector3 origin = foot.position + Vector3.up * rayHeight;
        bool hasHit = Physics.Raycast(
            origin,
            Vector3.down,
            out RaycastHit hit,
            rayDistance,
            groundMask,
            QueryTriggerInteraction.Ignore);

        target.hasHit = hasHit;
        if (!hasHit)
        {
            target.position = foot.position;
            target.rotation = foot.rotation;
            target.heightDelta = 0f;
            return;
        }

        Vector3 desiredPosition = hit.point + hit.normal * footOffset;
        Vector3 forward = Vector3.ProjectOnPlane(transform.forward, hit.normal);
        Quaternion desiredRotation = forward.sqrMagnitude > 0.0001f
            ? Quaternion.LookRotation(forward.normalized, hit.normal)
            : foot.rotation;
        float blend = 1f - Mathf.Exp(-ikSharpness * Time.deltaTime);

        target.position = Vector3.Lerp(target.position == Vector3.zero ? foot.position : target.position, desiredPosition, blend);
        target.rotation = Quaternion.Slerp(target.rotation == Quaternion.identity ? foot.rotation : target.rotation, desiredRotation, blend);
        target.heightDelta = desiredPosition.y - foot.position.y;
    }

    private void ApplyPelvisOffset(float groundedWeight)
    {
        float desiredOffset = 0f;
        if (leftFoot.hasHit && rightFoot.hasHit)
        {
            desiredOffset = Mathf.Min(leftFoot.heightDelta, rightFoot.heightDelta);
        }
        else if (leftFoot.hasHit)
        {
            desiredOffset = leftFoot.heightDelta;
        }
        else if (rightFoot.hasHit)
        {
            desiredOffset = rightFoot.heightDelta;
        }

        desiredOffset = Mathf.Clamp(desiredOffset, -maxPelvisAdjustment, maxPelvisAdjustment) * groundedWeight;
        pelvisOffset = Mathf.Lerp(
            pelvisOffset,
            desiredOffset,
            1f - Mathf.Exp(-pelvisSharpness * Time.deltaTime));

        animator.bodyPosition += Vector3.up * pelvisOffset;
    }

    private void ApplyFoot(AvatarIKGoal goal, FootTarget target, float groundedWeight)
    {
        float targetPositionWeight = target.hasHit ? positionWeight * groundedWeight : 0f;
        float targetRotationWeight = target.hasHit ? rotationWeight * groundedWeight : 0f;

        animator.SetIKPositionWeight(goal, targetPositionWeight);
        animator.SetIKRotationWeight(goal, targetRotationWeight);

        if (target.hasHit)
        {
            animator.SetIKPosition(goal, target.position);
            animator.SetIKRotation(goal, target.rotation);
        }
    }
}
