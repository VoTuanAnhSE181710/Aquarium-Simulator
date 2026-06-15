using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class PhysicsSceneSetup
{
    private static readonly string[] CharacterNames =
    {
        "Linh",
        "Minh",
        "MissLan",
        "Tuan",
        "UncleHung"
    };

    private static readonly string[] NonBlockingKeywords =
    {
        "grasse",
        "flower",
        "leaf",
        "foliage",
        "water",
        "sky",
        "roof",
        "celling",
        "ceiling",
        "windowcolor",
        "window_color"
    };

    private const string AnimatorControllerPath = "Assets/Animations/AquariumCharacter.controller";
    private const string IdleClipPath = "Assets/Animations/Reference/Idle.anim";
    private const string WalkingClipPath = "Assets/Animations/Reference/Walking.anim";

    [MenuItem("Tools/Aquarium Simulator/Setup Scene Physics")]
    public static void SetupScenePhysics()
    {
        SetupActiveScenePhysics();
    }

    public static void SetupBuildScenesPhysicsBatch()
    {
        foreach (EditorBuildSettingsScene buildScene in EditorBuildSettings.scenes)
        {
            if (!buildScene.enabled || string.IsNullOrEmpty(buildScene.path))
            {
                continue;
            }

            EditorSceneManager.OpenScene(buildScene.path, OpenSceneMode.Single);
            SetupActiveScenePhysics();
        }
    }

    private static void SetupActiveScenePhysics()
    {
        int characterControllerCount = SetupCharacters();
        int removedColliderCount = RemoveUnnecessaryHouseColliders();
        int colliderCount = SetupEnvironmentColliders();

        EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        EditorSceneManager.SaveOpenScenes();
        Debug.Log($"Physics setup complete. Character setup operations: {characterControllerCount}, removed colliders: {removedColliderCount}, environment colliders: {colliderCount}.");
    }

    private static int SetupCharacters()
    {
        int count = 0;
        AnimatorController animatorController = SetupAnimatorController();
        foreach (string characterName in CharacterNames)
        {
            GameObject character = GameObject.Find(characterName);
            if (character == null)
            {
                continue;
            }

            CharacterController controller = character.GetComponent<CharacterController>();
            if (controller == null)
            {
                controller = Undo.AddComponent<CharacterController>(character);
                count++;
            }

            float inverseScale = 1f / Mathf.Max(character.transform.lossyScale.y, 0.0001f);
            controller.height = 1.8f * inverseScale;
            controller.radius = 0.35f * inverseScale;
            controller.center = new Vector3(0f, 0.9f * inverseScale, 0f);
            controller.stepOffset = 0.3f * inverseScale;
            controller.skinWidth = 0.08f * inverseScale;
            controller.slopeLimit = 50f;

            if (EnsureComponent<CharacterGroundProbe>(character) != null)
            {
                count++;
            }

            Animator animator = character.GetComponent<Animator>();
            if (animator == null)
            {
                animator = Undo.AddComponent<Animator>(character);
                count++;
            }

            if (animatorController != null)
            {
                animator.runtimeAnimatorController = animatorController;
                animator.applyRootMotion = false;
                animator.updateMode = AnimatorUpdateMode.Normal;
            }

            if (EnsureComponent<CharacterAnimatorDriver>(character) != null)
            {
                count++;
            }

            if (EnsureComponent<HumanoidFootGroundingIK>(character) != null)
            {
                count++;
            }

            if (!string.Equals(characterName, "Minh", StringComparison.OrdinalIgnoreCase) &&
                character.GetComponent<NpcWander>() == null)
            {
                Undo.AddComponent<NpcWander>(character);
                count++;
            }
        }

        return count;
    }

    private static T EnsureComponent<T>(GameObject gameObject) where T : Component
    {
        if (gameObject.GetComponent<T>() != null)
        {
            return null;
        }

        return Undo.AddComponent<T>(gameObject);
    }

    private static AnimatorController SetupAnimatorController()
    {
        AnimationClip idle = AssetDatabase.LoadAssetAtPath<AnimationClip>(IdleClipPath);
        AnimationClip walking = AssetDatabase.LoadAssetAtPath<AnimationClip>(WalkingClipPath);
        if (idle == null || walking == null)
        {
            Debug.LogWarning("Idle or Walking animation clip is missing. Animator Controller setup was skipped.");
            return null;
        }

        StripTranslationAndScaleCurves(idle);
        StripTranslationAndScaleCurves(walking);

        AnimatorController existingController = AssetDatabase.LoadAssetAtPath<AnimatorController>(AnimatorControllerPath);
        if (existingController != null)
        {
            EnsureAnimatorParameter(existingController, "Speed", AnimatorControllerParameterType.Float);
            EnsureAnimatorParameter(existingController, "Grounded", AnimatorControllerParameterType.Bool);
            EnsureAnimatorParameter(existingController, "Airborne", AnimatorControllerParameterType.Bool);
            EnsureAnimatorParameter(existingController, "VerticalSpeed", AnimatorControllerParameterType.Float);
            EnsureAnimatorParameter(existingController, "HardLanding", AnimatorControllerParameterType.Trigger);
            EnsureIkPass(existingController);
            EditorUtility.SetDirty(existingController);
            AssetDatabase.SaveAssets();
            return existingController;
        }

        AnimatorController controller = AnimatorController.CreateAnimatorControllerAtPath(AnimatorControllerPath);
        controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
        controller.AddParameter("Grounded", AnimatorControllerParameterType.Bool);
        controller.AddParameter("Airborne", AnimatorControllerParameterType.Bool);
        controller.AddParameter("VerticalSpeed", AnimatorControllerParameterType.Float);
        controller.AddParameter("HardLanding", AnimatorControllerParameterType.Trigger);

        AnimatorStateMachine stateMachine = controller.layers[0].stateMachine;
        AnimatorState locomotion = stateMachine.AddState("Locomotion");
        locomotion.iKOnFeet = true;
        stateMachine.defaultState = locomotion;

        BlendTree blendTree = new()
        {
            name = "Idle Walk Blend",
            blendType = BlendTreeType.Simple1D,
            blendParameter = "Speed",
            useAutomaticThresholds = false
        };

        AssetDatabase.AddObjectToAsset(blendTree, controller);
        blendTree.AddChild(idle, 0f);
        blendTree.AddChild(walking, 1f);
        locomotion.motion = blendTree;
        EnsureIkPass(controller);
        EditorUtility.SetDirty(controller);
        AssetDatabase.SaveAssets();
        return controller;
    }

    private static void EnsureAnimatorParameter(
        AnimatorController controller,
        string parameterName,
        AnimatorControllerParameterType parameterType)
    {
        if (controller.parameters.Any(parameter => parameter.name == parameterName))
        {
            return;
        }

        controller.AddParameter(parameterName, parameterType);
    }

    private static void EnsureIkPass(AnimatorController controller)
    {
        AnimatorControllerLayer[] layers = controller.layers;
        if (layers.Length == 0)
        {
            return;
        }

        layers[0].iKPass = true;
        controller.layers = layers;

        AnimatorStateMachine stateMachine = layers[0].stateMachine;
        foreach (ChildAnimatorState childState in stateMachine.states)
        {
            childState.state.iKOnFeet = true;
        }
    }

    private static void StripTranslationAndScaleCurves(AnimationClip clip)
    {
        EditorCurveBinding[] bindings = AnimationUtility.GetCurveBindings(clip);

        foreach (EditorCurveBinding binding in bindings)
        {
            if (binding.propertyName.StartsWith("m_LocalPosition", StringComparison.Ordinal) ||
                binding.propertyName.StartsWith("m_LocalScale", StringComparison.Ordinal))
            {
                AnimationUtility.SetEditorCurve(clip, binding, null);
            }
        }

        EditorUtility.SetDirty(clip);
    }

    private static int SetupEnvironmentColliders()
    {
        int count = 0;
        MeshFilter[] meshFilters = UnityEngine.Object.FindObjectsByType<MeshFilter>(
            FindObjectsInactive.Include,
            FindObjectsSortMode.None);

        foreach (MeshFilter meshFilter in meshFilters)
        {
            GameObject gameObject = meshFilter.gameObject;
            if (meshFilter.sharedMesh == null ||
                gameObject.GetComponent<Collider>() != null ||
                IsPartOfCharacter(gameObject) ||
                !ShouldBlockCharacter(gameObject))
            {
                continue;
            }

            MeshCollider collider = Undo.AddComponent<MeshCollider>(gameObject);
            collider.sharedMesh = meshFilter.sharedMesh;
            count++;
        }

        return count;
    }

    private static bool IsPartOfCharacter(GameObject gameObject)
    {
        Transform current = gameObject.transform;

        while (current != null)
        {
            if (CharacterNames.Contains(current.name, StringComparer.OrdinalIgnoreCase))
            {
                return true;
            }

            current = current.parent;
        }

        return false;
    }

    private static bool ShouldBlockCharacter(GameObject gameObject)
    {
        string hierarchyPath = GetHierarchyPath(gameObject.transform).ToLowerInvariant();
        return !NonBlockingKeywords.Any(hierarchyPath.Contains);
    }

    private static int RemoveUnnecessaryHouseColliders()
    {
        GameObject houseLine = GameObject.Find("HouseLine");
        if (houseLine == null)
        {
            return 0;
        }

        int count = 0;
        MeshCollider[] colliders = houseLine.GetComponentsInChildren<MeshCollider>(true);

        foreach (MeshCollider collider in colliders)
        {
            if (ShouldBlockCharacter(collider.gameObject))
            {
                continue;
            }

            Undo.DestroyObjectImmediate(collider);
            count++;
        }

        return count;
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
