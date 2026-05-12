using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Tilemaps;
using UnityEngine.U2D;

public static class JitterFixerUtility
{
    private const string ReportPath = "Assets/Documentation/JitterFixReport.md";

    [MenuItem("Tools/Jitter Fix/Run Full Audit")]
    public static void RunFullAudit()
    {
        var report = new JitterFixReport();
        EnsureDocumentationFolder();

        ApplySpriteImportSettings(report);
        ApplyPixelPerfectCameraSettings(report);
        ApplyRigidbodyInterpolation(report);
        ScanScenesAndPrefabs(report);

        SaveReport(report);
        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
    }

    private static void EnsureDocumentationFolder()
    {
        string directory = Path.GetDirectoryName(ReportPath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            Directory.CreateDirectory(directory);
    }

    private static void ApplySpriteImportSettings(JitterFixReport report)
    {
        var spriteEntries = new List<SpriteEntry>();
        string[] textureGuids = AssetDatabase.FindAssets("t:Texture2D");

        foreach (string guid in textureGuids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            if (AssetImporter.GetAtPath(path) is not TextureImporter importer)
                continue;

            if (importer.textureType != TextureImporterType.Sprite)
                continue;

            spriteEntries.Add(new SpriteEntry(path, importer.spritePixelsPerUnit));

            bool changed = false;
            if (importer.filterMode != FilterMode.Point)
            {
                importer.filterMode = FilterMode.Point;
                changed = true;
            }

            if (importer.textureCompression != TextureImporterCompression.Uncompressed)
            {
                importer.textureCompression = TextureImporterCompression.Uncompressed;
                changed = true;
            }

            if (importer.mipmapEnabled)
            {
                importer.mipmapEnabled = false;
                changed = true;
            }

            if (changed)
            {
                importer.SaveAndReimport();
                report.FilesModified.Add($"Sprite import settings: {path}");
            }
        }

        if (spriteEntries.Count == 0)
        {
            report.MostCommonPpu = 32;
            return;
        }

        int commonPpu = spriteEntries
            .GroupBy(entry => Mathf.RoundToInt(entry.PixelsPerUnit))
            .OrderByDescending(group => group.Count())
            .Select(group => group.Key)
            .FirstOrDefault();

        report.MostCommonPpu = commonPpu > 0 ? commonPpu : 32;

        foreach (SpriteEntry entry in spriteEntries)
        {
            if (Mathf.RoundToInt(entry.PixelsPerUnit) != report.MostCommonPpu)
                report.InconsistentPpuAssets.Add($"{entry.Path} (PPU {entry.PixelsPerUnit:0.##})");
        }

        report.Summary.Add("Applied sprite import settings: Point filter, no compression, mip maps disabled.");
    }

    private static void ApplyPixelPerfectCameraSettings(JitterFixReport report)
    {
        SceneSetup[] setup = EditorSceneManager.GetSceneManagerSetup();
        string[] sceneGuids = AssetDatabase.FindAssets("t:Scene");

        foreach (string guid in sceneGuids)
        {
            string scenePath = AssetDatabase.GUIDToAssetPath(guid);
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            bool sceneDirty = false;
            foreach (Camera camera in UnityEngine.Object.FindObjectsByType<Camera>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (!camera.CompareTag("MainCamera"))
                    continue;

                camera.orthographic = true;

                var pixelPerfect = camera.GetComponent<PixelPerfectCamera>();
                if (pixelPerfect == null)
                {
                    pixelPerfect = camera.gameObject.AddComponent<PixelPerfectCamera>();
                    sceneDirty = true;
                }

                pixelPerfect.assetsPPU = report.MostCommonPpu;
                pixelPerfect.refResolutionX = 320;
                pixelPerfect.refResolutionY = 180;
                pixelPerfect.upscaleRT = true;
                pixelPerfect.pixelSnapping = true;
                pixelPerfect.cropFrameX = false;
                pixelPerfect.cropFrameY = false;

                report.CameraSettingsApplied.Add($"{scenePath} -> {camera.name} (PPU {report.MostCommonPpu})");
            }

            if (sceneDirty)
            {
                EditorSceneManager.SaveScene(scene);
                report.FilesModified.Add($"Scene camera update: {scenePath}");
            }
        }

        EditorSceneManager.RestoreSceneManagerSetup(setup);
        report.Summary.Add("Configured Pixel Perfect Camera settings and enforced orthographic projection on Main Camera.");
    }

    private static void ApplyRigidbodyInterpolation(JitterFixReport report)
    {
        foreach (string guid in AssetDatabase.FindAssets("t:Prefab"))
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            GameObject prefab = PrefabUtility.LoadPrefabContents(path);

            bool changed = false;
            foreach (Rigidbody2D body in prefab.GetComponentsInChildren<Rigidbody2D>(true))
            {
                if (body.interpolation != RigidbodyInterpolation2D.Interpolate)
                {
                    body.interpolation = RigidbodyInterpolation2D.Interpolate;
                    changed = true;
                    report.RigidbodyUpdates.Add($"{path} -> {body.name}");
                }
            }

            if (changed)
            {
                PrefabUtility.SaveAsPrefabAsset(prefab, path);
                report.FilesModified.Add($"Prefab rigidbody update: {path}");
            }

            PrefabUtility.UnloadPrefabContents(prefab);
        }
    }

