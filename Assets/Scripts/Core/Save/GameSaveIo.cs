using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public static class GameSaveIo
{
    public const string LegacyFileName = "svsim_save.json";

    public const string SavesSubfolderName = "SVSimSaveSlots";

    /// <summary>Dùng build cũ (save_*.json); enumerate vẫn lấy mọi *.json trong slots.</summary>
    public const string SlotFilePrefix = "save_";

    static string RootDir => Application.persistentDataPath;

    public static string LegacyFullPath => Path.Combine(RootDir, LegacyFileName);

    public static string SavesSlotsDirectoryPath => Path.Combine(RootDir, SavesSubfolderName);

    /// <summary>Single-file saves from older builds.</summary>
    public static bool LegacySaveExists()
    {
        try
        {
            return File.Exists(LegacyFullPath);
        }
        catch
        {
            return false;
        }
    }

    public static bool TryReadLegacy(out GameSaveFile data) => TryReadPath(LegacyFullPath, out data);

    /// <summary>True when any legacy or slot JSON exists.</summary>
    public static bool AnySaveExists()
    {
        if (LegacySaveExists()) return true;

        try
        {
            if (!Directory.Exists(SavesSlotsDirectoryPath))
            {
                return false;
            }

            string[] hits = Directory.GetFiles(SavesSlotsDirectoryPath, "*.json", SearchOption.TopDirectoryOnly);
            return hits.Length > 0;
        }
        catch
        {
            return false;
        }
    }

    public static bool TryReadPath(string absolutePath, out GameSaveFile data)
    {
        data = null;
        if (string.IsNullOrWhiteSpace(absolutePath) || !File.Exists(absolutePath))
        {
            return false;
        }

        try
        {
            string json = File.ReadAllText(absolutePath);
            if (string.IsNullOrWhiteSpace(json)) return false;

            data = JsonUtility.FromJson<GameSaveFile>(json);
            return data != null;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"GameSaveIo: không đọc được '{absolutePath}'. {e.Message}");
            data = null;
            return false;
        }
    }

    public static bool TryWriteLegacy(GameSaveFile data) => TryWritePath(LegacyFullPath, data);

    /// <summary>
    /// Ghi đè đúng một file trong persistentDataPath (legacy hoặc SVSimSaveSlots/*.json).
    /// Không xóa hay sửa file save khác.
    /// </summary>
    public static bool TryWriteAtAbsolutePath(GameSaveFile data, string absolutePath, out string writtenAbsolutePath)
    {
        writtenAbsolutePath = null;
        if (data == null || string.IsNullOrWhiteSpace(absolutePath))
        {
            return false;
        }

        string normalized = Path.GetFullPath(absolutePath.Trim());
        if (!IsAllowedPersistedAbsoluteWritePath(normalized))
        {
            Debug.LogWarning($"GameSaveIo: từ chối ghi ngoài thư mục save của game — '{normalized}'.");
            return false;
        }

        try
        {
            data.meta ??= new SaveSlotMeta();
            data.meta.fileSafeNameHint = Path.GetFileName(normalized);

            bool ok = TryWritePath(normalized, data);
            if (ok)
            {
                writtenAbsolutePath = normalized;
            }

            return ok;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"GameSaveIo: ghi save thất bại. {e.Message}");
            return false;
        }
    }

    /// <summary>
    /// Xóa một file trong persistentDataPath.
    /// </summary>
    public static bool TryDeleteAbsolutePath(string absolutePath)
    {
        if (string.IsNullOrWhiteSpace(absolutePath))
        {
            return false;
        }

        string normalized = Path.GetFullPath(absolutePath.Trim());
        if (!IsAllowedPersistedAbsoluteWritePath(normalized))
        {
            Debug.LogWarning($"GameSaveIo: từ chối xóa ngoài thư mục save của game — '{normalized}'.");
            return false;
        }

        try
        {
            if (File.Exists(normalized))
            {
                File.Delete(normalized);
                return true;
            }
            return false;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"GameSaveIo: xóa save thất bại. {e.Message}");
            return false;
        }
    }

    /// <summary>Chỉ cho phép legacy svsim_save.json hoặc file JSON ngay trong SVSimSaveSlots.</summary>
    public static bool IsAllowedPersistedAbsoluteWritePath(string normalizedFullPath)
    {
        if (string.IsNullOrWhiteSpace(normalizedFullPath))
        {
            return false;
        }

        try
        {
            if (!normalizedFullPath.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string legacyNorm = Path.GetFullPath(LegacyFullPath);
            if (string.Equals(normalizedFullPath, legacyNorm, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }

            string slotsNorm = Path.GetFullPath(SavesSlotsDirectoryPath);
            string parentOfFile = Path.GetDirectoryName(normalizedFullPath);
            if (string.IsNullOrEmpty(parentOfFile))
            {
                return false;
            }

            string slotParentNorm = Path.GetFullPath(parentOfFile);
            return string.Equals(slotParentNorm, slotsNorm, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    /// <returns>Đường dẫn có LastWriteUtc mới nhất, hoặc null.</returns>
    public static string ResolveMostRecentSavePathOrLegacy()
    {
        var entries = EnumerateSortedByNewest();
        return entries.Count > 0 ? entries[0].FullPath : null;
    }

    public readonly struct SaveSlotListEntry
    {
        public readonly string FullPath;
        public readonly string TitleLine;
        public readonly string SubLine;
        public readonly DateTime LastWriteUtc;

        public SaveSlotListEntry(string fullPath, string titleLine, string subLine, DateTime lastWriteUtc)
        {
            FullPath = fullPath;
            TitleLine = titleLine;
            SubLine = subLine;
            LastWriteUtc = lastWriteUtc;
        }
    }

    /// <summary>Ngược thời gian sửa file (slot + legacy).</summary>
    public static List<SaveSlotListEntry> EnumerateSortedByNewest()
    {
        List<(string Path, DateTime Written)> gathered = new List<(string Path, DateTime Written)>();

        try
        {
            if (LegacySaveExists())
            {
                gathered.Add((LegacyFullPath, File.GetLastWriteTimeUtc(LegacyFullPath)));
            }

            if (Directory.Exists(SavesSlotsDirectoryPath))
            {
                string[] paths = Directory.GetFiles(SavesSlotsDirectoryPath, "*.json", SearchOption.TopDirectoryOnly);
                for (int i = 0; i < paths.Length; i++)
                {
                    string p = paths[i];
                    gathered.Add((p, File.GetLastWriteTimeUtc(p)));
                }
            }

            gathered.Sort((a, b) => DateTime.Compare(b.Written, a.Written));

            List<SaveSlotListEntry> list = new List<SaveSlotListEntry>(gathered.Count);
            foreach (var g in gathered)
            {
                list.Add(BuildListEntryMetadata(g.Path, g.Written));
            }

            return list;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"GameSaveIo: liệt kê save lỗi. {e.Message}");
            return new List<SaveSlotListEntry>();
        }
    }

    static SaveSlotListEntry BuildListEntryMetadata(string absolutePath, DateTime lastWriteUtc)
    {
        string title =
            $"{lastWriteUtc.ToLocalTime():yyyy-MM-dd HH:mm:ss}" +
            $"  ·  {Path.GetFileName(absolutePath)}";

        string sub =
            "Chưa đọc được tiến độ — có thể file hỏng.";

        if (TryReadPath(absolutePath, out GameSaveFile data) && data != null)
        {
            SaveSlotMeta meta = data.meta;
            string gameLine = SummaryFromPayload(data);

            if (meta != null
                && (!string.IsNullOrWhiteSpace(meta.realWorldSavedAtDisplay)
                    || !string.IsNullOrWhiteSpace(meta.gameClockSummary)))
            {
                string rw = string.IsNullOrWhiteSpace(meta.realWorldSavedAtDisplay)
                    ? lastWriteUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss")
                    : meta.realWorldSavedAtDisplay;

                title =
                    $"{rw}  ·  " +
                    (string.IsNullOrWhiteSpace(meta.fileSafeNameHint)
                        ? Path.GetFileName(absolutePath)
                        : meta.fileSafeNameHint);

                string subAgg = AggregateMetaSubtitle(meta);
                sub = string.IsNullOrWhiteSpace(subAgg) ? gameLine : subAgg;
            }
            else
            {
                sub = gameLine;
            }
        }

        return new SaveSlotListEntry(absolutePath, title.Trim(), sub.Trim(), lastWriteUtc);
    }

    static string SummaryFromPayload(GameSaveFile data)
    {
        string clock = "?";
        if (data.time != null)
        {
            clock =
                $"HK{data.time.semester} Ngày {data.time.dayInSemester}, {data.time.hour}:00";
        }

        string sc = string.IsNullOrWhiteSpace(data.sceneName) ? "?" : data.sceneName;
        return $"{clock}  |  Cảnh: {sc}";
    }

    static string AggregateMetaSubtitle(SaveSlotMeta meta)
    {
        if (meta == null)
        {
            return string.Empty;
        }

        var a = meta.gameClockSummary?.Trim();
        var b = meta.sceneNameDisplay?.Trim();
        var c = meta.progressSummary?.Trim();
        bool hasA = !string.IsNullOrEmpty(a);
        bool hasB = !string.IsNullOrEmpty(b);
        bool hasC = !string.IsNullOrEmpty(c);
        if (!hasA && !hasB && !hasC)
        {
            return string.Empty;
        }

        System.Text.StringBuilder sb = new System.Text.StringBuilder(128);
        if (hasA)
        {
            sb.Append(a);
        }

        if (hasB)
        {
            if (sb.Length > 0)
            {
                sb.Append(" · ");
            }

            sb.Append(b);
        }

        if (hasC)
        {
            if (sb.Length > 0)
            {
                sb.Append(" · ");
            }

            sb.Append(c);
        }

        return sb.ToString();
    }

    public static bool TryDeleteLegacySaveOnly()
    {
        try
        {
            if (!File.Exists(LegacyFullPath)) return true;

            File.Delete(LegacyFullPath);
            return true;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"GameSaveIo: xóa legacy thất bại. {e.Message}");
            return false;
        }
    }

    /// <summary>Gợi ý chỉnh sửa: xóa toàn bộ slot (Không xóa mặc định trong New Game).</summary>
    public static bool TryDeleteAllSlotFiles(bool alsoDeleteLegacy)
    {
        try
        {
            if (alsoDeleteLegacy)
            {
                TryDeleteLegacySaveOnly();
            }

            if (Directory.Exists(SavesSlotsDirectoryPath))
            {
                string[] paths =
                    Directory.GetFiles(SavesSlotsDirectoryPath, "*.json", SearchOption.TopDirectoryOnly);
                foreach (var p in paths)
                {
                    File.Delete(p);
                }
            }

            return true;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"GameSaveIo: xóa slots thất bại. {e.Message}");
            return false;
        }
    }

    static bool TryWritePath(string absolutePath, GameSaveFile data)
    {
        if (data == null) return false;

        try
        {
            string dirPath = Path.GetDirectoryName(absolutePath);
            if (!string.IsNullOrEmpty(dirPath))
            {
                Directory.CreateDirectory(dirPath);
            }

            File.WriteAllText(absolutePath, JsonUtility.ToJson(data, prettyPrint: true));
            return true;
        }
        catch (Exception e)
        {
            Debug.LogWarning($"GameSaveIo: không ghi được '{absolutePath}'. {e.Message}");
            return false;
        }
    }

    /// <summary>Đọc backward-compat: chỉ legacy.</summary>
    public static bool TryRead(out GameSaveFile data) => TryReadLegacy(out data);

    /// <summary>Ghi backward-compat vào một file duy nhất (ít dùng).</summary>
    public static bool TryWrite(GameSaveFile data) => TryWriteLegacy(data);

    /// <summary>Xóa file legacy đơn (compatibility).</summary>
    public static bool TryDelete() => TryDeleteLegacySaveOnly();

    public static bool SaveExists() => AnySaveExists();
}
