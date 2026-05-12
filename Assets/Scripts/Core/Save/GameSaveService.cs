using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Aggregates DontDestroy snapshots + disk IO for manual saves.</summary>
public sealed class GameSaveService : MonoBehaviour
{
    public static GameSaveService Instance { get; private set; }

    GameSaveFile _queuedAfterGameplaySceneLoad;

    /// <summary>
    /// File chỉ được ghi đè khi Lưu: gán sau Load/Continue; New Game không load thì đặt sau lần Lưu đầu trong session.
    /// </summary>
    string _sessionPersistedAbsolutePath;

    [Header("New game baseline (applied after deleting save file)")]
    [SerializeField] private int newGameClockSemester = 1;
    [SerializeField] private int newGameClockDay = 1;
    [SerializeField] private int newGameClockHour = 8;
    [SerializeField] private int newHudStatDay = 1;
    [SerializeField] private int newHudStatClockHour = 8;
    [SerializeField] private float newGpa = 0f;
    [SerializeField] private float newStress = 0f;
    [SerializeField] private float newMoney = 0f;
    [SerializeField] private float newHealth = 100f;
    [SerializeField] private float newEnergy = 100f;
    [SerializeField] private float newSocial = 0f;
    [SerializeField] private float newSkill = 0f;

    readonly Dictionary<string, ItemData> _itemLookup =
        new Dictionary<string, ItemData>(StringComparer.Ordinal);

    void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;

        if (transform.parent != null)
        {
            transform.SetParent(null);
        }

