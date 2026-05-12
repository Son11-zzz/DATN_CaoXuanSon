using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Sinh 30 <see cref="LessonQuizData"/> (15 buổi tối vui + 15 bài trên lớp khó) và ghi lịch vào
/// <c>LessonQuizManager</c> trong <c>00_Bootstrap.unity</c>.
/// </summary>
public static class LessonQuizCampaign15PackGenerator
{
    const string OutputFolder = "Assets/ScriptableObject/Quiz/CampaignSem1";
    const string BootstrapScenePath = "Assets/Scenes/00_Bootstrap.unity";

    [MenuItem("SVSimulator/Học kỳ/Sinh bộ Quiz 15 ngày (vui + khó)")]
    public static void GenerateAll()
    {
        if (!EditorUtility.DisplayDialog(
                "Sinh bộ Quiz 15 ngày",
                $"Sẽ xóa asset cũ trong:\n{OutputFolder}\n\nVà ghi đè lịch trên LessonQuizManager trong 00_Bootstrap. Tiếp tục?",
                "Chạy",
                "Hủy"))
        {
            return;
        }

        EnsureFolder(OutputFolder);
        DeleteGeneratedAssetsInFolder(OutputFolder);

        var evePaths = new string[15];
        var clsPaths = new string[15];

        AssetDatabase.StartAssetEditing();
        try
        {
            for (int day = 1; day <= 15; day++)
            {
                string evePath = $"{OutputFolder}/SEM1_EVE_D{day:00}.asset";
                string clsPath = $"{OutputFolder}/SEM1_CLASS_D{day:00}.asset";

                CreateEveningAsset(day, evePath);
                CreateClassAsset(day, clsPath);

                evePaths[day - 1] = evePath;
                clsPaths[day - 1] = clsPath;
            }
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        PatchBootstrapSchedule(evePaths, clsPaths);

        EditorUtility.DisplayDialog(
            "Xong",
            "Đã sinh 30 quiz trong CampaignSem1 và cập nhật LessonQuizManager (00_Bootstrap).",
            "OK");
    }

    static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;
        string parent = Path.GetDirectoryName(path)?.Replace("\\", "/") ?? "Assets";
        string leaf = Path.GetFileName(path);
        if (!AssetDatabase.IsValidFolder(parent))
        {
            EnsureFolder(parent);
        }

        AssetDatabase.CreateFolder(parent, leaf);
    }

    static void DeleteGeneratedAssetsInFolder(string folder)
    {
        if (!AssetDatabase.IsValidFolder(folder)) return;

        string[] guids = AssetDatabase.FindAssets("t:LessonQuizData", new[] { folder });
        foreach (string guid in guids)
        {
            string p = AssetDatabase.GUIDToAssetPath(guid);
            if (string.IsNullOrEmpty(p)) continue;
            AssetDatabase.DeleteAsset(p);
        }

        AssetDatabase.Refresh();
    }

    static void CreateEveningAsset(int day, string path)
    {
        var data = ScriptableObject.CreateInstance<LessonQuizData>();
        data.kind = LessonQuizKind.SmallQuiz;
        data.title = $"Đêm nhà — ngày {day} (thư giãn)";
        data.intro = "Ôn kiểu chill: trả lời sai cũng không sao. Sáng mai trên lớp mới \"hơi\" gắt hơn.";
        data.questions = QuizQuestionBank.BuildEveningQuestions(day);
        data.passScore = 0.45f;
        data.gpaOnPass = 0.06f;
        data.gpaPerCorrectPercent = 0.03f;
        data.skillOnPass = 4f;
        data.stressOnFail = 0f;
        data.stressPerWrong = 0f;
        data.morningRecapGpaPenaltyPerWrong = 0f;
        data.hoursToAdvance = 1;

        AssetDatabase.CreateAsset(data, path);
        EditorUtility.SetDirty(data);
    }

