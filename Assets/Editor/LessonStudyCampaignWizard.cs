using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Mot lan chay Menu: sinh quiz chu de theo ngay + lich LessonQuizManager + quest + tu dong chap nhan quest.
/// Git buoc trong Unity: SVSimulator ▶ Học kỳ ▶ Generate Lesson + Quest 15 ngày...</summary>
public static class LessonStudyCampaignWizard
{
    private const string CampaignQuizFolder = "Assets/ScriptableObject/Quiz/CampaignLesson";
    private const string CampaignQuestFolder = "Assets/Quest/CampaignStudy";
    private const string BootstrapScenePath = "Assets/Scenes/00_Bootstrap.unity";
    private const string MidtermQuizPath = "Assets/ScriptableObject/Quiz/Quiz_Day10_Midterm.asset";
    private const string FinalQuizPath = "Assets/ScriptableObject/Quiz/Quiz_Day14_Final.asset";

    [MenuItem("SVSimulator/Học kỳ/Generate Lesson + Quest 15 ngày (nhà + lớp)")]
    public static void Generate()
    {
        if (!EditorUtility.DisplayDialog(
                "Generate campaign",
                "Sẽ sinh/ghi đè các asset trong:\n• " + CampaignQuizFolder + "\n• " + CampaignQuestFolder
                + "\n\nvà cập nhật LessonQuizManager + QuestManager trong 00_Bootstrap (giữ quest/quiz không nằm trong CampaignLesson / CampaignStudy). Tiếp tục?",
                "Chạy",
                "Hủy"))
        {
            return;
        }

        EnsureFolders();
        CleanupGeneratedAssetsFolder(CampaignQuizFolder, "Campaign_");
        CleanupGeneratedAssetsFolder(CampaignQuestFolder, "Campaign_");

        var midterm = AssetDatabase.LoadAssetAtPath<LessonQuizData>(MidtermQuizPath);
        var finalQuiz = AssetDatabase.LoadAssetAtPath<LessonQuizData>(FinalQuizPath);
        if (midterm == null || finalQuiz == null)
        {
            EditorUtility.DisplayDialog("Lỗi", $"Không tìm được Midterm ({MidtermQuizPath}) hoặc Final ({FinalQuizPath}). Kiểm tra đường dẫn asset.", "OK");
            return;
        }

        AssetDatabase.StartAssetEditing();
        try
        {
            SetMorningExamPenalty(midterm, 0.11f);
            SetMorningExamPenalty(finalQuiz, 0.13f);

            Dictionary<int, LessonQuizData> classLessons = BuildClassDayLessons(out LessonQuizData wrap);
            var schedules = BuildSchedules(classLessons, midterm, finalQuiz, wrap);
            List<QuestData> quests = BuildQuests();

            WriteBootstrap(schedules, quests);
            EnsureQuestAutoAssignerOnSceneOpened();
            EditorSceneManager.SaveOpenScenes();

            AssetDatabase.SaveAssets();
        }
        finally
        {
            AssetDatabase.StopAssetEditing();
            AssetDatabase.Refresh();
        }

        EditorUtility.DisplayDialog("Xong", "Đã sinh quiz + quest 15 ngày và đã nhét vào 00_Bootstrap. Hãy mở scene để Inspector refresh nếu cần.", "OK");
    }

    private static void EnsureFolders()
    {
        EnsureFolderRecursive(CampaignQuizFolder);
        EnsureFolderRecursive(CampaignQuestFolder);
        EnsureFolderRecursive("Assets/Editor");
    }

    private static void EnsureFolderRecursive(string path)
    {
        if (AssetDatabase.IsValidFolder(path)) return;

        string parent = Path.GetDirectoryName(path)?.Replace("\\", "/") ?? "Assets";
        string leaf = Path.GetFileName(path);
        if (!AssetDatabase.IsValidFolder(parent))
        {
            EnsureFolderRecursive(parent);
        }

        AssetDatabase.CreateFolder(parent, leaf);
    }

