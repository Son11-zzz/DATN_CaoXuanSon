using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Di chuyển tuyến tính + routine theo giờ, resolve điểm qua <see cref="NpcScenePointRegistry"/>.</summary>
[DisallowMultipleComponent]
[DefaultExecutionOrder(-150)]
public class NpcSimpleMover2D : MonoBehaviour
{
    [Header("Dữ liệu")]
    [SerializeField] private NpcSimpleRoutineData routineData;

    [Tooltip("Dùng khi không có NpcCharacter (vd. quầy bán hàng).")]
    [SerializeField] private NpcRoutineArchetype archetypeOverride = NpcRoutineArchetype.Classmate;

    [Header("Hệ")]
    [SerializeField] private NpcWorldPresence2D worldPresence;
    [SerializeField] private SpriteRenderer spriteRenderer;
    [SerializeField] private Rigidbody2D rb2d;
    [SerializeField] private Animator optionalAnimator;
    [SerializeField] private bool driveAnimator;
    [SerializeField] private bool flipByDirection = true;
    [SerializeField] private bool facingRightByDefault = true;
    [SerializeField] private bool pauseDuringDialogue = true;
    [SerializeField] private float arriveDistance = 0.1f;
    [Tooltip("Tên tham số blend tree (nếu có).")]
    [SerializeField] private string animatorHorizontal = "Horizontal";
    [SerializeField] private string animatorVertical = "Vertical";
    [SerializeField] private string animatorIsWalking = "isWalking";

    [Tooltip("Chuẩn hoá vector di chuyển (-1..1) cho Animator — tránh blend tree không đổi clip khi mỗi frame chỉ lệch vài pixel.")]
    [SerializeField] private bool normalizeAnimatorMoveDirection = true;

    [Tooltip("Nếu blend chỉ có clip đi ngang một phía (vd. Walk_Right): truyền |Horizontal|, trái/phải nhờ flip sprite (ApplyFacing). Tắt nếu Animator dùng Horizontal âm/dương cho trái/phải riêng.")]
    [SerializeField] private bool animatorSideMirrorViaSpriteFlip = true;

    [Header("Idle (đứng yên / hết path)")]
    [Tooltip("Blend tree khi không di chuyển (thường Y âm = idle hướng về camera / idle down).")]
    [SerializeField] private Vector2 idleAnimatorBlend = new Vector2(0f, -1f);

    [Tooltip("Khi dừng, reset flip sprite về trạng thái mặc định (tránh giữ flip từ bước chạy ngang).")]
    [SerializeField] private bool resetSpriteFlipForIdle = true;

    [Tooltip("Giá trị SpriteRenderer.flipX khi idle (nếu resetSpriteFlipForIdle bật).")]
    [SerializeField] private bool idleSpriteFlipX;

    [Tooltip("Quest/cutscene: tắt tự cập nhật routine.")]
    [SerializeField] private bool externalControl;

    [Tooltip("Khi rời rồi quay lại cùng scene cùng giờ, tiếp tục path từ vị trí/điểm đã lưu (Single load — unload scene tạm lưu).")]
    [SerializeField] private bool resumePathOnReturnToScene = true;

    private NpcCharacterProfile _profile;
    private NpcRoutineArchetype EffectiveArchetype =>
        _profile != null ? _profile.routineArchetype : archetypeOverride;
    private readonly List<Transform> _pathTransforms = new List<Transform>(8);
    /// <summary>Waypoint cache khi scene unload — Transform bị destroy nhưng NPC DDOL vẫn đi path.</summary>
    private readonly List<Vector3> _pathWorldPositions = new List<Vector3>(8);
    private NpcRoutineSlot _activePathSlot;
    private int _pathIndex;
    private bool _pathCompletionHandled;
    private int _lastHourEvaluated = int.MinValue;
    private bool _forceNextRefresh = true;
    private bool _vendorHoming;

    /// <summary>Scene gameplay nơi MovePath đang chạy — dùng khi slot.sceneName rỗng (wildcard).</summary>
    private string _movePathBoundSceneName = string.Empty;

    private bool _suppressNextBootstrapAnchorTeleport;

    /// <summary>Ngày (DayInSemester) mà NPC đã hoàn thành path “ẩn khi xong” trong ngày — khi không còn slot khớp giờ/scene vẫn giữ ẩn thay vì hiện lại.</summary>
    private int _hiddenDueToCompletedPathDay = -1;

    private static readonly System.Collections.Generic.Dictionary<string, PathResumeEntry> SPathResume =
        new System.Collections.Generic.Dictionary<string, PathResumeEntry>(StringComparer.OrdinalIgnoreCase);

    private struct PathResumeEntry
    {
        public int hour;
        public int dayInSemester;
        public int pathIndex;
        public Vector3 worldPos;
        public string slotFingerprint;
    }

    /// <summary>Đã đi hết path trong ngày — tránh re-spawn / re-walk khi quay lại scene.</summary>
    private struct CompletedSlotEntry
    {
        public int day;
        public Vector3 worldPos;
        public bool hidden;
    }

    private static readonly System.Collections.Generic.Dictionary<string, CompletedSlotEntry> SCompletedSlot =
        new System.Collections.Generic.Dictionary<string, CompletedSlotEntry>(StringComparer.OrdinalIgnoreCase);

    private static string MakeCompletionKey(string id, string slotFingerprint)
    {
        return $"{id}##{slotFingerprint}";
    }

    private static string MakePathResumeStorageKey(string npcId, string slotFingerprint)
    {
        if (string.IsNullOrEmpty(npcId)) return string.Empty;
        return string.IsNullOrEmpty(slotFingerprint)
            ? npcId + "##"
            : npcId + "##" + slotFingerprint;
    }

