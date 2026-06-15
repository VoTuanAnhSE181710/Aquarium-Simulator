#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

public static class AssetUsageReporter
{
    private static readonly string[] KeepExtensions =
    {
        ".cs", ".asmdef", ".asmref", ".dll"
    };

    [MenuItem("Tools/Asset Cleanup/Generate Used-Unused Report")]
    public static void GenerateReport()
    {
        var rootAssets = new List<string>();

        // 1. Lấy scene đang bật trong Build Settings
        foreach (var scene in EditorBuildSettings.scenes)
        {
            if (scene.enabled && File.Exists(scene.path))
            {
                rootAssets.Add(scene.path);
            }
        }

        // 2. Lấy thêm asset đang được chọn trong Project window
        // Dùng khi bạn có prefab/menu scene/addressable root muốn bảo vệ
        foreach (var obj in Selection.objects)
        {
            var path = AssetDatabase.GetAssetPath(obj);
            if (!string.IsNullOrEmpty(path) && path.StartsWith("Assets/"))
            {
                rootAssets.Add(path);
            }
        }

        if (rootAssets.Count == 0)
        {
            Debug.LogWarning("Không có Scene trong Build Settings hoặc asset nào được chọn.");
            return;
        }

        // 3. Lấy toàn bộ dependency từ scene/prefab/root asset
        var usedAssets = new HashSet<string>();

        foreach (var dep in AssetDatabase.GetDependencies(rootAssets.ToArray(), true))
        {
            if (dep.StartsWith("Assets/"))
            {
                usedAssets.Add(dep);
            }
        }

        // 4. Lấy toàn bộ file asset trong project
        var allAssets = AssetDatabase.GetAllAssetPaths()
            .Where(p => p.StartsWith("Assets/"))
            .Where(File.Exists)
            .Where(p => !p.EndsWith(".meta"))
            .ToList();

        bool IsProtected(string path)
        {
            var ext = Path.GetExtension(path).ToLowerInvariant();

            // Không tự động coi script/plugin là unused vì có thể được code gọi gián tiếp
            if (KeepExtensions.Contains(ext)) return true;

            // Resources có thể được load bằng Resources.Load("path")
            if (path.StartsWith("Assets/Resources/") || path.Contains("/Resources/")) return true;

            // StreamingAssets được copy nguyên vào build
            if (path.StartsWith("Assets/StreamingAssets/")) return true;

            // Editor tool thường không ảnh hưởng build nhiều, xóa nhầm dễ hỏng workflow
            if (path.Contains("/Editor/")) return true;

            return false;
        }

        var unusedCandidates = allAssets
            .Where(p => !usedAssets.Contains(p))
            .Where(p => !IsProtected(p))
            .OrderBy(p => p)
            .ToList();

        var sb = new StringBuilder();

        sb.AppendLine("=== ROOT ASSETS ===");
        foreach (var p in rootAssets.Distinct().OrderBy(p => p))
            sb.AppendLine(p);

        sb.AppendLine();
        sb.AppendLine("=== USED ASSETS ===");
        foreach (var p in usedAssets.OrderBy(p => p))
            sb.AppendLine(p);

        sb.AppendLine();
        sb.AppendLine("=== UNUSED CANDIDATES - REVIEW BEFORE DELETE ===");
        foreach (var p in unusedCandidates)
            sb.AppendLine(p);

        var reportPath = "Assets/AssetCleanupReport.txt";
        File.WriteAllText(reportPath, sb.ToString());
        AssetDatabase.Refresh();

        Debug.Log($"Asset cleanup report created: {reportPath}. Unused candidates: {unusedCandidates.Count}");
    }
}
#endif