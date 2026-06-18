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
        "Linh", "Minh", "MissLan", "Tuan", "UncleHung",
        "Ch01_nonPBR", "Ch02_nonPBR", "Ch06_nonPBR", "Ch07_nonPBR",
        "Ch08_nonPBR", "Ch21_nonPBR", "Ch22_nonPBR", "Ch23_nonPBR",
        "Ch26_nonPBR", "Ch27_nonPBR", "Ch28_nonPBR", "Ch31_nonPBR",
        "Ch33_nonPBR", "Ch37_nonPBR", "Ch41_nonPBR", "Ch42_nonPBR", "Remy"
    };

    private static readonly string[] NonBlockingKeywords =
    {
        "grasse", "flower", "leaf", "foliage", "water", "sky",
        "roof", "celling", "ceiling", "windowcolor", "window_color"
    };

    private const string AnimatorControllerPath = "Assets/Animations/CharController.controller";
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
        AnimatorController animatorController = LoadAnimatorController();
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
            else
            {
                // BẮT BUỘC: Báo cho Unity biết ta sắp sửa một Component đã tồn tại
                Undo.RecordObject(controller, "Setup Character Controller Properties");
            }

            float inverseScale = 1f / Mathf.Max(character.transform.lossyScale.y, 0.0001f);
            
            // Tính toán height trước
            float calculatedHeight = 1.8f * inverseScale;
            
            controller.height = calculatedHeight;
            controller.radius = 0.35f * inverseScale;
            controller.center = new Vector3(0f, 0.9f * inverseScale, 0f);
            controller.skinWidth = 0.08f * inverseScale;

            // Kiểm tra quy tắc ngầm của Unity
            if (0.315f > calculatedHeight)
            {
                Debug.LogWarning($"[PhysicsSetup] Nhân vật {characterName} có Height ({calculatedHeight:F3}) nhỏ hơn StepOffset (0.315). Unity sẽ tự động clamp StepOffset xuống bằng Height!");
            }

            // Gán Step Offset
            controller.stepOffset = 0.315f;
            controller.slopeLimit = 50f;

            // BẮT BUỘC: Ép Unity lưu dữ liệu vừa sửa (Hỗ trợ cả Prefab)
            EditorUtility.SetDirty(controller);
            PrefabUtility.RecordPrefabInstancePropertyModifications(controller);

            if (EnsureComponent<CharacterGroundProbe>(character) != null) count++;

            Animator animator = character.GetComponent<Animator>();
            if (animator == null)
            {
                animator = Undo.AddComponent<Animator>(character);
                count++;
            }

            if (animatorController != null)
            {
                Undo.RecordObject(animator, "Setup Animator");
                animator.runtimeAnimatorController = animatorController;
                animator.applyRootMotion = false;
                animator.updateMode = AnimatorUpdateMode.Normal;
                EditorUtility.SetDirty(animator);
                PrefabUtility.RecordPrefabInstancePropertyModifications(animator);
            }

            if (EnsureComponent<CharacterAnimatorDriver>(character) != null) count++;
            if (EnsureComponent<HumanoidFootGroundingIK>(character) != null) count++;

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
        T existingComponent = gameObject.GetComponent<T>();
        if (existingComponent != null)
        {
            return null;
        }

        return Undo.AddComponent<T>(gameObject);
    }

    private static AnimatorController LoadAnimatorController()
    {
        AnimatorController controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(AnimatorControllerPath);
        if (controller == null)
        {
            Debug.LogWarning($"Animator Controller not found at '{AnimatorControllerPath}'. Character animations will not work.");
            return null;
        }

        EnsureIkPass(controller);
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
        if (layers.Length == 0) return;

        layers[0].iKPass = true;
        controller.layers = layers;

        AnimatorStateMachine stateMachine = layers[0].stateMachine;
        foreach (ChildAnimatorState childState in stateMachine.states)
        {
            childState.state.iKOnFeet = true;
        }
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
        if (houseLine == null) return 0;

        int count = 0;
        MeshCollider[] colliders = houseLine.GetComponentsInChildren<MeshCollider>(true);

        foreach (MeshCollider collider in colliders)
        {
            if (ShouldBlockCharacter(collider.gameObject)) continue;

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