    private static void RemoveAllPathResumeForNpc(string npcId)
    {
        if (string.IsNullOrEmpty(npcId) || SPathResume.Count == 0) return;
        string prefix = npcId + "##";
        var keysToRemove = new List<string>();
        foreach (var kv in SPathResume)
        {
            string k = kv.Key;
            if (string.Equals(k, npcId, StringComparison.OrdinalIgnoreCase)
                || k.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
                keysToRemove.Add(k);
        }

        for (int i = 0; i < keysToRemove.Count; i++)
            SPathResume.Remove(keysToRemove[i]);
    }

    public bool ExternalControl
    {
        get => externalControl;
        set => externalControl = value;
    }

    public NpcSimpleRoutineData RoutineData
    {
        get => routineData;
        set => routineData = value;
    }

    private void Awake()
    {
        if (worldPresence == null) worldPresence = GetComponent<NpcWorldPresence2D>();
        if (spriteRenderer == null) spriteRenderer = GetComponent<SpriteRenderer>();
        if (rb2d == null) rb2d = GetComponent<Rigidbody2D>();
        if (optionalAnimator == null) optionalAnimator = GetComponent<Animator>();
        var ch = GetComponent<NpcCharacter>();
        if (ch != null) _profile = ch.Profile;
        if (_profile != null && _profile.routineData != null) routineData = _profile.routineData;
        SceneManager.sceneLoaded += OnSceneLoaded;
        SceneManager.sceneUnloaded += OnSceneUnloaded;
    }

    private string GetNpcIdKey()
    {
        if (_profile != null && !string.IsNullOrEmpty(_profile.characterId)) return _profile.characterId.Trim();
        return name;
    }

    /// <summary>
    /// Một lần: true nếu vừa phục hồi vị trí từ path resume — bootstrap bỏ Teleport/Refresh thứ hai (tránh ghi đè resume / reset về index 0).
    /// </summary>
    public bool TakeSuppressBootstrapAnchorTeleport()
    {
        if (!_suppressNextBootstrapAnchorTeleport) return false;
        _suppressNextBootstrapAnchorTeleport = false;
        return true;
    }

    /// <summary>
    /// Routine có “ý nghĩa” trong scene hiện tại khi không có anchor — chỉ DDOL surface được phép,
    /// slot sceneName rỗng chỉ được hiểu Town/School/vendor (24_*); Menu/Nhà player (22_*) vẫn loại trừ.
    /// </summary>
    public bool HasActiveRoutineInScene(int hour, string activeScene)
    {
        if (routineData == null) return false;
        if (externalControl) return false;
        if (IsGameplaySurfaceBlockedForRuntimeNpc(activeScene))
            return false;

        if (EffectiveArchetype == NpcRoutineArchetype.StationaryVendor)
        {
            if (!IsVendorWorldScene(activeScene))
                return false;
            if (WouldApplyDynamicLunch(hour, activeScene, out _))
                return true;
            return hour >= routineData.vendorShopOpenHour && hour < routineData.vendorShopCloseHour;
        }

        if (WouldApplyDynamicLunch(hour, activeScene, out _))
            return true;

        NpcRoutineSlot[] table = IsSchoolDay() ? routineData.schoolDaySlots : routineData.noSchoolSlots;
        if (table == null)
            return ShouldHoldMovePathWhileOffRoutineSurface(hour, activeScene);
        for (int i = 0; i < table.Length; i++)
        {
            if (table[i] != null && SlotMatches(table[i], hour, activeScene)) return true;
        }

        return ShouldHoldMovePathWhileOffRoutineSurface(hour, activeScene);
    }

    /// <summary>
    /// Có slot khớp giờ + filter (ngày học/ngày nghỉ), không xét scene — dùng bootstrap để biết NPC có lịch
    /// ở mốc giờ này dù không thuộc surface hiện tại (vd. chỉ ở <c>23_FriendHouse</c> lúc 21h).
    /// </summary>
    public bool HasRoutineSlotMatchingSchoolFilterAtHour(int hour)
    {
        if (routineData == null || externalControl) return false;
        if (EffectiveArchetype == NpcRoutineArchetype.StationaryVendor) return false;

        NpcRoutineSlot[] table = IsSchoolDay() ? routineData.schoolDaySlots : routineData.noSchoolSlots;
        if (table == null) return false;
        for (int i = 0; i < table.Length; i++)
        {
            if (table[i] != null && RoutineSlotHourAndSchoolFilterMatches(table[i], hour))
                return true;
        }

        return false;
    }

    /// <summary>
    /// Bỏ teleport anchor: cơm trưa động (đã có điểm riêng) hoặc slot “hẹp nhất” khớp surface là MovePath một lượt —
    /// vị trí phải là waypoint đầu, không phải TownFromSchool / SchoolExit.
    /// </summary>
    public bool ShouldSkipBootstrapAnchorForMovePathHead(int hour, string activeScene)
    {
        if (routineData == null || externalControl) return false;
        if (IsGameplaySurfaceBlockedForRuntimeNpc(activeScene)) return false;
        if (EffectiveArchetype == NpcRoutineArchetype.StationaryVendor) return false;
        if (WouldApplyDynamicLunch(hour, activeScene, out _))
            return true;

        NpcRoutineSlot[] table = IsSchoolDay() ? routineData.schoolDaySlots : routineData.noSchoolSlots;
        if (table == null || table.Length == 0) return false;

        NpcRoutineSlot chosen = null;
        int chosenSpan = int.MaxValue;
        for (int i = 0; i < table.Length; i++)
        {
            var s = table[i];
            if (s == null || !SlotMatches(s, hour, activeScene)) continue;
            int span = s.endHour - s.startHour;
            if (span <= 0) span = 24;
            if (chosen == null || span < chosenSpan)
            {
                chosen = s;
                chosenSpan = span;
            }
        }

        if (chosen == null) return false;
        if (chosen.action != NpcSimpleRoutineAction.MovePath) return false;
        if (chosen.loop) return false;
        return chosen.pathPointIds != null && chosen.pathPointIds.Length > 0;
    }

    /// <summary>
    /// Sau giờ tan học chính nhưng vẫn trong slot sân trường kéo dài tới ≥ cutoff (vd. CLB 16–20):
    /// không dùng anchor <see cref="NpcSceneAnchorKind.SchoolExit"/> ép vị trí lối ra.
    /// Slot bắt đầu lúc ≥ cutoff (vd. To_exit 20–21) vẫn dùng anchor exit bình thường.
    /// </summary>
    public bool ShouldDeferSchoolExitBootstrapAnchor(int hour, int eveningRoutineCutoffHour = 20)
    {
        if (routineData == null || externalControl) return false;
        if (IsGameplaySurfaceBlockedForRuntimeNpc("21_SchoolArea"))
            return false;

        if (EffectiveArchetype == NpcRoutineArchetype.StationaryVendor)
            return false;

        NpcRoutineSlot[] table = IsSchoolDay() ? routineData.schoolDaySlots : routineData.noSchoolSlots;
        if (table == null) return false;

        const string school = "21_SchoolArea";
        for (int i = 0; i < table.Length; i++)
        {
            var s = table[i];
            if (s == null || !SlotMatches(s, hour, school)) continue;
            if (s.startHour >= eveningRoutineCutoffHour) continue;
            int end = s.endHour > 24 ? 24 : s.endHour;
            if (s.endHour <= 0) continue;
            if (end >= eveningRoutineCutoffHour) return true;
        }

        return false;
    }

    private static readonly string[] GameplayBlockedScenePrefixes =
    {
        "00_", "10_", "22_", "70_", "90_",
    };

    /// <summary>
    /// Surface cho phép giữ MovePath khi player sang scene khác trong nhóm hub/interior:
    /// <c>20_Town</c>, <c>21_SchoolArea</c>, <c>22_*</c> nhà người chơi (<see cref="IsGameplaySurfaceBlockedForRuntimeNpc"/> vẫn chặn routine tại chỗ —
    /// chỉ dùng để không <see cref="ClearPath"/> khi chờ quay về hub), <c>23_FriendHouse</c>, cửa hàng <c>24_*</c>.
    /// </summary>
    private static bool IsNpcRoutineCrossTravelSurface(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName)) return false;
        return sceneName.StartsWith("20_", StringComparison.OrdinalIgnoreCase)
               || sceneName.StartsWith("21_", StringComparison.OrdinalIgnoreCase)
               || sceneName.StartsWith("22_", StringComparison.OrdinalIgnoreCase)
               || sceneName.StartsWith("23_", StringComparison.OrdinalIgnoreCase)
               || sceneName.StartsWith("24_", StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsGameplaySurfaceBlockedForRuntimeNpc(string scene)
    {
        if (string.IsNullOrEmpty(scene)) return true;
        for (int p = 0; p < GameplayBlockedScenePrefixes.Length; p++)
        {
            if (scene.StartsWith(GameplayBlockedScenePrefixes[p], StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    /// <summary>Slot sceneName rỗng (data cũ) không còn = mọi scene — chỉ Town / School và quầy Vendor.</summary>
    private static bool WildcardsEmptySceneNameForDdolPresence(string scene, NpcRoutineArchetype arch)
    {
        if (string.IsNullOrEmpty(scene)) return false;
        if (arch == NpcRoutineArchetype.StationaryVendor)
        {
            return scene.StartsWith("20_", StringComparison.OrdinalIgnoreCase)
                   || scene.StartsWith("21_", StringComparison.OrdinalIgnoreCase)
                   || scene.StartsWith("24_", StringComparison.OrdinalIgnoreCase);
        }
        return scene.StartsWith("20_", StringComparison.OrdinalIgnoreCase)
               || scene.StartsWith("21_", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Quầy hàng chỉ hiển thị ở thị trấn / cửa hàng — không phải sân trường.</summary>
    private static bool IsVendorWorldScene(string scene)
    {
        if (string.IsNullOrEmpty(scene)) return false;
        return scene.StartsWith("20_", StringComparison.OrdinalIgnoreCase)
               || scene.StartsWith("24_", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildSlotFingerprint(NpcRoutineSlot s)
    {
        if (s == null) return string.Empty;
        var ids = s.pathPointIds == null ? string.Empty : string.Join("|", s.pathPointIds);
        return $"{s.startHour}_{s.endHour}_{s.sceneName}_{(int)s.action}_{(int)s.scheduleFilter}_{ids}";
    }

    /// <summary>Scene để khớp unload/persist — slot có scene cố định hoặc wildcard + scene đang đi path.</summary>
    private string GetMovePathSceneBindingForPersist()
    {
        if (_activePathSlot == null) return string.Empty;
        if (!string.IsNullOrEmpty(_activePathSlot.sceneName))
            return _activePathSlot.sceneName.Trim();
        return _movePathBoundSceneName ?? string.Empty;
    }

    /// <summary>
    /// Player sang scene khác trong nhóm cross-travel (Town / School / FriendHouse / cửa hàng 24_*)
    /// nhưng NPC đang MovePath trên surface khác — giữ polyline + slot để <see cref="Update"/> tiếp tục như khi surface gốc đang load.
    /// </summary>
    private bool ShouldHoldMovePathWhileOffRoutineSurface(int hour, string activeScene)
    {
        if (!resumePathOnReturnToScene || routineData == null || externalControl) return false;
        if (EffectiveArchetype == NpcRoutineArchetype.StationaryVendor) return false;
        if (_activePathSlot == null || _activePathSlot.action != NpcSimpleRoutineAction.MovePath || _pathCompletionHandled)
            return false;
        if (!RoutineSlotHourAndSchoolFilterMatches(_activePathSlot, hour)) return false;

        string bind = GetMovePathSceneBindingForPersist();
        if (string.IsNullOrEmpty(bind)) return false;
        if (string.Equals(bind, activeScene, StringComparison.OrdinalIgnoreCase)) return false;

        if (_pathWorldPositions.Count == 0 && _pathTransforms.Count == 0) return false;

        return IsNpcRoutineCrossTravelSurface(activeScene)
               && IsNpcRoutineCrossTravelSurface(bind);
    }

    private void OnEnable()
    {
        if (GameTimeManager.Instance != null)
        {
            GameTimeManager.Instance.OnTimeChanged -= HandleTimeChanged;
            GameTimeManager.Instance.OnTimeChanged += HandleTimeChanged;
        }
        _lastHourEvaluated = int.MinValue;
        _forceNextRefresh = true;
        RefreshSchedule();
    }

    private void OnDisable()
    {
        if (GameTimeManager.Instance != null)
        {
            GameTimeManager.Instance.OnTimeChanged -= HandleTimeChanged;
        }
    }

    private void OnDestroy()
    {
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneUnloaded -= OnSceneUnloaded;
        if (GameTimeManager.Instance != null)
        {
            GameTimeManager.Instance.OnTimeChanged -= HandleTimeChanged;
        }
    }

    private void OnSceneUnloaded(Scene s)
    {
        if (!resumePathOnReturnToScene) return;
        if (_activePathSlot == null) return;
        if (_activePathSlot.action != NpcSimpleRoutineAction.MovePath) return;
        if (_pathCompletionHandled) return;
        string binding = GetMovePathSceneBindingForPersist();
        if (string.IsNullOrEmpty(binding)) return;
        if (!string.Equals(binding, s.name, StringComparison.OrdinalIgnoreCase)) return;
        int hour = GameTimeManager.Instance != null ? GameTimeManager.Instance.Hour : 0;
        PersistMovePathResumeCore(hour);
    }

    /// <summary>
    /// Race: <see cref="OnSceneLoaded"/> gọi <see cref="RefreshSchedule"/> và có thể <see cref="ClearPath"/>
    /// trước khi nhận <see cref="OnSceneUnloaded"/> — không còn path để lưu.
    /// Gọi thêm nhánh này khi scene của slot không còn load (đã rời scene).
    /// </summary>
    private void TryPersistIncompleteMovePathIfSlotSceneNotLoaded(int scheduleHour)
    {
        if (!resumePathOnReturnToScene) return;
        if (_activePathSlot == null || _activePathSlot.action != NpcSimpleRoutineAction.MovePath || _pathCompletionHandled) return;
        string slotSn = GetMovePathSceneBindingForPersist();
        if (string.IsNullOrEmpty(slotSn)) return;
        string activeSn = SceneManager.GetActiveScene().name?.Trim() ?? string.Empty;
        bool stillInsideSlotScene =
            string.Equals(activeSn, slotSn, StringComparison.OrdinalIgnoreCase)
            && IsSceneWithNameLoaded(slotSn);
        if (stillInsideSlotScene) return;
        PersistMovePathResumeCore(scheduleHour);
    }

    private static bool IsSceneWithNameLoaded(string sceneName)
    {
        if (string.IsNullOrEmpty(sceneName)) return false;
        var sc = SceneManager.GetSceneByName(sceneName);
        return sc.IsValid() && sc.isLoaded;
    }

    private void PersistMovePathResumeCore(int scheduleHour)
    {
        if (_activePathSlot == null || _activePathSlot.action != NpcSimpleRoutineAction.MovePath) return;
        if (_pathCompletionHandled) return;
        string id = GetNpcIdKey();
        if (string.IsNullOrEmpty(id)) return;
        int pathLen = _pathWorldPositions.Count > 0 ? _pathWorldPositions.Count : _pathTransforms.Count;
        int maxIdx = pathLen > 0 ? pathLen - 1 : 0;
        int idx = Mathf.Clamp(_pathIndex, 0, maxIdx);
        int dayInSemester = GameTimeManager.Instance != null ? GameTimeManager.Instance.DayInSemester : 0;
        string fp = BuildSlotFingerprint(_activePathSlot);
        string key = MakePathResumeStorageKey(id, fp);
        SPathResume[key] = new PathResumeEntry
        {
            hour = scheduleHour,
            dayInSemester = dayInSemester,
            pathIndex = idx,
            worldPos = transform.position,
            slotFingerprint = fp
        };
    }

    private void OnSceneLoaded(Scene s, LoadSceneMode m)
    {
        // LoadScene Routine: gameplay Single → sceneLoaded(gameplay); rồi UI Additive → sceneLoaded(ui).
        // Lần UI không đổi active scene nhưng vẫn gọy callback → Refresh lần 2 làm tiêu SPathResume / reset index.
        if (ShouldIgnoreNpcRefreshForLoadedScene(s, m))
            return;

        MarkForceRefreshNext();
        // Bị Ẩn ở Town (path xong) rồi sang 21: bật hiện trước khi slot chạy (không bắt buộc IsSchoolDay).
        if (IsSchoolAreaSceneName(s.name)
            && routineData != null
            && EffectiveArchetype != NpcRoutineArchetype.StationaryVendor)
        {
            ApplyWorldHidden(false);
        }
        RefreshSchedule();
    }

    /// <summary>Gameplay load Single trước, UI load Additive sau — callback thứ 2 không đổi active scene nhưng vẫn reset routine.</summary>
    private static bool ShouldIgnoreNpcRefreshForLoadedScene(Scene s, LoadSceneMode m)
    {
        if (m != LoadSceneMode.Additive) return false;
        string loaded = s.name?.Trim() ?? string.Empty;
        string active = SceneManager.GetActiveScene().name?.Trim() ?? string.Empty;
        return !string.Equals(loaded, active, StringComparison.OrdinalIgnoreCase);
    }

    private static bool IsSchoolAreaSceneName(string name)
    {
        return !string.IsNullOrEmpty(name)
               && string.Equals(name.Trim(), "21_SchoolArea", StringComparison.Ordinal);
    }

    private void HandleTimeChanged()
    {
        RefreshSchedule();
    }

    private void Update()
    {
        if (externalControl) return;
        if (PauseMovement()) return;
        if (routineData == null) return;
        if (EffectiveArchetype == NpcRoutineArchetype.StationaryVendor && !_vendorHoming) return;
        int pathLen = _pathWorldPositions.Count > 0 ? _pathWorldPositions.Count : _pathTransforms.Count;
        if (pathLen == 0) return;
        if (_pathIndex >= pathLen) return;

        Vector3 target;
        if (_pathWorldPositions.Count > _pathIndex)
            target = _pathWorldPositions[_pathIndex];
        else if (_pathIndex < _pathTransforms.Count && _pathTransforms[_pathIndex] != null)
            target = _pathTransforms[_pathIndex].position;
        else
            return;
        float speed = _activePathSlot != null ? _activePathSlot.moveSpeed : 1.5f;
        var pos = transform.position;
        Vector3 next = Vector3.MoveTowards(pos, target, speed * Time.deltaTime);
        if (rb2d != null) rb2d.MovePosition(next);
        else transform.position = next;
        var delta = (Vector2)(next - pos);
        bool moving = delta.sqrMagnitude > 0.0001f;
        if (moving)
        {
            Vector2 dirUnit = delta.normalized;
            ApplyFacing(dirUnit);
            Vector2 animBlend = normalizeAnimatorMoveDirection ? dirUnit : delta;
            if (animatorSideMirrorViaSpriteFlip)
            {
                Vector2 n = normalizeAnimatorMoveDirection ? dirUnit : delta.normalized;
                animBlend = new Vector2(Mathf.Abs(n.x), n.y);
            }

            SetAnimatorMove(animBlend, true);
        }
        else
        {
            SetAnimatorMove(Vector2.zero, false);
        }

        if ((next - target).sqrMagnitude < arriveDistance * arriveDistance) AdvancePathIndex();
    }

    private void AdvancePathIndex()
    {
        int pathLen = _pathWorldPositions.Count > 0 ? _pathWorldPositions.Count : _pathTransforms.Count;
        if (pathLen == 0) return;
        _pathIndex++;
        if (_pathIndex < pathLen) return;
        if (_activePathSlot != null
            && _activePathSlot.loop
            && pathLen > 0)
        {
            _pathIndex = 0;
            return;
        }
        OnPathCompleted();
    }

    private void OnPathCompleted()
    {
        if (_pathCompletionHandled) return;
        _pathCompletionHandled = true;
        string id = GetNpcIdKey();
        RemoveAllPathResumeForNpc(id);
        bool hide = _activePathSlot != null && _activePathSlot.hideWhenPathComplete;
        if (hide)
        {
            ApplyWorldHidden(true);
            _hiddenDueToCompletedPathDay = GameTimeManager.Instance != null ? GameTimeManager.Instance.DayInSemester : -1;
        }
        else _hiddenDueToCompletedPathDay = -1;

        if (_activePathSlot != null
            && _activePathSlot.action == NpcSimpleRoutineAction.MovePath
            && !string.IsNullOrEmpty(id))
        {
            int day = GameTimeManager.Instance != null ? GameTimeManager.Instance.DayInSemester : 0;
            string key = MakeCompletionKey(id, BuildSlotFingerprint(_activePathSlot));
            SCompletedSlot[key] = new CompletedSlotEntry
            {
                day = day,
                worldPos = transform.position,
                hidden = hide
            };
        }

        if (EffectiveArchetype == NpcRoutineArchetype.StationaryVendor) _vendorHoming = false;

        ApplyIdlePresentation();
    }

    private void ApplyWorldHidden(bool hidden)
    {
        if (worldPresence != null) worldPresence.SetWorldHidden(hidden);
        else
        {
            foreach (var col in GetComponentsInChildren<Collider2D>(true))
            {
                if (col != null) col.enabled = !hidden;
            }
            foreach (var sr in GetComponentsInChildren<SpriteRenderer>(true))
            {
                if (sr != null) sr.enabled = !hidden;
            }
        }
    }

    private void ApplyFacing(Vector2 dir)
    {
        if (!flipByDirection || spriteRenderer == null) return;
        if (dir.x < -0.01f) spriteRenderer.flipX = !facingRightByDefault;
        else if (dir.x > 0.01f) spriteRenderer.flipX = facingRightByDefault;
    }

    private void SetAnimatorMove(Vector2 dir, bool moving)
    {
        if (optionalAnimator == null || !driveAnimator) return;
        if (optionalAnimator.runtimeAnimatorController == null) return;
        Vector2 blend = moving ? dir : idleAnimatorBlend;
        foreach (var p in optionalAnimator.parameters)
        {
            if (p.name == animatorHorizontal) optionalAnimator.SetFloat(p.name, blend.x);
            else if (p.name == animatorVertical) optionalAnimator.SetFloat(p.name, blend.y);
            else if (p.name == animatorIsWalking) optionalAnimator.SetBool(p.name, moving);
        }
    }

    private void ApplyIdlePresentation()
    {
        SetAnimatorMove(Vector2.zero, false);
        bool skipIdleFlipReset = _profile != null
            && string.Equals(_profile.characterId, "npc_ThayGiaoBa", StringComparison.Ordinal);
        if (resetSpriteFlipForIdle && !skipIdleFlipReset && spriteRenderer != null && flipByDirection)
            spriteRenderer.flipX = idleSpriteFlipX;
    }

    private bool PauseMovement()
    {
        if (!pauseDuringDialogue) return false;
        return DialogueSystem.Instance != null && DialogueSystem.Instance.IsDialogueActive;
    }

    public void RefreshSchedule()
    {
        if (externalControl) return;
        if (routineData == null) return;

        int hour = GameTimeManager.Instance != null ? GameTimeManager.Instance.Hour : 12;
        int semesterDay = GameTimeManager.Instance != null ? GameTimeManager.Instance.DayInSemester : 0;
        if (_hiddenDueToCompletedPathDay >= 0 && _hiddenDueToCompletedPathDay != semesterDay)
            _hiddenDueToCompletedPathDay = -1;

        TryPersistIncompleteMovePathIfSlotSceneNotLoaded(hour);

        if (!isActiveAndEnabled) return;
        if (_lastHourEvaluated == hour && !_forceNextRefresh) return;
        _lastHourEvaluated = hour;
        _forceNextRefresh = false;
        if (EffectiveArchetype == NpcRoutineArchetype.StationaryVendor)
        {
            ApplyVendor(hour);
            return;
        }
        string scene = SceneManager.GetActiveScene().name;
        if (TryApplyDynamicLunch(hour, scene)) return;
        NpcRoutineSlot[] table = IsSchoolDay() ? routineData.schoolDaySlots : routineData.noSchoolSlots;
        if (table == null || table.Length == 0)
        {
            if (_hiddenDueToCompletedPathDay >= 0 && _hiddenDueToCompletedPathDay == semesterDay)
            {
                ClearPath();
                ApplyWorldHidden(true);
                ApplyIdlePresentation();
                return;
            }

            if (ShouldHoldMovePathWhileOffRoutineSurface(hour, scene))
            {
                _suppressNextBootstrapAnchorTeleport = true;
                return;
            }

            ClearPath();
            ApplyIdlePresentation();
            return;
        }
        // Nhiều slot có thể cùng khớp giờ + scene (vd. 16–20 CLB vs 16–18 thoát lớp). Ưu tiên cửa sổ hẹp hơn.
        NpcRoutineSlot chosen = null;
        int chosenSpan = int.MaxValue;
        for (int i = 0; i < table.Length; i++)
        {
            var s = table[i];
            if (s == null) continue;
            if (!SlotMatches(s, hour, scene)) continue;
            if (ShouldPreserveActiveMovePath(s))
            {
                _suppressNextBootstrapAnchorTeleport = true;
                return;
            }

            int span = s.endHour - s.startHour;
            if (span <= 0) span = 24;
            if (chosen == null || span < chosenSpan)
            {
                chosen = s;
                chosenSpan = span;
            }
        }

        if (chosen != null)
        {
            ApplySlot(chosen, hour);
            return;
        }

        if (_hiddenDueToCompletedPathDay >= 0 && _hiddenDueToCompletedPathDay == semesterDay)
        {
            ClearPath();
            ApplyWorldHidden(true);
            ApplyIdlePresentation();
            return;
        }

        if (ShouldHoldMovePathWhileOffRoutineSurface(hour, scene))
        {
            _suppressNextBootstrapAnchorTeleport = true;
            return;
        }

        ClearPath();
        ApplyIdlePresentation();
    }

    /// <summary>
    /// Cùng slot MovePath vẫn khớp nhiều giờ (vd. To_Club 16–19). Không gọi lại <see cref="ApplySlot"/> mỗi lần đổi giờ /
    /// <see cref="MarkForceRefreshNext"/> — tránh reset <see cref="_pathIndex"/> về 0 (NPC chạy lại từ waypoint đầu).
    /// Khi đã đi hết path, khớp cùng fingerprint thì guard tiếp: đứng yên đến khi giờ/slot fingerprint khác (vd. thoát trường lúc 20h).
    /// </summary>
    private bool ShouldPreserveActiveMovePath(NpcRoutineSlot nextSlot)
    {
        if (_activePathSlot == null || nextSlot == null) return false;
        if (_activePathSlot.action != NpcSimpleRoutineAction.MovePath
            || nextSlot.action != NpcSimpleRoutineAction.MovePath)
            return false;
        string activeFp = BuildSlotFingerprint(_activePathSlot);
        if (string.IsNullOrEmpty(activeFp))
            return false;
        if (!string.Equals(activeFp, BuildSlotFingerprint(nextSlot), StringComparison.Ordinal))
            return false;

        int pathLen = _pathWorldPositions.Count > 0 ? _pathWorldPositions.Count : _pathTransforms.Count;
        if (pathLen == 0)
            return false;

        bool finished = _pathCompletionHandled || _pathIndex >= pathLen;
        bool inProgress = !finished && _pathIndex >= 0 && _pathIndex < pathLen;

        return inProgress || finished;
    }

    public void MarkForceRefreshNext()
    {
        _forceNextRefresh = true;
    }

    private void ClearPath()
    {
        _pathTransforms.Clear();
        _pathWorldPositions.Clear();
        _activePathSlot = null;
        _pathIndex = 0;
        _movePathBoundSceneName = string.Empty;
    }

    private static bool IsHourInWindow(int h, NpcRoutineSlot s)
    {
        if (s.endHour <= 0) return false;
        int end = s.endHour > 24 ? 24 : s.endHour;
        return h >= s.startHour && h < end;
    }

    private bool SkipClassToday()
    {
        return StoryEventManager.Instance != null
               && StoryEventManager.Instance.ActiveEvent != null
               && StoryEventManager.Instance.ActiveEvent.skipClassAttendanceToday;
    }

    private bool IsSchoolDay()
    {
        if (SkipClassToday()) return false;
        if (routineData == null) return false;
        int day = GameTimeManager.Instance != null ? GameTimeManager.Instance.DayInSemester : 1;
        if (routineData.activeSchoolDayOverride != null && routineData.activeSchoolDayOverride.Length > 0)
        {
            for (int i = 0; i < routineData.activeSchoolDayOverride.Length; i++)
            {
                if (routineData.activeSchoolDayOverride[i] == day) return true;
            }
            return false;
        }
        var sched = PhoneSystem.Instance != null ? PhoneSystem.Instance.GetTodaySchedule() : null;
        return SchoolSchedulePhaseHelper.HasClassBlocks(sched);
    }

    private bool RoutineSlotHourAndSchoolFilterMatches(NpcRoutineSlot s, int hour)
    {
        if (!IsHourInWindow(hour, s)) return false;
        bool school = IsSchoolDay();
        switch (s.scheduleFilter)
        {
            case NpcRoutineScheduleFilter.SchoolDayOnly: if (!school) return false; break;
            case NpcRoutineScheduleFilter.NoSchoolOnly: if (school) return false; break;
        }
        return true;
    }

    /// <summary>Khớp giờ + filter; sceneName rỗng chỉ khớp Town / School (vendor thêm 24_*); nhà bạn 23_* không wildcard — chỉ cross-travel MovePath.</summary>
    private bool SlotMatches(NpcRoutineSlot s, int hour, string scene)
    {
        if (!RoutineSlotHourAndSchoolFilterMatches(s, hour)) return false;
        if (!string.IsNullOrEmpty(s.sceneName))
            return string.Equals(scene, s.sceneName.Trim(), StringComparison.OrdinalIgnoreCase);
        return WildcardsEmptySceneNameForDdolPresence(scene, EffectiveArchetype);
    }

    private bool WouldApplyDynamicLunch(int hour, string activeScene, out Transform lunchPoint)
    {
        lunchPoint = null;
        if (routineData == null) return false;
        if (!routineData.useDynamicLunchFromPhone) return false;
        if (string.IsNullOrEmpty(activeScene) || !string.Equals(activeScene, "21_SchoolArea", StringComparison.Ordinal)) return false;
        if (!IsSchoolDay()) return false;
        var sched = PhoneSystem.Instance != null ? PhoneSystem.Instance.GetTodaySchedule() : null;
        if (sched == null) return false;
        if (SchoolSchedulePhaseHelper.GetPhase(hour, sched, SkipClassToday()) != SchoolSchedulePhase.Lunch) return false;
        string lunchId;
        if (EffectiveArchetype == NpcRoutineArchetype.Teacher)
        {
            lunchId = string.IsNullOrEmpty(routineData.lunchOfficePointId) ? routineData.lunchCanteenPointId : routineData.lunchOfficePointId;
        }
        else
        {
            lunchId = string.IsNullOrEmpty(routineData.lunchCanteenPointId) ? routineData.lunchOfficePointId : routineData.lunchCanteenPointId;
        }
        if (string.IsNullOrEmpty(lunchId) || !NpcScenePointRegistry.TryGetPointInLoadedScenes(lunchId, out lunchPoint) || lunchPoint == null) return false;
        return true;
    }

    private bool TryApplyDynamicLunch(int hour, string activeScene)
    {
        if (!WouldApplyDynamicLunch(hour, activeScene, out var t) || t == null) return false;
        _hiddenDueToCompletedPathDay = -1;
        _pathCompletionHandled = true;
        _pathTransforms.Clear();
        _pathWorldPositions.Clear();
        _activePathSlot = null;
        _pathIndex = 0;
        _movePathBoundSceneName = string.Empty;
        ApplyWorldHidden(false);
        var p = t.position;
        if (rb2d != null) rb2d.MovePosition(p);
        else transform.position = p;
        ApplyIdlePresentation();
        return true;
    }

    private void ApplyVendor(int hour)
    {
        string scene = SceneManager.GetActiveScene().name;
        if (!IsVendorWorldScene(scene))
        {
            ApplyWorldHidden(true);
            ClearPath();
            _vendorHoming = false;
            ApplyIdlePresentation();
            return;
        }

        if (hour >= routineData.vendorShopOpenHour && hour < routineData.vendorShopCloseHour)
        {
            _vendorHoming = false;
            if (NpcScenePointRegistry.TryGetPointInLoadedScenes(routineData.vendorStallPointId, out var t) && t != null)
            {
                ApplyWorldHidden(false);
                var p = t.position;
                if (rb2d != null) rb2d.MovePosition(p);
                else transform.position = p;
            }
            ClearPath();
            ApplyIdlePresentation();
            return;
        }
        if (hour < routineData.vendorShopOpenHour)
        {
            ApplyWorldHidden(true);
            ClearPath();
            return;
        }
        if (_vendorHoming && (_pathWorldPositions.Count > 0 || _pathTransforms.Count > 0)) return;
        if (routineData.vendorPathHomePointIds == null || routineData.vendorPathHomePointIds.Length == 0)
        {
            if (!string.IsNullOrEmpty(routineData.vendorHomePointId)
                && NpcScenePointRegistry.TryGetPointInLoadedScenes(routineData.vendorHomePointId, out var home) && home != null)
            {
                ApplyWorldHidden(false);
                if (rb2d != null) rb2d.MovePosition(home.position);
                else transform.position = home.position;
            }
            else ApplyWorldHidden(true);
            ClearPath();
            return;
        }
        _vendorHoming = true;
        _pathCompletionHandled = false;
        _activePathSlot = new NpcRoutineSlot
        {
            moveSpeed = 1.2f,
            loop = false,
            hideWhenPathComplete = true
        };
        _pathTransforms.Clear();
        _pathWorldPositions.Clear();
        foreach (var id in routineData.vendorPathHomePointIds)
        {
            if (string.IsNullOrEmpty(id)) continue;
            if (NpcScenePointRegistry.TryGetPointInLoadedScenes(id, out var tr) && tr != null)
            {
                _pathTransforms.Add(tr);
                _pathWorldPositions.Add(tr.position);
            }
        }
        if (_pathTransforms.Count == 0)
        {
            ApplyWorldHidden(true);
            _vendorHoming = false;
            return;
        }
        _pathIndex = 0;
        ApplyWorldHidden(false);
        _movePathBoundSceneName = scene?.Trim() ?? string.Empty;
    }

    private void ApplySlot(NpcRoutineSlot s, int scheduleHour)
    {
        _vendorHoming = false;
        ApplyIdlePresentation();

        if (s.action == NpcSimpleRoutineAction.MovePath
            && s.pathPointIds != null
            && !s.loop)
        {
            string npcIdEarly = GetNpcIdKey();
            int curDayEarly = GameTimeManager.Instance != null ? GameTimeManager.Instance.DayInSemester : 0;
            if (!string.IsNullOrEmpty(npcIdEarly)
                && SCompletedSlot.TryGetValue(MakeCompletionKey(npcIdEarly, BuildSlotFingerprint(s)), out var doneEarly)
                && doneEarly.day == curDayEarly)
            {
                _pathCompletionHandled = true;
                _activePathSlot = s;
                _pathTransforms.Clear();
                _pathWorldPositions.Clear();
                _pathIndex = 0;
                RemoveAllPathResumeForNpc(npcIdEarly);
                _hiddenDueToCompletedPathDay = doneEarly.hidden ? curDayEarly : -1;
                if (rb2d != null) rb2d.position = doneEarly.worldPos;
                else transform.position = doneEarly.worldPos;
                ApplyWorldHidden(doneEarly.hidden);
                ApplyIdlePresentation();
                _suppressNextBootstrapAnchorTeleport = true;
                return;
            }
        }

        _pathCompletionHandled = false;
        _hiddenDueToCompletedPathDay = -1;
        _activePathSlot = s;
        _pathTransforms.Clear();
        _pathWorldPositions.Clear();
        _pathIndex = 0;
        switch (s.action)
        {
            case NpcSimpleRoutineAction.Hide:
                RemoveAllPathResumeForNpc(GetNpcIdKey());
                ApplyWorldHidden(true);
                return;
            case NpcSimpleRoutineAction.Teleport:
            case NpcSimpleRoutineAction.AppearAtPoint:
            case NpcSimpleRoutineAction.StationaryAtPoint:
                RemoveAllPathResumeForNpc(GetNpcIdKey());
                if (!string.IsNullOrEmpty(s.targetPointId)
                    && NpcScenePointRegistry.TryGetPointInLoadedScenes(s.targetPointId, out var t) && t != null)
                {
                    ApplyWorldHidden(false);
                    var p = t.position;
                    if (rb2d != null) rb2d.MovePosition(p);
                    else transform.position = p;
                }
                return;
            case NpcSimpleRoutineAction.MovePath:
            {
                if (s.pathPointIds == null) return;
                string npcIdLookup = GetNpcIdKey();
                foreach (var id in s.pathPointIds)
                {
                    if (string.IsNullOrEmpty(id)) continue;
                    if (NpcScenePointRegistry.TryGetPointInLoadedScenes(id, out var tr) && tr != null)
                    {
                        _pathTransforms.Add(tr);
                        _pathWorldPositions.Add(tr.position);
                    }
                }
                if (_pathTransforms.Count == 0)
                {
                    ApplyWorldHidden(false);
                    ClearPath();
                    ApplyIdlePresentation();
                    return;
                }
                _movePathBoundSceneName = SceneManager.GetActiveScene().name?.Trim() ?? string.Empty;
                string npcId = npcIdLookup;
                int curDayForResume = GameTimeManager.Instance != null ? GameTimeManager.Instance.DayInSemester : 0;
                string fpMove = BuildSlotFingerprint(s);
                string resumeStoreKey = MakePathResumeStorageKey(npcId, fpMove);
                bool foundResume = SPathResume.TryGetValue(resumeStoreKey, out var pr);
                if (!foundResume && !string.IsNullOrEmpty(npcId))
                    foundResume = SPathResume.TryGetValue(npcId, out pr);
                bool appliedResume = resumePathOnReturnToScene
                    && !string.IsNullOrEmpty(npcId)
                    && foundResume
                    && pr.dayInSemester == curDayForResume
                    && pr.slotFingerprint == fpMove
                    && RoutineSlotHourAndSchoolFilterMatches(s, scheduleHour);
                if (appliedResume)
                {
                    _pathIndex = Mathf.Clamp(pr.pathIndex, 0, _pathWorldPositions.Count - 1);
                    Vector3 p = pr.worldPos;
                    if (rb2d != null) rb2d.position = p;
                    else transform.position = p;
                    if (SPathResume.ContainsKey(resumeStoreKey))
                        SPathResume.Remove(resumeStoreKey);
                    else SPathResume.Remove(npcId);
                    _suppressNextBootstrapAnchorTeleport = true;
                }
                else
                {
                    if (!string.IsNullOrEmpty(npcId)) RemoveAllPathResumeForNpc(npcId);
                    _pathIndex = 0;
                }

                ApplyWorldHidden(false);

                // Bắt đầu path mới (không resume): đặt ngay waypoint đầu — tránh anchor bootstrap ở xa khiến NPC chạy từ đáy map tới đầu path.
                if (!appliedResume && _pathWorldPositions.Count > 0)
                {
                    Vector3 startWp = _pathWorldPositions[0];
                    if (rb2d != null) rb2d.position = startWp;
                    else transform.position = startWp;
                    _suppressNextBootstrapAnchorTeleport = true;
                }
            }
            return;
        }
    }
}
