using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(CharacterController))]
public sealed class ProceduralCharacterMotion : MonoBehaviour
{
    [SerializeField] private float strideFrequency = 1.35f;
    [SerializeField] private float runStrideMultiplier = 1.45f;
    [SerializeField] private float walkSpeedReference = 1.4f;
    [SerializeField] private float runSpeedReference = 6.5f;
    [SerializeField] private float legSwingAngle = 20f;
    [SerializeField] private float armSwingForwardAmount = 0.22f;
    [SerializeField] private float armOutwardAmount = 0.08f;
    [SerializeField] private float kneeBendAngle = 12f;
    [SerializeField] private float breathingAmount = 0.8f;
    [SerializeField] private float blendSmoothTime = 0.14f;
    [SerializeField] private float speedSmoothTime = 0.1f;
    [SerializeField] private float jointSmoothSpeed = 10f;

    private readonly Dictionary<Transform, Quaternion> initialRotations = new();
    private CharacterController characterController;
    private Transform hips;
    private Transform spine;
    private Transform leftUpLeg;
    private Transform rightUpLeg;
    private Transform leftLeg;
    private Transform rightLeg;
    private Transform leftArm;
    private Transform rightArm;
    private Transform leftForeArm;
    private Transform rightForeArm;
    private float walkBlend;
    private float walkBlendVelocity;
    private float smoothedHorizontalSpeed;
    private float speedVelocity;
    private float cycle;
    private Vector3 leftArmDirection;
    private Vector3 rightArmDirection;
    private Vector3 leftForeArmDirection;
    private Vector3 rightForeArmDirection;

    private void Start()
    {
        Animator animator = GetComponent<Animator>();
        if (animator != null && animator.runtimeAnimatorController != null)
        {
            enabled = false;
            return;
        }

        characterController = GetComponent<CharacterController>();
        hips = FindBone("mixamorig:Hips");
        spine = FindBone("mixamorig:Spine");
        leftUpLeg = FindBone("mixamorig:LeftUpLeg");
        rightUpLeg = FindBone("mixamorig:RightUpLeg");
        leftLeg = FindBone("mixamorig:LeftLeg");
        rightLeg = FindBone("mixamorig:RightLeg");
        leftArm = FindBone("mixamorig:LeftArm");
        rightArm = FindBone("mixamorig:RightArm");
        leftForeArm = FindBone("mixamorig:LeftForeArm");
        rightForeArm = FindBone("mixamorig:RightForeArm");

        RememberPose(
            hips,
            spine,
            leftUpLeg,
            rightUpLeg,
            leftLeg,
            rightLeg,
            leftArm,
            rightArm,
            leftForeArm,
            rightForeArm);

        leftArmDirection = GetArmDirection(0f, -1f, 1f);
        rightArmDirection = GetArmDirection(0f, 1f, 1f);
        leftForeArmDirection = GetArmDirection(0f, -1f, 0.5f);
        rightForeArmDirection = GetArmDirection(0f, 1f, 0.5f);
    }

