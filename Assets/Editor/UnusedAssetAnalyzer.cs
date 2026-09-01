using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;

/// <summary>
/// 未使用资源分析器。
///
/// 用 AssetDatabase 的真实依赖图判断哪些资源没有被任何"根"引用，
/// 用于安全地删除第三方美术包中未使用的部分。
///
/// 根的定义（这些一定会进包，必须视为已使用）：
///   - 所有场景
///   - Assets/Resources/ 下的全部内容（Resources 无条件进包，不参与依赖裁剪）
///   - 非第三方目录下的 Prefab 与 ScriptableObject
///
/// 只做分析与报告，不删除任何文件。
/// </summary>
public static class UnusedAssetAnalyzer
{
    /// <summary>被分析的目标目录（判断其中哪些资源未被使用）。</summary>
    private static readonly string[] TargetFolders =
    {
        "Assets/ThirdParty",
        "Assets/Rowlan",
        "Assets/Fonts",
        "Assets/Art",
        "Assets/Audio",
    };

    /// <summary>不参与"未使用"判定的目录（它们本身就是根，或由 Unity 特殊处理）。</summary>
    private static readonly string[] RootFolders =
    {
        "Assets/Resources",
        "Assets/Scenes",
        "Assets/Prefabs",
        "Assets/Skills",
        "Assets/Items",
        "Assets/ScriptableObjects",
        "Assets/Settings",
    };

    [MenuItem("Tools/资源分析/分析未使用的资源")]
    public static void Analyze()
    {
        var roots = CollectRoots();
        Debug.Log($"[UnusedAssetAnalyzer] 收集到 {roots.Count} 个根资源，开始计算依赖图…");

        // GetDependencies 递归展开：场景 -> prefab -> 材质 -> 贴图 / Animator -> 动画片段
        var used = new HashSet<string>(
            AssetDatabase.GetDependencies(roots.ToArray(), true),
            StringComparer.OrdinalIgnoreCase);

        Debug.Log($"[UnusedAssetAnalyzer] 依赖图包含 {used.Count} 个资源。");

        var sb = new StringBuilder();
        sb.AppendLine("# 未使用资源分析报告");
        sb.AppendLine();
        sb.AppendLine($"生成时间：{DateTime.Now:yyyy-MM-dd HH:mm}");
        sb.AppendLine($"根资源数：{roots.Count}　依赖图资源数：{used.Count}");
        sb.AppendLine();
        sb.AppendLine("> 注意：本报告只统计资源引用关系。");
        sb.AppendLine("> 通过 Resources.Load / Addressables / 字符串路径动态加载的资源无法被检测，");
        sb.AppendLine("> 删除前请再确认一次。");
        sb.AppendLine();

        long grandUsed = 0, grandUnused = 0;

        foreach (var folder in TargetFolders)
        {
            if (!Directory.Exists(folder)) continue;

            var guids = AssetDatabase.FindAssets("", new[] { folder });
            var paths = guids
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(p => !string.IsNullOrEmpty(p) && !AssetDatabase.IsValidFolder(p))
                .Distinct()
                .ToList();

            // 按第二层目录分组，方便按整包判断能否删除
            var groups = paths
                .GroupBy(p => TopLevelGroup(p, folder))
                .OrderBy(g => g.Key, StringComparer.OrdinalIgnoreCase);

            sb.AppendLine($"## {folder}");
            sb.AppendLine();
            sb.AppendLine("| 子目录 | 已使用 | 未使用 | 未使用体积 | 可否整包删除 |");
            sb.AppendLine("|---|---:|---:|---:|---|");

            var unusedDetail = new List<string>();

            foreach (var g in groups)
            {
                int usedCount = 0, unusedCount = 0;
                long usedSize = 0, unusedSize = 0;

                foreach (var p in g)
                {
                    long size = FileSize(p);
                    if (used.Contains(p)) { usedCount++;   usedSize   += size; }
                    else                  { unusedCount++; unusedSize += size; unusedDetail.Add(p); }
                }

                grandUsed   += usedSize;
                grandUnused += unusedSize;

                string verdict = usedCount == 0 ? "**可整包删除**" : "部分使用，逐个确认";
                sb.AppendLine($"| {g.Key} | {usedCount} | {unusedCount} | {Human(unusedSize)} | {verdict} |");
            }

            sb.AppendLine();

            if (unusedDetail.Count > 0)
            {
                sb.AppendLine($"<details><summary>未使用文件清单（{unusedDetail.Count} 个）</summary>");
                sb.AppendLine();
                sb.AppendLine("```");
                foreach (var p in unusedDetail.OrderBy(x => x, StringComparer.OrdinalIgnoreCase))
                    sb.AppendLine(p);
                sb.AppendLine("```");
                sb.AppendLine();
                sb.AppendLine("</details>");
                sb.AppendLine();
            }
        }

        sb.AppendLine("---");
        sb.AppendLine();
        sb.AppendLine($"**已使用合计：{Human(grandUsed)}　未使用合计：{Human(grandUnused)}**");

        const string outPath = "ProjectDocs/UNUSED_ASSETS_REPORT.md";
        Directory.CreateDirectory(Path.GetDirectoryName(outPath));
        File.WriteAllText(outPath, sb.ToString(), new UTF8Encoding(true));

        Debug.Log($"[UnusedAssetAnalyzer] 报告已写入 {outPath}\n" +
                  $"已使用 {Human(grandUsed)} / 未使用 {Human(grandUnused)}");

        EditorUtility.RevealInFinder(outPath);
    }

    private static List<string> CollectRoots()
    {
        var roots = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // 全部场景
        foreach (var guid in AssetDatabase.FindAssets("t:Scene", new[] { "Assets" }))
        {
            var p = AssetDatabase.GUIDToAssetPath(guid);
            // _Recovery 是崩溃备份，不作为根
            if (p.StartsWith("Assets/_Recovery", StringComparison.OrdinalIgnoreCase)) continue;
            roots.Add(p);
        }

        // 根目录下的全部资源
        foreach (var folder in RootFolders)
        {
            if (!Directory.Exists(folder)) continue;
            foreach (var guid in AssetDatabase.FindAssets("", new[] { folder }))
            {
                var p = AssetDatabase.GUIDToAssetPath(guid);
                if (!string.IsNullOrEmpty(p) && !AssetDatabase.IsValidFolder(p)) roots.Add(p);
            }
        }

        return roots.ToList();
    }

    /// <summary>取 folder 之下的第一层子目录名，用于分组。</summary>
    private static string TopLevelGroup(string assetPath, string folder)
    {
        var rest = assetPath.Substring(folder.Length).TrimStart('/');
        int slash = rest.IndexOf('/');
        return slash < 0 ? "(根目录文件)" : rest.Substring(0, slash);
    }

    private static long FileSize(string assetPath)
    {
        try { return new FileInfo(assetPath).Length; }
        catch { return 0; }
    }

    private static string Human(long bytes)
    {
        if (bytes >= 1024L * 1024 * 1024) return $"{bytes / 1024.0 / 1024 / 1024:F2} GB";
        if (bytes >= 1024L * 1024)        return $"{bytes / 1024.0 / 1024:F1} MB";
        if (bytes >= 1024)                return $"{bytes / 1024.0:F0} KB";
        return $"{bytes} B";
    }
}