    static void CreateClassAsset(int day, string path)
    {
        var data = ScriptableObject.CreateInstance<LessonQuizData>();
        bool mid = day == 10;
        bool fin = day == 15;

        data.kind = fin ? LessonQuizKind.Final : mid ? LessonQuizKind.Midterm : LessonQuizKind.SmallQuiz;
        data.title = fin
            ? $"CK cuối kỳ — ngày {day}"
            : mid
                ? $"Giữa kỳ — ngày {day}"
                : $"Kiểm tra nhanh — ngày {day}";
        data.intro = "Bài trên lớp: câu hỏi nhiễu nhiều hơn. Chọn đáp án chặt chẽ nhất (kiểu event hài hước).";
        data.questions = QuizQuestionBank.BuildClassQuestions(day, mid, fin);
        data.passScore = fin ? 0.65f : mid ? 0.6f : 0.55f;
        data.gpaOnPass = fin ? 0.2f : mid ? 0.16f : 0.1f;
        data.gpaPerCorrectPercent = fin ? 0.12f : mid ? 0.1f : 0.08f;
        data.skillOnPass = fin ? 8f : mid ? 7f : 6f;
        data.stressOnFail = fin ? 14f : mid ? 12f : 9f;
        data.stressPerWrong = fin ? 1.6f : mid ? 1.4f : 1.2f;
        data.morningRecapGpaPenaltyPerWrong = fin ? 0.12f : mid ? 0.11f : 0.09f;
        data.hoursToAdvance = 1;

        AssetDatabase.CreateAsset(data, path);
        EditorUtility.SetDirty(data);
    }

    static void PatchBootstrapSchedule(string[] evePaths, string[] clsPaths)
    {
        var scene = EditorSceneManager.OpenScene(BootstrapScenePath, OpenSceneMode.Single);
        LessonQuizManager mgr = UnityEngine.Object.FindObjectsByType<LessonQuizManager>(
                FindObjectsInactive.Include,
                FindObjectsSortMode.None)
            .FirstOrDefault();

        if (mgr == null)
        {
            Debug.LogError("LessonQuizCampaign15PackGenerator: không tìm thấy LessonQuizManager trong 00_Bootstrap.");
            return;
        }

        var so = new SerializedObject(mgr);
        SerializedProperty entries = so.FindProperty("entries");
        if (entries == null || !entries.isArray)
        {
            Debug.LogError("LessonQuizCampaign15PackGenerator: SerializeField 'entries' không tìm thấy.");
            return;
        }

        entries.ClearArray();
        entries.arraySize = 30;

        for (int day = 1; day <= 15; day++)
        {
            var classData = AssetDatabase.LoadAssetAtPath<LessonQuizData>(clsPaths[day - 1]);
            var eveData = AssetDatabase.LoadAssetAtPath<LessonQuizData>(evePaths[day - 1]);

            int iMorning = (day - 1) * 2;
            int iEvening = iMorning + 1;

            FillEntry(entries, iMorning, day, classData, LessonQuizStudyContext.MorningClassDesk);
            FillEntry(entries, iEvening, day, eveData, LessonQuizStudyContext.EveningHome);
        }

        so.ApplyModifiedPropertiesWithoutUndo();
        EditorUtility.SetDirty(mgr);
        EditorSceneManager.MarkSceneDirty(scene);
        EditorSceneManager.SaveScene(scene);
    }

    static void FillEntry(
        SerializedProperty entries,
        int index,
        int day,
        LessonQuizData asset,
        LessonQuizStudyContext ctx)
    {
        SerializedProperty e = entries.GetArrayElementAtIndex(index);
        e.FindPropertyRelative("semester").intValue = 1;
        e.FindPropertyRelative("dayInSemester").intValue = day;
        e.FindPropertyRelative("data").objectReferenceValue = asset;
        e.FindPropertyRelative("onlyFirstStudyOfDay").boolValue = true;
        var ctxProp = e.FindPropertyRelative("studyContext");
        if (ctxProp != null)
        {
            ctxProp.enumValueIndex = (int)ctx;
        }
    }
}
