using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Bootstrap NPC persistent xuyên scene, đặt vị trí theo NpcSceneAnchor2D.
/// </summary>
public class NpcRuntimeBootstrap : MonoBehaviour
{
    [Serializable]
    private class RuntimeNpcEntry
    {
        public string npcId;
        [Tooltip("Prefab asset trong Project (*.prefab). Không được gán instance trong Hierarchy — unload scene sẽ MissingReference và bootstrap lỗi.")]
        public GameObject prefab;
        [Tooltip("Nếu scene không có anchor: giữ active thay vì ẩn.")]
        public bool keepActiveWithoutAnchor;
    }

    public static NpcRuntimeBootstrap Instance;

    [Header("Pilot NPC Prefabs")]
    [SerializeField] private List<RuntimeNpcEntry> npcs = new List<RuntimeNpcEntry>();

    [Header("Options")]
    [SerializeField] private bool disableDuplicateSceneNpcs = true;
    [SerializeField] private bool logBootstrap = true;

    private readonly Dictionary<string, GameObject> runtimeById =
        new Dictionary<string, GameObject>(StringComparer.OrdinalIgnoreCase);

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
        BuildRuntimeInstances();
    }

    private void OnEnable()
    {
        SceneManager.sceneLoaded += HandleSceneLoaded;
    }

    private void OnDisable()
    {
        SceneManager.sceneLoaded -= HandleSceneLoaded;
    }

    private void Start()
    {
        StartCoroutine(ApplySceneAnchorsNextFrame());
    }

    private void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        if (mode == LoadSceneMode.Additive
            && !string.Equals(scene.name, SceneManager.GetActiveScene().name, StringComparison.OrdinalIgnoreCase))
            return;
        StartCoroutine(ApplySceneAnchorsNextFrame());
    }

    private void BuildRuntimeInstances()
    {
        runtimeById.Clear();

        for (int i = 0; i < npcs.Count; i++)
        {
            var entry = npcs[i];
            if (entry == null || !IsValidPrefabReference(entry.prefab))
            {
                if (entry != null && entry.prefab != null && !IsValidPrefabReference(entry.prefab))
                {
                    Debug.LogWarning(
                        $"NpcRuntimeBootstrap: entry {i} — Prefab tham chiếu không hợp lệ (Missing/destroyed). Gán lại prefab từ thư mục Project, không kéo object trong scene.",
                        this);
                }
                continue;
            }

            string id = ResolveEntryId(entry);
            if (string.IsNullOrWhiteSpace(id))
            {
                Debug.LogWarning($"NpcRuntimeBootstrap: entry {i} thiếu npcId.", this);
                continue;
            }

            if (runtimeById.ContainsKey(id))
            {
                Debug.LogWarning($"NpcRuntimeBootstrap: trùng npcId '{id}', bỏ qua entry {i}.", this);
                continue;
            }

            string profileId = SafeResolveNpcIdFromPrefab(entry.prefab);

            GameObject existing = FindExistingNpcInLoadedScenes(id)
                                 ?? (!string.IsNullOrEmpty(profileId) ? FindExistingNpcInLoadedScenes(profileId) : null);
            GameObject runtimeGo;
            try
            {
                runtimeGo = existing != null ? existing : Instantiate(entry.prefab);
            }
            catch (Exception ex)
            {
                Debug.LogError($"NpcRuntimeBootstrap: không Instantiate prefab entry {i} (id='{id}'). {ex.Message}", this);
                continue;
            }
            runtimeGo.name = entry.prefab.name;
            DontDestroyOnLoad(runtimeGo);
            runtimeById[id] = runtimeGo;
            if (!string.IsNullOrEmpty(profileId)
                && !string.Equals(id, profileId, StringComparison.OrdinalIgnoreCase))
            {
                runtimeById[profileId] = runtimeGo;
            }

            if (logBootstrap)
            {
                Debug.Log($"NpcRuntimeBootstrap: ready '{id}' -> {runtimeGo.name}", this);
            }
        }
    }

    private IEnumerator ApplySceneAnchorsNextFrame()
    {
        yield return null;

        string activeScene = SceneManager.GetActiveScene().name;
        var anchors = FindObjectsByType<NpcSceneAnchor2D>(FindObjectsInactive.Include, FindObjectsSortMode.None);

        for (int i = 0; i < npcs.Count; i++)
        {
            var entry = npcs[i];
            if (entry == null) continue;

            string id = ResolveEntryId(entry);
            if (string.IsNullOrWhiteSpace(id)) continue;
            if (!IsValidPrefabReference(entry.prefab)) continue;
            string profileIdLookup = SafeResolveNpcIdFromPrefab(entry.prefab);
            if (!runtimeById.TryGetValue(id, out GameObject go) || go == null)
            {
                if (string.IsNullOrEmpty(profileIdLookup) || !runtimeById.TryGetValue(profileIdLookup, out go) || go == null)
                    continue;
            }

            NpcSceneAnchor2D defaultAnchor = null;
            var matches = new List<NpcSceneAnchor2D>();
            for (int k = 0; k < anchors.Length; k++)
            {
                var anchor = anchors[k];
                if (anchor == null || !anchor.MatchesBootstrapOrProfile(id, profileIdLookup, activeScene)) continue;
                if (anchor.IsDefault) defaultAnchor = anchor;
                matches.Add(anchor);
            }

            ClassScheduleEntry sched = PhoneSystem.Instance != null ? PhoneSystem.Instance.GetTodaySchedule() : null;
            int hour = GameTimeManager.Instance != null ? GameTimeManager.Instance.Hour : 12;
            NpcSceneAnchor2D selected = SelectBestAnchor(matches, defaultAnchor, activeScene, hour, sched);
            selected = RefineBootstrapAnchorForNpcRoutine(selected, go, activeScene, hour, sched);

            if (selected == null)
            {
                var mover = go.GetComponent<NpcSimpleMover2D>();
                bool hasRoutine = mover != null && mover.HasActiveRoutineInScene(hour, activeScene);
                go.SetActive(entry.keepActiveWithoutAnchor || hasRoutine);
                if (mover != null)
                {
                    if (!mover.TakeSuppressBootstrapAnchorTeleport())
                    {
                        mover.MarkForceRefreshNext();
                        mover.RefreshSchedule();
                    }
                }
                continue;
            }

            go.SetActive(true);
            var runtimeMover = go.GetComponent<NpcSimpleMover2D>();
            bool skipTeleportFromPathResume = runtimeMover != null && runtimeMover.TakeSuppressBootstrapAnchorTeleport();
            if (!skipTeleportFromPathResume
                && runtimeMover != null
                && runtimeMover.ShouldSkipBootstrapAnchorForMovePathHead(hour, activeScene))
            {
                runtimeMover.MarkForceRefreshNext();
                runtimeMover.RefreshSchedule();
                skipTeleportFromPathResume = true;
            }

            if (!skipTeleportFromPathResume)
            {
                TeleportNpc(go, selected.transform.position);
                RefreshNpc(go);
            }
        }

        if (disableDuplicateSceneNpcs)
        {
            DisableDuplicateSceneNpcs(activeScene);
        }
    }

    /// <summary>
    /// Anchor theo lịch chung (tan học / trước giờ vào lớp) có thể trái với routine slot từng NPC
    /// (CLB tới 20h, chỉ có thị trấn lúc 19h...). Bỏ anchor nếu surface hiện tại không khớp lịch NPC.
    /// </summary>
    private static NpcSceneAnchor2D RefineBootstrapAnchorForNpcRoutine(
        NpcSceneAnchor2D selected,
        GameObject go,
        string activeScene,
        int hour,
        ClassScheduleEntry sched)
    {
        if (selected == null) return null;

        var mover = go.GetComponent<NpcSimpleMover2D>();
        if (mover == null) return selected;

        bool rTown = mover.HasActiveRoutineInScene(hour, "20_Town");
        bool rSchool = mover.HasActiveRoutineInScene(hour, "21_SchoolArea");
        bool rHere = mover.HasActiveRoutineInScene(hour, activeScene);

        if (!rHere)
        {
            if (string.Equals(activeScene, "20_Town", StringComparison.Ordinal) && rSchool && !rTown)
                return null;
            if (string.Equals(activeScene, "21_SchoolArea", StringComparison.Ordinal) && rTown && !rSchool)
                return null;

            // Routine chỉ thuộc scene khác (vd. 23_FriendHouse 19h–24h nhưng player đang ở Town) — không ghim anchor surface này.
            if (mover.HasRoutineSlotMatchingSchoolFilterAtHour(hour))
                return null;
        }

        if (string.Equals(activeScene, "20_Town", StringComparison.Ordinal)
            && selected.AnchorKind == NpcSceneAnchorKind.TownMorningHome
            && rSchool
            && !rTown)
            return null;

        if (string.Equals(activeScene, "21_SchoolArea", StringComparison.Ordinal)
            && selected.AnchorKind == NpcSceneAnchorKind.SchoolExit
            && sched != null)
        {
            int last = SchoolSchedulePhaseHelper.GetLastClassEndHour(sched);
            if (hour >= last && hour < 20 && mover.ShouldDeferSchoolExitBootstrapAnchor(hour, 20))
                return null;
        }

        return selected;
    }

    private static NpcSceneAnchor2D SelectBestAnchor(
        List<NpcSceneAnchor2D> matches,
        NpcSceneAnchor2D fallbackDefault,
        string activeScene,
        int hour,
        ClassScheduleEntry sched)
    {
        if (matches == null || matches.Count == 0) return null;
        if (matches.Count == 1) return matches[0];

        int first = SchoolSchedulePhaseHelper.GetFirstClassHour(sched);
        int last = SchoolSchedulePhaseHelper.GetLastClassEndHour(sched);
        NpcSceneAnchorKind want = NpcSceneAnchorKind.Default;
        if (string.Equals(activeScene, "20_Town", StringComparison.Ordinal))
        {
            if (hour >= 8 && hour < first) want = NpcSceneAnchorKind.TownMorningHome;
            else if (hour >= last && hour < 21) want = NpcSceneAnchorKind.TownFromSchool;
        }
        else if (string.Equals(activeScene, "21_SchoolArea", StringComparison.Ordinal))
        {
            if (hour >= 8 && hour < first) want = NpcSceneAnchorKind.SchoolEntrance;
            else if (hour >= last) want = NpcSceneAnchorKind.SchoolExit;
        }

        for (int i = 0; i < matches.Count; i++)
        {
            if (matches[i] != null && matches[i].AnchorKind == want) return matches[i];
        }

        if (fallbackDefault != null) return fallbackDefault;
        for (int i = 0; i < matches.Count; i++)
        {
            if (matches[i] != null && matches[i].IsDefault) return matches[i];
        }

        return matches[0];
    }

    private void DisableDuplicateSceneNpcs(string activeScene)
    {
        var sceneNpcs = FindObjectsByType<NpcCharacter>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < sceneNpcs.Length; i++)
        {
            var npc = sceneNpcs[i];
            if (npc == null) continue;
            if (!npc.gameObject.scene.IsValid() || !npc.gameObject.scene.isLoaded) continue;
            if (!string.Equals(npc.gameObject.scene.name, activeScene, StringComparison.Ordinal)) continue;

            string id = ResolveNpcIdFromCharacter(npc);
            if (string.IsNullOrWhiteSpace(id)) continue;
            if (!runtimeById.TryGetValue(id, out GameObject runtimeGo) || runtimeGo == null) continue;
            if (ReferenceEquals(runtimeGo, npc.gameObject)) continue;

            npc.gameObject.SetActive(false);
            if (logBootstrap)
            {
                Debug.Log($"NpcRuntimeBootstrap: disable duplicate scene NPC '{id}' in {activeScene}.", this);
            }
        }
    }

    private static void TeleportNpc(GameObject go, Vector3 worldPos)
    {
        go.transform.position = worldPos;
        var rb = go.GetComponent<Rigidbody2D>();
        if (rb != null)
        {
            rb.linearVelocity = Vector2.zero;
            rb.angularVelocity = 0f;
        }
    }

    private static void RefreshNpc(GameObject go)
    {
        var mover = go.GetComponent<NpcSimpleMover2D>();
        if (mover != null)
        {
            mover.MarkForceRefreshNext();
            mover.RefreshSchedule();
        }
    }

    private string ResolveEntryId(RuntimeNpcEntry entry)
    {
        if (!string.IsNullOrWhiteSpace(entry.npcId)) return entry.npcId.Trim();
        return SafeResolveNpcIdFromPrefab(entry.prefab);
    }

    /// <summary>Tránh MissingReference khi field Prefab là scene instance đã unload / reference Missing trong Inspector.</summary>
    private static bool IsValidPrefabReference(GameObject prefab)
    {
        if (prefab == null) return false;
        try
        {
            _ = prefab.transform;
            return true;
        }
        catch (MissingReferenceException)
        {
            return false;
        }
        catch (UnityEngine.UnityException)
        {
            return false;
        }
    }

    private static string SafeResolveNpcIdFromPrefab(GameObject prefab)
    {
        if (!IsValidPrefabReference(prefab))
            return string.Empty;
        try
        {
            var npc = prefab.GetComponent<NpcCharacter>();
            return ResolveNpcIdFromCharacter(npc);
        }
        catch (MissingReferenceException)
        {
            return string.Empty;
        }
        catch (UnityEngine.UnityException)
        {
            return string.Empty;
        }
    }

    private static string ResolveNpcIdFromCharacter(NpcCharacter npc)
    {
        if (npc == null || npc.Profile == null) return string.Empty;
        return npc.Profile.characterId != null ? npc.Profile.characterId.Trim() : string.Empty;
    }

    private static GameObject FindExistingNpcInLoadedScenes(string id)
    {
        var npcs = FindObjectsByType<NpcCharacter>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < npcs.Length; i++)
        {
            string cid = ResolveNpcIdFromCharacter(npcs[i]);
            if (string.Equals(cid, id, StringComparison.OrdinalIgnoreCase))
            {
                return npcs[i].gameObject;
            }
        }

        return null;
    }
}