        DontDestroyOnLoad(gameObject);
    }

    /// <summary>Lazy bootstrap if the Bootstrap scene omitted this component.</summary>
    public static GameSaveService ResolveOrCreate()
    {
        if (Instance != null)
        {
            return Instance;
        }

        GameObject owner = new GameObject(nameof(GameSaveService));
        DontDestroyOnLoad(owner);
        return owner.AddComponent<GameSaveService>();
    }

    public static bool HasPersistedSave() => GameSaveIo.SaveExists();

    public void QueueApplyAfterGameplaySceneLoad(GameSaveFile file)
    {
        _queuedAfterGameplaySceneLoad = file;
    }

    /// <summary>Used by SceneFlowController after additive UI + gameplay worlds exist.</summary>
    public bool TryConsumePendingAfterSceneApplied(out bool appliedAnything)
    {
        appliedAnything = false;
        if (_queuedAfterGameplaySceneLoad == null)
        {
            return false;
        }

        ApplySnapshotIntoRuntime(_queuedAfterGameplaySceneLoad);
        _queuedAfterGameplaySceneLoad = null;
        appliedAnything = true;
        return true;
    }

    /// <summary>
    /// Ghi đúng vào session đã continue (hoặc file mới tạo trong session nếu chưa có).
    /// </summary>
    public bool CaptureActiveSessionToDisk(out string savedAbsolutePath)
    {
        GameSaveFile snapshot = CaptureActiveSession();
        savedAbsolutePath = null;

        if (snapshot == null)
        {
            return false;
        }

        string dest = _sessionPersistedAbsolutePath;
        if (string.IsNullOrEmpty(dest))
        {
            Directory.CreateDirectory(GameSaveIo.SavesSlotsDirectoryPath);
            string fname = $"{DateTime.Now:yyyy-MM-dd}_{DateTime.Now:HHmm}.json";
            dest = Path.Combine(GameSaveIo.SavesSlotsDirectoryPath, fname);
            _sessionPersistedAbsolutePath = dest;
        }

        bool ok = GameSaveIo.TryWriteAtAbsolutePath(snapshot, dest, out savedAbsolutePath);
        if (ok && !string.IsNullOrEmpty(savedAbsolutePath))
        {
            _sessionPersistedAbsolutePath = Path.GetFullPath(savedAbsolutePath.Trim());
        }

        return ok;
    }

    /// <summary>Lưu nhanh từ pause menu — ghi đè bản trong thư mục slot.</summary>
    public bool CaptureActiveSessionToDisk()
    {
        return CaptureActiveSessionToDisk(out _);
    }

    GameSaveFile CaptureActiveSession()
    {
        var file = new GameSaveFile();

        Scene active = SceneManager.GetActiveScene();
        file.sceneName = active.IsValid() ? active.name : string.Empty;
        file.useSavedWorldPosition = true;
        file.spawnIdFallback = "Default";

        if (PersistentPlayer.Instance != null)
        {
            Transform t = PersistentPlayer.Instance.transform;
            file.playerPosition = SerVector3.From(t.position);
        }

        GameAudioSettings.ApplyStoredVolumeToAudioListenerIfNeeded();
        file.masterVolumeLinear = GameAudioSettings.MasterVolumeLinear;

        if (GameTimeManager.Instance != null)
        {
            var time = GameTimeManager.Instance;

            file.time = new TimePayload
            {
                semester = time.Semester,
                dayInSemester = time.DayInSemester,
                hour = time.Hour,
                autoTick = time.AutoTickEnabled,
                secondsPerGameHour = time.SecondsPerGameHourPublic,
                tickTimer = time.TickTimerSeconds
            };
        }

        if (StatManager.Instance != null)
        {
            StatManager sm = StatManager.Instance;

            file.stats = new StatPayload
            {
                day = sm.day,
                time = sm.time,
                gpa = sm.gpa,
                stress = sm.stress,
                money = sm.money,
                health = sm.health,
                energy = sm.energy,
                social = sm.social,
                skill = sm.skill
            };
        }

        RebuildItemLookupCaches();

        if (InventorySystem.Instance != null && InventorySystem.Instance.stacks != null)
        {
            var list = new List<InventoryPayload>(InventorySystem.Instance.stacks.Count);
            foreach (InventoryStack stack in InventorySystem.Instance.stacks)
            {
                if (stack?.item == null || stack.amount <= 0)
                {
                    continue;
                }

                list.Add(new InventoryPayload { itemKey = stack.item.name, amount = stack.amount });
            }

            file.inventory = list.ToArray();
        }

        file.quest = QuestManager.Instance?.CaptureQuestPayload();
        file.story = StoryEventManager.Instance?.CaptureStoryPayload();
        file.phone = PhoneSystem.Instance?.Persist_CapturePhone();
        file.semester = SemesterProgressManager.Instance?.Persist_CaptureSemester();
        file.ending = CaptureEndingPayload();

        if (LessonQuizManager.Instance != null)
        {
            file.lessonQuiz = new LessonQuizPayload
            {
                completedEntryIdsOrdered = LessonQuizManager.Instance.Persist_CaptureCompletedQuizEntryIds()
            };
        }

        if (GameplayGuideLetter.Instance != null)
        {
            file.gameplayGuideShown = GameplayGuideLetter.Instance.WasGameplayGuideDismissed();
        }

        PopulateSaveMeta(file);
        return file;
    }

    void PopulateSaveMeta(GameSaveFile file)
    {
        if (file == null)
        {
            return;
        }

        DateTime now = DateTime.Now;
        file.schemaVersion = Mathf.Max(file.schemaVersion, 2);

        file.meta ??= new SaveSlotMeta();
        SaveSlotMeta m = file.meta;
        m.realWorldSavedAtIso = now.ToString("o");
        m.realWorldSavedAtDisplay = now.ToString("yyyy-MM-dd HH:mm:ss");

        string rawScene = file.sceneName?.Trim();
        m.sceneNameDisplay = string.IsNullOrEmpty(rawScene)
            ? "?"
            : Path.GetFileNameWithoutExtension(rawScene);

        if (file.time != null)
        {
            TimePayload t = file.time;
            m.gameClockSummary = $"HK{t.semester} · Ngày {t.dayInSemester} · {t.hour}h game";
        }
        else
        {
            m.gameClockSummary = "Thời gian trong game: ?";
        }

        m.progressSummary = BuildLiveProgressSummary();
    }

    static string BuildLiveProgressSummary()
    {
        List<string> parts = new List<string>(6);

        if (StatManager.Instance != null)
        {
            StatManager sm = StatManager.Instance;
            parts.Add($"GPA {sm.gpa:F1} · Tiền {sm.money:F0}");
        }

        StoryEventManager story = StoryEventManager.Instance;
        if (story != null && story.ActiveEvent != null)
        {
            parts.Add($"Sự kiện: {story.ActiveEvent.GetDisplayTitle()}");
        }

        QuestManager quests = QuestManager.Instance;
        if (quests?.ActiveQuests != null)
        {
            int n = 0;
            foreach (QuestData q in quests.ActiveQuests)
            {
                if (q == null)
                {
                    continue;
                }

                string label = string.IsNullOrWhiteSpace(q.title) ? q.GetId() : q.title;
                parts.Add($"Quest: {label}");
                if (++n >= 2)
                {
                    break;
                }
            }
        }

        if (EndingManager.Instance != null && EndingManager.Instance.HasEnded)
        {
            parts.Add($"Kết thúc: {EndingManager.Instance.CurrentEnding}");
        }

        return parts.Count == 0 ? "Đang chơi…" : string.Join(" · ", parts);
    }

    static EndingPayload CaptureEndingPayload()
    {
        if (EndingManager.Instance == null)
        {
            return new EndingPayload { hasEnded = false, endingTypeOrdinal = (int)EndingType.None };
        }

        return new EndingPayload
        {
            hasEnded = EndingManager.Instance.HasEnded,
            endingTypeOrdinal = (int)EndingManager.Instance.CurrentEnding
        };
    }

    public void ApplySnapshotIntoRuntime(GameSaveFile file)
    {
        if (file == null) return;

        GameAudioSettings.MasterVolumeLinear = Mathf.Clamp01(file.masterVolumeLinear);

        GameplayGuideLetter.Instance?.Persist_ApplyFromSaveFlag(file.gameplayGuideShown);

        RebuildItemLookupCaches();

        if (EndingManager.Instance != null)
        {
            EndingManager.Instance.Persist_ApplyEndingQuiet(file.ending);
        }

        if (SemesterProgressManager.Instance != null && file.semester != null)
        {
            SemesterProgressManager.Instance.Persist_ApplySemester(file.semester);
        }

        if (StatManager.Instance != null && file.stats != null)
        {
            StatManager.Instance.Persist_Apply(file.stats);
        }

        if (GameTimeManager.Instance != null && file.time != null)
        {
            GameTimeManager.Instance.RestoreFromSave(file.time, invokeTimeCallbacksOnceAfter: false);
        }

        if (LessonQuizManager.Instance != null)
        {
            LessonQuizManager.Instance.Persist_RestoreCompletedQuizEntries(file.lessonQuiz?.completedEntryIdsOrdered);
        }

        if (PhoneSystem.Instance != null)
        {
            PhoneSystem.Instance.Persist_ApplyPhone(file.phone);
        }

        if (QuestManager.Instance != null && file.quest != null)
        {
            QuestManager.Instance.Persist_ApplyQuestPayload(file.quest);
        }

        if (InventorySystem.Instance != null)
        {
            InventorySystem.Instance.Persist_ApplyInventoryStacks(file.inventory, ResolveItemOrNull);
        }

        if (StoryEventManager.Instance != null)
        {
            StoryEventManager.Instance.Persist_ApplyStoryPayload(file.story);
        }

        if (GameTimeManager.Instance != null)
        {
            GameTimeManager.Instance.RaiseTimeChangedOnce();
        }

        if (EventManager.Instance != null)
        {
            EventManager.Instance.NotifyStatChanged();
        }
    }

    public void ContinueFromPersistedSave()
    {
        string pick = GameSaveIo.ResolveMostRecentSavePathOrLegacy();
        if (string.IsNullOrEmpty(pick))
        {
            Debug.LogWarning("GameSaveService: không có file save để tiếp tục.");
            return;
        }

        LoadGameFromAbsolutePath(pick);
    }

    /// <summary>Tiếp tục từ một slot cụ thể (Main Menu hoặc test).</summary>
    public void LoadGameFromAbsolutePath(string absolutePath)
    {
        if (string.IsNullOrWhiteSpace(absolutePath))
        {
            Debug.LogWarning("GameSaveService: đường dẫn save trống.");
            return;
        }

        if (!GameSaveIo.TryReadPath(absolutePath, out GameSaveFile data) || data == null)
        {
            Debug.LogWarning($"GameSaveService: không đọc được save '{absolutePath}'.");
            return;
        }

        _sessionPersistedAbsolutePath = Path.GetFullPath(absolutePath.Trim());
        GameResumeContext.SetPendingTeleport(data.playerPosition.ToVector3());
        QueueApplyAfterGameplaySceneLoad(data);

        string sceneToLoad =
            string.IsNullOrWhiteSpace(data.sceneName) ? "22_PlayerHouse" : data.sceneName.Trim();

        if (SceneFlowController.Instance != null)
        {
            SceneFlowController.Instance.LoadGameplayScene(sceneToLoad,
                string.IsNullOrWhiteSpace(data.spawnIdFallback) ? "Default" : data.spawnIdFallback.Trim());
        }
        else
        {
            Debug.LogWarning("GameSaveService: SceneFlowController missing — không thể vào scene đã lưu.");
        }
    }

    /// <summary>Wipes disk + resets core singletons so « New Game » truly restarts the sim.</summary>
    public void PrepareFreshCampaign()
    {
        GameSaveIo.TryDelete();
        GameResumeContext.Clear();
        _queuedAfterGameplaySceneLoad = null;
        _sessionPersistedAbsolutePath = null;

        if (GameTimeManager.Instance != null)
        {
            GameTimeManager.Instance.RestoreFromSave(
                new TimePayload
                {
                    semester = newGameClockSemester,
                    dayInSemester = newGameClockDay,
                    hour = newGameClockHour,
                    autoTick = true,
                    secondsPerGameHour = GameTimeManager.Instance.SecondsPerGameHourPublic,
                    tickTimer = 0f
                },
                invokeTimeCallbacksOnceAfter: false);

            GameTimeManager.Instance.SetAutoTickEnabled(true);
        }

        if (StatManager.Instance != null)
        {
            StatManager.Instance.Persist_Apply(new StatPayload
            {
                day = newHudStatDay,
                time = newHudStatClockHour,
                gpa = newGpa,
                stress = newStress,
                money = StatManager.Instance != null ? StatManager.Instance.NewCampaignStartingMoney : newMoney,
                health = newHealth,
                energy = newEnergy,
                social = newSocial,
                skill = newSkill
            });
        }

        EndingManager.Instance?.Persist_ResetEndingQuiet();
        SemesterProgressManager.Instance?.Persist_ResetSemesterForSave();
        LessonQuizManager.Instance?.Persist_ResetCompletedQuizzesForSave();
        PhoneSystem.Instance?.Persist_ResetPhoneForSave();
        QuestManager.Instance?.ResetRuntimeQuestStateForSave();
        StoryEventManager.Instance?.ResetRuntimeStoryStateForSave();
        InventorySystem.Instance?.ClearStacksForSave();
        GameplayGuideLetter.Instance?.PrepareFresh_ResetGuideFlag();

        if (GameTimeManager.Instance != null)
        {
            GameTimeManager.Instance.RaiseTimeChangedOnce();
        }

        if (EventManager.Instance != null)
        {
            EventManager.Instance.NotifyStatChanged();
        }
    }

    void RebuildItemLookupCaches()
    {
        _itemLookup.Clear();

        var bag = new HashSet<ItemData>();

        if (InventorySystem.Instance != null)
        {
            InventorySystem.Instance.Persist_CollectDistinctItems(bag);
        }

        if (QuestManager.Instance != null)
        {
            IReadOnlyList<QuestData> quests = QuestManager.Instance.Persist_AllConfiguredQuestAssets;
            for (int i = 0; i < quests.Count; i++)
            {
                CollectQuestItems(quests[i], bag);
            }
        }

        foreach (ItemData item in bag)
        {
            RegisterItemKey(item);
        }
    }

    static void CollectQuestItems(QuestData quest, HashSet<ItemData> bag)
    {
        if (quest?.objectives == null) return;

        for (int i = 0; i < quest.objectives.Count; i++)
        {
            QuestObjective obj = quest.objectives[i];
            if (obj == null || obj.type != QuestObjectiveType.CollectItem)
            {
                continue;
            }

            if (obj.item != null)
            {
                bag.Add(obj.item);
            }
        }
    }

    void RegisterItemKey(ItemData item)
    {
        if (item == null) return;

        if (!_itemLookup.ContainsKey(item.name))
        {
            _itemLookup.Add(item.name, item);
        }

        if (!string.IsNullOrWhiteSpace(item.itemName))
        {
            string friendly = item.itemName.Trim();
            if (!_itemLookup.ContainsKey(friendly))
            {
                _itemLookup.Add(friendly, item);
            }
        }
    }

    ItemData ResolveItemOrNull(string key)
    {
        if (string.IsNullOrWhiteSpace(key)) return null;

        string trimmed = key.Trim();
        if (_itemLookup.TryGetValue(trimmed, out ItemData direct))
        {
            return direct;
        }

        foreach (KeyValuePair<string, ItemData> pair in _itemLookup)
        {
            if (string.Equals(pair.Key, trimmed, StringComparison.OrdinalIgnoreCase))
            {
                return pair.Value;
            }
        }

        Debug.LogWarning($"GameSaveService: missing ItemData for key '{trimmed}'.");
        return null;
    }
}
