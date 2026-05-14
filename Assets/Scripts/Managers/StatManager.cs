using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;

public class StatManager : MonoBehaviour
{
    public static StatManager Instance;

    [Header("Timeline")]
    public int day = 1;
    public int time = 8;

    [Header("New campaign baseline")]
    [Tooltip("Tiền áp khi bắt đầu New Game (PrepareFreshCampaign). Gán số tiền khởi đầu mong muốn ở đây.")]
    [SerializeField] private float newCampaignStartingMoney;

    /// <summary>Tiền khi PrepareFreshCampaign — lấy từ Inspector, không phải từ GameSaveService.newMoney.</summary>
    public float NewCampaignStartingMoney => newCampaignStartingMoney;

    [Header("Stats")]
    public float gpa;
    public float stress;
    public float money;
    public float health;
    public float energy;
    public float social;
    public float skill;

    [Header("Hospital settings")]
    [Tooltip("SpawnPoint ID của giường bệnh trong scene. Khi health = 0, player sẽ teleport tới vị trí này.")]
    [SerializeField] private string hospitalBedSpawnId = "HospitalBed";

    [Tooltip("Tên scene chứa giường bệnh. Nếu player đang ở scene khác sẽ load scene này trước.")]
    [SerializeField] private string hospitalSceneName = "21_SchoolArea";

    [Tooltip("Phí viện (trừ tiền khi nhập viện). Nếu không đủ tiền → ending.")]
    [SerializeField] private float hospitalFee = 200000f;

    [Tooltip("Phần trăm health hồi phục sau khi nhập viện (0–1). Mặc định 0.25 = 25%.")]
    [SerializeField] private float hospitalHealthRestorePercent = 0.25f;

    [Tooltip("Thời gian chờ (giây, unscaled) trước khi hiện thông báo nhập viện — để player thấy mình đã teleport.")]
    [SerializeField] private float hospitalNoticeDelay = 0.5f;

    /// <summary>True khi đang xử lý hospital flow — các hệ thống khác nên bỏ qua health=0.</summary>
    public bool IsHandlingHealthZero { get; private set; }

    private void Awake()
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

    private void Start()
    {
        if (EventManager.Instance != null)
        {
            EventManager.Instance.OnChoiceSelected += UpdateStat;
            // Delay 1 frame to ensure UI objects have run OnEnable/Start and subscribed.
            Invoke(nameof(NotifyInitialStats), 0f);
        }
    }

    private void OnEnable()
    {
        if (EventManager.Instance != null)
        {
            Invoke(nameof(NotifyInitialStats), 0f);
        }
    }

    private void LateUpdate()
    {
        // Bắt mọi trường hợp health bị gán trực tiếp từ bên ngoài (không qua ModifyHealth)
        if (health <= 0f && !IsHandlingHealthZero)
        {
            CheckHealthZero();
        }
    }

    private void NotifyInitialStats()
    {
        if (EventManager.Instance != null)
        {
            EventManager.Instance.NotifyStatChanged();
        }
    }

    void UpdateStat()
    {
        gpa = Mathf.Clamp(gpa + 0.1f, 0f, 4.0f);
        stress -= 1f;

        energy -= 1f;
        if (energy < 0f) energy = 0f;

        time += 1;
        if (time >= 24)
        {
            time = 0;
            day += 1;
        }

        EventManager.Instance.NotifyStatChanged();
    }

    /// <summary>
    /// Thay đổi health theo lượng cho trước.
    /// amount > 0 → hồi máu, amount < 0 → trừ máu.
    /// Tự clamp trong khoảng [0, 100] và phát sự kiện cập nhật UI.
    /// Nếu health về 0 → tự động xử lý nhập viện.
    /// </summary>
    public void ModifyHealth(float amount)
    {
        health = Mathf.Clamp(health + amount, 0f, 100f);
        NotifyInitialStats();
        CheckHealthZero();
    }

    // ──────────────────────────────────────────────
    //  Health = 0 → Hospital flow
    // ──────────────────────────────────────────────

    /// <summary>
    /// Kiểm tra health ≤ 0. Được gọi từ ModifyHealth và có thể gọi thủ công.
    /// </summary>
    public void CheckHealthZero()
    {
        if (health > 0f) return;
        if (IsHandlingHealthZero) return;

        // Nếu EndingManager đã kết thúc game, không xử lý thêm.
        if (EndingManager.Instance != null && EndingManager.Instance.HasEnded) return;

        IsHandlingHealthZero = true;

        if (money >= hospitalFee)
        {
            // Đủ tiền → nhập viện
            money -= hospitalFee;
            float maxHealth = 100f;
            health = Mathf.Clamp(maxHealth * hospitalHealthRestorePercent, 1f, maxHealth);
            NotifyInitialStats();

            TeleportToHospitalBed();
            StartCoroutine(ShowHospitalNoticeAndReset());
        }
        else
        {
            // Không đủ tiền → kết thúc game
            TriggerHospitalizedEnding();
            IsHandlingHealthZero = false;
        }
    }