    private void LateUpdate()
    {
        float horizontalSpeed = Vector3.ProjectOnPlane(characterController.velocity, Vector3.up).magnitude;
        smoothedHorizontalSpeed = Mathf.SmoothDamp(
            smoothedHorizontalSpeed,
            horizontalSpeed,
            ref speedVelocity,
            speedSmoothTime);

        float targetBlend = Mathf.Clamp01(smoothedHorizontalSpeed / Mathf.Max(walkSpeedReference, 0.01f));
        walkBlend = Mathf.SmoothDamp(
            walkBlend,
            targetBlend,
            ref walkBlendVelocity,
            blendSmoothTime);

        float runBlend = Mathf.InverseLerp(walkSpeedReference, runSpeedReference, smoothedHorizontalSpeed);
        float cadence = strideFrequency * Mathf.Lerp(1f, runStrideMultiplier, runBlend);
        cycle += smoothedHorizontalSpeed * cadence * Time.deltaTime;

        float stride = Mathf.Sin(cycle) * walkBlend;
        float alternateStride = Mathf.Sin(cycle + Mathf.PI) * walkBlend;
        float breathing = Mathf.Sin(Time.time * 1.7f) * breathingAmount;

        ApplyRotation(leftUpLeg, legSwingAngle * stride, 0f, 0f);
        ApplyRotation(rightUpLeg, legSwingAngle * alternateStride, 0f, 0f);
        ApplyRotation(leftLeg, Mathf.Max(0f, -stride) * kneeBendAngle, 0f, 0f);
        ApplyRotation(rightLeg, Mathf.Max(0f, -alternateStride) * kneeBendAngle, 0f, 0f);
        ApplyArmRotation(
            leftArm,
            leftForeArm,
            alternateStride,
            -1f,
            ref leftArmDirection,
            ref leftForeArmDirection);
        ApplyArmRotation(
            rightArm,
            rightForeArm,
            stride,
            1f,
            ref rightArmDirection,
            ref rightForeArmDirection);
        ApplyRotation(spine, breathing, Mathf.Sin(cycle * 0.5f) * 1.5f * walkBlend, 0f);
    }

    private void OnDisable()
    {
        foreach (KeyValuePair<Transform, Quaternion> pair in initialRotations)
        {
            if (pair.Key != null)
            {
                pair.Key.localRotation = pair.Value;
            }
        }

    }

    private Transform FindBone(string boneName)
    {
        foreach (Transform child in GetComponentsInChildren<Transform>(true))
        {
            if (child.name == boneName)
            {
                return child;
            }
        }

        return null;
    }

    private void RememberPose(params Transform[] bones)
    {
        foreach (Transform bone in bones)
        {
            if (bone != null)
            {
                initialRotations[bone] = bone.localRotation;
            }
        }
    }

    private void ApplyRotation(Transform bone, float x, float y, float z)
    {
        if (bone != null && initialRotations.TryGetValue(bone, out Quaternion initialRotation))
        {
            Quaternion targetRotation = initialRotation * Quaternion.Euler(x, y, z);
            bone.localRotation = Quaternion.Slerp(bone.localRotation, targetRotation, GetJointBlend());
        }
    }

    private void ApplyArmRotation(
        Transform arm,
        Transform foreArm,
        float stride,
        float side,
        ref Vector3 armDirection,
        ref Vector3 foreArmDirection)
    {
        float blend = GetJointBlend();
        Vector3 desiredArmDirection = GetArmDirection(stride, side, 1f);
        Vector3 desiredForeArmDirection = GetArmDirection(stride, side, 0.5f);
        armDirection = Vector3.Slerp(armDirection, desiredArmDirection, blend).normalized;
        foreArmDirection = Vector3.Slerp(foreArmDirection, desiredForeArmDirection, blend).normalized;

        ApplyBoneDirection(arm, armDirection);
        ApplyBoneDirection(foreArm, foreArmDirection);
    }

    private Vector3 GetArmDirection(float stride, float side, float amountMultiplier)
    {
        return (
            Vector3.down +
            transform.right * side * armOutwardAmount * amountMultiplier +
            transform.forward * stride * armSwingForwardAmount * amountMultiplier).normalized;
    }

    private void ApplyBoneDirection(Transform bone, Vector3 desiredWorldDirection)
    {
        if (bone == null ||
            bone.childCount == 0 ||
            !initialRotations.TryGetValue(bone, out Quaternion initialRotation))
        {
            return;
        }

        Vector3 boneDirection = initialRotation * bone.GetChild(0).localPosition.normalized;
        Vector3 desiredDirection =
            bone.parent.InverseTransformDirection(desiredWorldDirection.normalized);
        Quaternion targetRotation =
            Quaternion.FromToRotation(boneDirection, desiredDirection) * initialRotation;

        bone.localRotation = Quaternion.Slerp(bone.localRotation, targetRotation, GetJointBlend());
    }

    private float GetJointBlend()
    {
        return 1f - Mathf.Exp(-jointSmoothSpeed * Time.deltaTime);
    }
}
