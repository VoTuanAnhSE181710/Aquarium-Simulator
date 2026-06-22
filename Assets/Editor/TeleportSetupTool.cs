using UnityEngine;
using UnityEditor;

public class TeleportSetupTool : EditorWindow
{
    [MenuItem("Tools/Setup Fish Store Teleports")]
    public static void SetupTeleports()
    {
        // 1. Tìm các object chính trong scene
        GameObject outsideStore = GameObject.Find("Fish Store");
        GameObject insideFloor1 = GameObject.Find("FishStore Inside Floor 1");
        GameObject insideFloor2 = GameObject.Find("FishStore Inside Floor 2");

        if (outsideStore == null || insideFloor1 == null)
        {
            EditorUtility.DisplayDialog("Lỗi", "Không tìm thấy 'Fish Store' hoặc 'FishStore Inside Floor 1' trong Scene hiện tại. Hãy mở scene FishSence trước!", "OK");
            return;
        }

        Transform outsideDoor = FindChildRecursive(outsideStore.transform, "Door");
        Transform insideDoor = FindChildRecursive(insideFloor1.transform, "Door (1)");

        int count = 0;

        if (outsideDoor != null && insideDoor != null)
        {
            SetupTeleportPair(outsideDoor.gameObject, insideDoor.gameObject, "Nhấn F để vào cửa hàng", "Nhấn F để đi ra ngoài");
            count += 2;
        }
        else
        {
            Debug.LogWarning($"[TeleportTool] Không tìm thấy cửa! Cửa ngoài: {outsideDoor != null}, Cửa trong: {insideDoor != null}");
        }

        if (insideFloor2 != null)
        {
            Transform ladderFloor1 = FindChildRecursive(insideFloor1.transform, "Ladder");
            Transform ladderFloor2 = FindChildRecursive(insideFloor2.transform, "Ladder");

            if (ladderFloor1 != null && ladderFloor2 != null)
            {
                SetupTeleportPair(ladderFloor1.gameObject, ladderFloor2.gameObject, "Nhấn F để leo lên tầng 2", "Nhấn F để đi xuống tầng 1");
                count += 2;
            }
            else
            {
                Debug.LogWarning($"[TeleportTool] Không tìm thấy thang! Thang tầng 1: {ladderFloor1 != null}, Thang tầng 2: {ladderFloor2 != null}");
            }
        }

        if (count > 0)
        {
            // Đánh dấu scene đã thay đổi để Unity cho phép Save
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(UnityEngine.SceneManagement.SceneManager.GetActiveScene());
            EditorUtility.DisplayDialog("Thành công", $"Đã thiết lập xong {count} cổng dịch chuyển! Hãy di chuyển các GameObject '_SpawnPoint' vừa được tạo ra để chỉnh vị trí hạ cánh đẹp nhất.", "OK");
        }
        else
        {
            EditorUtility.DisplayDialog("Cảnh báo", "Không thiết lập được cổng dịch chuyển nào. Kiểm tra console để xem chi tiết.", "OK");
        }
    }

    private static void SetupTeleportPair(GameObject objA, GameObject objB, string promptAToB, string promptBToA)
    {
        // 1. Tạo điểm đến cho A (nằm gần B) nếu chưa có
        string destAName = objB.name + "_SpawnPoint";
        Transform destA = objB.transform.Find(destAName);
        if (destA == null)
        {
            GameObject destAGo = new GameObject(destAName);
            destA = destAGo.transform;
            destA.SetParent(objB.transform);
            // Đặt lệch ra trước mặt một chút
            destA.localPosition = new Vector3(0, 0.5f, 2.5f);
            destA.localRotation = Quaternion.identity;
        }

        // 2. Tạo điểm đến cho B (nằm gần A) nếu chưa có
        string destBName = objA.name + "_SpawnPoint";
        Transform destB = objA.transform.Find(destBName);
        if (destB == null)
        {
            GameObject destBGo = new GameObject(destBName);
            destB = destBGo.transform;
            destB.SetParent(objA.transform);
            // Đặt lệch ra trước mặt một chút
            destB.localPosition = new Vector3(0, 0.5f, 2.0f);
            destB.localRotation = Quaternion.identity;
        }

        // 3. Gắn script TeleportPortal và cấu hình cho A
        TeleportPortal portalA = objA.GetComponent<TeleportPortal>();
        if (portalA == null) portalA = objA.AddComponent<TeleportPortal>();
        portalA.destination = destA;
        portalA.promptMessage = promptAToB;
        portalA.interactionDistance = 2.5f;

        // 4. Gắn script TeleportPortal và cấu hình cho B
        TeleportPortal portalB = objB.GetComponent<TeleportPortal>();
        if (portalB == null) portalB = objB.AddComponent<TeleportPortal>();
        portalB.destination = destB;
        portalB.promptMessage = promptBToA;
        portalB.interactionDistance = 2.5f;
    }

    private static Transform FindChildRecursive(Transform parent, string name)
    {
        if (parent.name == name) return parent;
        foreach (Transform child in parent)
        {
            Transform found = FindChildRecursive(child, name);
            if (found != null) return found;
        }
        return null;
    }
}