    /// <summary>
    /// Teleport player tới giường bệnh (SpawnPoint có Id = hospitalBedSpawnId).
    /// Nếu player đang ở scene khác hospitalSceneName → dùng SceneFlowController load scene.
    /// Nếu đang cùng scene → teleport trực tiếp.
    /// </summary>
    private void TeleportToHospitalBed()
    {
        if (string.IsNullOrWhiteSpace(hospitalBedSpawnId))
        {
            Debug.LogWarning("StatManager: hospitalBedSpawnId chưa được gán.");
            return;
        }

        string activeScene = SceneManager.GetActiveScene().name;
        bool sameScene = string.Equals(activeScene, hospitalSceneName, System.StringComparison.Ordinal);

        if (!sameScene && !string.IsNullOrWhiteSpace(hospitalSceneName))
        {
            // Đang ở scene khác → load scene bệnh viện, SceneFlowController sẽ tự teleport tới SpawnPoint
            if (SceneFlowController.Instance != null)
            {
                SceneFlowController.Instance.LoadGameplayScene(hospitalSceneName, hospitalBedSpawnId);
                Debug.Log($"StatManager: Loading scene '{hospitalSceneName}' với SpawnPoint '{hospitalBedSpawnId}'.");
            }
            else
            {
                Debug.LogWarning("StatManager: SceneFlowController.Instance null — không thể load scene bệnh viện.");
            }
            return;
        }

        // Đang ở cùng scene → teleport trực tiếp
        if (PersistentPlayer.Instance == null)
        {
            Debug.LogWarning("StatManager: PersistentPlayer.Instance null — không thể teleport tới giường bệnh.");
            return;
        }

        string targetId = hospitalBedSpawnId.Trim();
        SpawnPoint[] points = Object.FindObjectsByType<SpawnPoint>(FindObjectsInactive.Exclude);

        SpawnPoint picked = null;
        for (int i = 0; i < points.Length; i++)
        {
            if (points[i] != null && string.Equals(points[i].SpawnId, targetId, System.StringComparison.Ordinal))
            {
                picked = points[i];
                break;
            }
        }

        if (picked != null)
        {
            PersistentPlayer.Instance.TeleportTo(picked.transform.position);
            Debug.Log($"StatManager: Player teleported tới giường bệnh '{targetId}'.");
        }
        else
        {
            Debug.LogWarning($"StatManager: Không tìm thấy SpawnPoint '{targetId}' trong scene '{activeScene}'.");
        }
    }

    /// <summary>
    /// Hiện thông báo đã nhập viện sau một khoảng delay ngắn, rồi reset cờ IsHandlingHealthZero.
    /// </summary>
    private IEnumerator ShowHospitalNoticeAndReset()
    {
        if (hospitalNoticeDelay > 0f)
        {
            yield return new WaitForSecondsRealtime(hospitalNoticeDelay);
        }

        EventLetterUI letter = FindLetterUI();
        if (letter != null)
        {
            string title = "Nhập viện";
            string body = $"Sức khỏe của bạn xuống quá thấp. Bạn đã được đưa vào bệnh viện.\n\n" +
                           $"Chi phí điều trị: {hospitalFee:#,0}₫\n" +
                           $"Sức khỏe hồi phục: {hospitalHealthRestorePercent * 100f:0}%";
            letter.gameObject.SetActive(true);
            letter.ShowPlain(title, body, "Được");
        }

        IsHandlingHealthZero = false;
    }

    /// <summary>
    /// Kết thúc game vì nhân vật bị bệnh nặng và không có tiền chữa trị.
    /// </summary>
    private void TriggerHospitalizedEnding()
    {
        if (EndingManager.Instance == null)
        {
            Debug.LogWarning("StatManager: EndingManager.Instance null — không thể kích hoạt ending Hospitalized.");
            return;
        }

        EndingManager.Instance.TriggerEnding(EndingType.Hospitalized);
    }

    // ──────────────────────────────────────────────

    private static EventLetterUI FindLetterUI()
    {
        if (EventLetterUI.Instance != null) return EventLetterUI.Instance;

        EventLetterUI[] uis = Object.FindObjectsByType<EventLetterUI>(FindObjectsInactive.Include);
        return uis != null && uis.Length > 0 ? uis[0] : null;
    }

    public void Persist_Apply(StatPayload p)
    {
        if (p == null) return;

        day = Mathf.Max(1, p.day);
        time = Mathf.Clamp(p.time, 0, 23);
        gpa = p.gpa;
        stress = p.stress;
        money = p.money;
        health = p.health;
        energy = Mathf.Max(0f, p.energy);
        social = p.social;
        skill = p.skill;

        NotifyInitialStats();
    }
}