    private static void ScanScenesAndPrefabs(JitterFixReport report)
    {
        SceneSetup[] setup = EditorSceneManager.GetSceneManagerSetup();
        string[] sceneGuids = AssetDatabase.FindAssets("t:Scene");

        foreach (string guid in sceneGuids)
        {
            string scenePath = AssetDatabase.GUIDToAssetPath(guid);
            Scene scene = EditorSceneManager.OpenScene(scenePath, OpenSceneMode.Single);

            foreach (Transform transform in UnityEngine.Object.FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (HasNonIntegerScale(transform.localScale))
                    report.NonIntegerScaleObjects.Add($"{scenePath} -> {GetHierarchyPath(transform)} ({transform.localScale})");
            }

            foreach (Tilemap tilemap in UnityEngine.Object.FindObjectsByType<Tilemap>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (tilemap.tileAnchor != Vector3.zero)
                    report.TilemapIssues.Add($"{scenePath} -> {tilemap.name} tileAnchor {tilemap.tileAnchor}");

                GridLayout grid = tilemap.layoutGrid;
                if (grid != null && grid.cellSize != Vector3.one)
                    report.TilemapIssues.Add($"{scenePath} -> {tilemap.name} cellSize {grid.cellSize}");
            }
        }

        EditorSceneManager.RestoreSceneManagerSetup(setup);
    }

    private static bool HasNonIntegerScale(Vector3 scale)
    {
        return IsNonInteger(scale.x) || IsNonInteger(scale.y) || IsNonInteger(scale.z);
    }

    private static bool IsNonInteger(float value)
    {
        return Mathf.Abs(value - Mathf.Round(value)) > 0.0001f;
    }

    private static string GetHierarchyPath(Transform transform)
    {
        var parts = new Stack<string>();
        while (transform != null)
        {
            parts.Push(transform.name);
            transform = transform.parent;
        }

        return string.Join("/", parts);
    }

    private static void SaveReport(JitterFixReport report)
    {
        var sb = new StringBuilder();
        sb.AppendLine("# Jitter Fix Report");
        sb.AppendLine();
        sb.AppendLine($"Generated: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        sb.AppendLine();
        sb.AppendLine($"Most common PPU: {report.MostCommonPpu}");
        sb.AppendLine();

        AppendList(sb, "Files modified", report.FilesModified);
        AppendList(sb, "Assets with inconsistent PPU", report.InconsistentPpuAssets);
        AppendList(sb, "Objects with non-integer scale", report.NonIntegerScaleObjects);
        AppendList(sb, "Tilemap issues found", report.TilemapIssues);
        AppendList(sb, "Camera settings applied", report.CameraSettingsApplied);
        AppendList(sb, "Rigidbody2D interpolation updates", report.RigidbodyUpdates);

        sb.AppendLine("## Summary of fixes");
        if (report.Summary.Count == 0)
        {
            sb.AppendLine("- No summary entries recorded.");
        }
        else
        {
            foreach (string line in report.Summary)
                sb.AppendLine($"- {line}");
        }

        File.WriteAllText(ReportPath, sb.ToString());
    }

    private static void AppendList(StringBuilder sb, string title, List<string> items)
    {
        sb.AppendLine($"## {title}");
        if (items.Count == 0)
        {
            sb.AppendLine("- None detected.");
        }
        else
        {
            foreach (string item in items)
                sb.AppendLine($"- {item}");
        }

        sb.AppendLine();
    }

    private sealed class JitterFixReport
    {
        public int MostCommonPpu = 32;
        public List<string> FilesModified { get; } = new();
        public List<string> InconsistentPpuAssets { get; } = new();
        public List<string> NonIntegerScaleObjects { get; } = new();
        public List<string> TilemapIssues { get; } = new();
        public List<string> CameraSettingsApplied { get; } = new();
        public List<string> RigidbodyUpdates { get; } = new();
        public List<string> Summary { get; } = new();
    }

    private readonly struct SpriteEntry
    {
        public SpriteEntry(string path, float pixelsPerUnit)
        {
            Path = path;
            PixelsPerUnit = pixelsPerUnit;
        }

        public string Path { get; }
        public float PixelsPerUnit { get; }
    }
}