    private static void CleanupGeneratedAssetsFolder(string folder, string namePrefixStartsWithCampaign)
    {
        if (!AssetDatabase.IsValidFolder(folder)) return;

        var guids = AssetDatabase.FindAssets("t:ScriptableObject", new[] { folder });
        foreach (var guid in guids)
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            string fileName = Path.GetFileNameWithoutExtension(path);
            if (!string.IsNullOrEmpty(fileName) && fileName.StartsWith(namePrefixStartsWithCampaign, System.StringComparison.Ordinal))
            {
                AssetDatabase.DeleteAsset(path);
            }
        }
    }

    private static void SetMorningExamPenalty(LessonQuizData quiz, float penalty)
    {
        if (quiz == null) return;
        var so = new SerializedObject(quiz);
        SerializedProperty pen = so.FindProperty("morningRecapGpaPenaltyPerWrong");
        if (pen != null)
        {
            pen.floatValue = penalty;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        EditorUtility.SetDirty(quiz);
    }

    private static Dictionary<int, LessonQuizData> BuildClassDayLessons(out LessonQuizData eve15Wrap)
    {
        var dict = new Dictionary<int, LessonQuizData>();
        int[] days = { 1, 2, 3, 4, 5, 6, 7, 8, 9, 11, 12, 13, 14 };
        foreach (int d in days)
        {
            string path = $"{CampaignQuizFolder}/Camp_ClassDay_{d:D2}.asset";
            var q = CreateLessonQuiz($"Chủ đề ngày học {d}", LessonQuestionPack.ForClassDay(d), path);
            dict[d] = q;
        }

        eve15Wrap = CreateLessonQuiz(
            "Tự kiểm tra cuối học kỳ",
            LessonQuestionPack.SemesterWrap(),
            $"{CampaignQuizFolder}/Camp_EveWrap_D15.asset");
        eve15Wrap.title = "Nhật ký ôn cuối kỳ (ở nhà)";
        eve15Wrap.intro = "Điểm lại các thói quen học tập trước khi sang ngày mới trong học kỳ.";

        return dict;
    }

    private static LessonQuizData CreateLessonQuiz(string title, List<LessonQuizQuestion> questions, string assetPath)
    {
        var d = ScriptableObject.CreateInstance<LessonQuizData>();
        d.kind = LessonQuizKind.SmallQuiz;
        d.title = title;
        d.intro = "Nội dung trùng với những gì ôn nhà và lên lớp sẽ kiểm tra lại.";
        d.questions = questions;
        d.passScore = 0.5f;
        d.gpaOnPass = 0.08f;
        d.gpaPerCorrectPercent = 0.04f;
        d.skillOnPass = 5f;
        d.stressOnFail = 4f;
        d.stressPerWrong = 1.1f;
        d.morningRecapGpaPenaltyPerWrong = 0.07f;
        d.hoursToAdvance = 1;

        AssetDatabase.CreateAsset(d, assetPath);
        return d;
    }

    private static List<LessonQuizManager.QuizSchedule> BuildSchedules(
        Dictionary<int, LessonQuizData> lessons,
        LessonQuizData mid,
        LessonQuizData fin,
        LessonQuizData eve15Wrap)
    {
        var list = new List<LessonQuizManager.QuizSchedule>(30);
        for (int day = 1; day <= 15; day++)
        {
            LessonQuizData morningData = PickMorning(day, lessons, mid, fin);
            list.Add(new LessonQuizManager.QuizSchedule
            {
                semester = 1,
                dayInSemester = day,
                data = morningData,
                onlyFirstStudyOfDay = true,
                studyContext = LessonQuizStudyContext.MorningClassDesk
            });

            LessonQuizData eveData = PickEvening(day, lessons, mid, fin, eve15Wrap);
            list.Add(new LessonQuizManager.QuizSchedule
            {
                semester = 1,
                dayInSemester = day,
                data = eveData,
                onlyFirstStudyOfDay = true,
                studyContext = LessonQuizStudyContext.EveningHome
            });
        }

        return list;
    }

    private static LessonQuizData PickMorning(int day, Dictionary<int, LessonQuizData> lessons, LessonQuizData mid, LessonQuizData fin)
    {
        if (day == 10) return mid;
        if (day == 15) return fin;
        return lessons[day];
    }

    private static LessonQuizData PickEvening(int day, Dictionary<int, LessonQuizData> lessons, LessonQuizData mid, LessonQuizData fin, LessonQuizData eve15Wrap)
    {
        if (day == 9) return mid;
        if (day == 14) return fin;
        if (day == 15) return eve15Wrap;
        return lessons[day + 1];
    }

    private static List<QuestData> BuildQuests()
    {
        var quests = new List<QuestData>(30);
        for (int d = 1; d <= 15; d++)
        {
            quests.Add(CreateQuest(
                $"study_eve_sem1_{d:D2}",
                $"Ôn tại nhà (đêm ngày {d})",
                $"Hoàn thành quiz ôn nhà vào đêm ngày {d}.",
                1,
                d,
                LessonQuizStudyContext.EveningHome,
                $"{CampaignQuestFolder}/Camp_Q_study_eve_sem1_{d:D2}.asset"));

            quests.Add(CreateQuest(
                $"study_cls_sem1_{d:D2}",
                $"Kiểm tra tại lớp (ngày {d})",
                $"Hoàn thành quiz trên bàn vào trong giờ học ngày {d} (khớp ND đã ôn đêm hôm trước, trừ GPA nếu sai).",
                1,
                d,
                LessonQuizStudyContext.MorningClassDesk,
                $"{CampaignQuestFolder}/Camp_Q_study_cls_sem1_{d:D2}.asset"));
        }

        return quests;
    }

    private static QuestData CreateQuest(
        string stableId,
        string title,
        string description,
        int sem,
        int calDay,
        LessonQuizStudyContext ctx,
        string path)
    {
        var q = ScriptableObject.CreateInstance<QuestData>();
        q.questId = stableId;
        q.title = title;
        q.description = description;
        q.objectives = new List<QuestObjective>
        {
            new QuestObjective
            {
                type = QuestObjectiveType.CompleteLessonQuiz,
                lessonQuizSemester = sem,
                lessonQuizCalendarDay = calDay,
                lessonQuizContext = ctx
            }
        };
        q.rewardSkill = ctx == LessonQuizStudyContext.EveningHome ? 3f : 5f;
        q.autoCompleteWhenReady = true;
        AssetDatabase.CreateAsset(q, path);
        return q;
    }

    private static void WriteBootstrap(List<LessonQuizManager.QuizSchedule> newRows, List<QuestData> newQuests)
    {
        var scene = EditorSceneManager.OpenScene(BootstrapScenePath, OpenSceneMode.Single);

        var lqm = Object.FindObjectsByType<LessonQuizManager>(FindObjectsInactive.Include).FirstOrDefault();
        var qm = Object.FindObjectsByType<QuestManager>(FindObjectsInactive.Include).FirstOrDefault();
        if (lqm == null || qm == null)
        {
            Debug.LogError("LessonStudyCampaignWizard: Không thấy LessonQuizManager hoặc QuestManager trong 00_Bootstrap.");
            return;
        }

        var lso = new SerializedObject(lqm);
        StripCampaignEntries(lso.FindProperty("entries"), "CampaignLesson");
        AppendLessonSchedules(lso.FindProperty("entries"), newRows);
        lso.ApplyModifiedPropertiesWithoutUndo();

        var qso = new SerializedObject(qm);
        StripCampaignQuests(qso.FindProperty("allQuests"), "CampaignStudy");
        AppendQuestRefs(qso.FindProperty("allQuests"), newQuests);
        qso.ApplyModifiedPropertiesWithoutUndo();

        EditorUtility.SetDirty(lqm);
        EditorUtility.SetDirty(qm);
        EditorSceneManager.MarkSceneDirty(scene);
    }

    private static void StripCampaignEntries(SerializedProperty entriesProp, string pathSegment)
    {
        if (entriesProp == null) return;
        for (int i = entriesProp.arraySize - 1; i >= 0; i--)
        {
            SerializedProperty dp = entriesProp.GetArrayElementAtIndex(i).FindPropertyRelative("data");
            var lq = dp.objectReferenceValue as LessonQuizData;
            if (lq == null) continue;

            string p = AssetDatabase.GetAssetPath(lq);
            if (!string.IsNullOrEmpty(p) && p.Replace("\\", "/").Contains(pathSegment))
            {
                entriesProp.DeleteArrayElementAtIndex(i);
            }
        }
    }

    private static void AppendLessonSchedules(SerializedProperty entriesProp, List<LessonQuizManager.QuizSchedule> rows)
    {
        int start = entriesProp.arraySize;
        entriesProp.arraySize = start + rows.Count;
        for (int i = 0; i < rows.Count; i++)
        {
            var row = rows[i];
            SerializedProperty el = entriesProp.GetArrayElementAtIndex(start + i);
            el.FindPropertyRelative("semester").intValue = row.semester;
            el.FindPropertyRelative("dayInSemester").intValue = row.dayInSemester;
            el.FindPropertyRelative("data").objectReferenceValue = row.data;
            el.FindPropertyRelative("onlyFirstStudyOfDay").boolValue = row.onlyFirstStudyOfDay;

            SerializedProperty ctxField = el.FindPropertyRelative("studyContext");
            if (ctxField != null)
            {
                ctxField.enumValueIndex = (int)row.studyContext;
            }
        }
    }

    private static void StripCampaignQuests(SerializedProperty listProp, string campaignFolderSlug)
    {
        if (listProp == null) return;
        for (int i = listProp.arraySize - 1; i >= 0; i--)
        {
            var q = listProp.GetArrayElementAtIndex(i).objectReferenceValue as QuestData;
            if (q == null) continue;
            string p = AssetDatabase.GetAssetPath(q);
            if (!string.IsNullOrEmpty(p) && p.Replace("\\", "/").Contains(campaignFolderSlug))
            {
                listProp.DeleteArrayElementAtIndex(i);
            }
        }
    }

    private static void AppendQuestRefs(SerializedProperty listProp, List<QuestData> qs)
    {
        int start = listProp.arraySize;
        listProp.arraySize = start + qs.Count;
        for (int i = 0; i < qs.Count; i++)
        {
            listProp.GetArrayElementAtIndex(start + i).objectReferenceValue = qs[i];
        }
    }

    /// <summary>Thêm LessonStudyQuestAutoAssigner vào cùng GameObject QuestManager.</summary>
    private static void EnsureQuestAutoAssignerOnSceneOpened()
    {
        var qm = Object.FindObjectsByType<QuestManager>(FindObjectsInactive.Include).FirstOrDefault();
        if (qm == null) return;
        var exist = qm.GetComponent<LessonStudyQuestAutoAssigner>();
        if (exist == null)
        {
            qm.gameObject.AddComponent<LessonStudyQuestAutoAssigner>();
            EditorUtility.SetDirty(qm.gameObject);
        }
    }

    private static class LessonQuestionPack
    {
        internal static List<LessonQuizQuestion> SemesterWrap()
        {
            return new List<LessonQuizQuestion>
            {
                MCQ(
                    "Muốn ổn định GPA cả học kỳ, điều nào nên làm đều đặn nhất?",
                    new[] { "Đi học và ôn có lịch, tránh chỉ cày một đêm", "Chỉ học khi gần thi", "Bỏ qua buổi sáng", "Không ôn nhà buổi tối" },
                    0),
                MCQ(
                    "Ôn tối và kiểm tra sáng cùng nội dung giúp gì?",
                    new[] { "Ghi nhớ sâu hơn và bớt bất ngờ khi vào phòng", "Không có tác dụng", "Tăng stress vô cớ", "Làm giảm kỹ năng làm quiz" },
                    0),
                MCQ(
                    "Nếu trả lời sai trong buổi kiểm tra lại buổi sáng, game sẽ xử lý thế nào?",
                    new[] { "Trừ GPA theo mỗi câu sai (và các hiệu ứng stat khác theo preset)", "Luôn tăng GPA", "Không thay đổi GPA", "Kết thúc game luôn" },
                    0)
            };
        }

        internal static List<LessonQuizQuestion> ForClassDay(int lectureDayIndex)
        {
            int seed = Mathf.Clamp(lectureDayIndex, 1, 15);
            return new List<LessonQuizQuestion>
            {
                MCQ(
                    $"[Ngày học {seed}] Chuẩn bị sang ngày tiếp theo: Ưu tiên nào hợp lý?",
                    new[] { "Ôn tối tại nhà trước + kiểm tra lại sáng ở lớp", "Chỉ lên Google lúc 5 phút đầu giờ", "Học bừa không theo chủ đề ngày", "Bỏ qua các câu hỏi lồng vào storyline" },
                    0),
                MCQ(
                    $"[Ngày học {seed}] Kỹ thuật ngủ nghỉ hỗ trợ nhớ kiến thức ra sao?",
                    new[] { "Ngủ đủ 7–8 tiếng, tránh học Marathon", "Luôn học đến 3h để ôn được nhiều", "Chiều chỉ caffeine", "Bỏ ăn tối" },
                    0),
                MCQ(
                    $"[Ngày học {seed}] Khi không chắc đáp án trong lớp, bạn nên?",
                    new[] { "Suy luận từ bài ôn nhà và loại các phương án phi lý nhất", "Chọn đại", "Đổi chủ đề bài ôn không liên quan", "Đổ lỗi cho game" },
                    0),
            };
        }

        private static LessonQuizQuestion MCQ(string prompt, string[] opts, int correctIndex)
        {
            var q = new LessonQuizQuestion
            {
                prompt = prompt,
                options = new List<string>(),
                correctIndex = correctIndex,
                explanation = ""
            };
            foreach (var t in opts)
            {
                q.options.Add(t);
            }

            return q;
        }
    }
}
