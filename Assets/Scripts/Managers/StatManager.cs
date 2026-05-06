using UnityEngine;

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

    private void NotifyInitialStats()
    {
        if (EventManager.Instance != null)
        {
            EventManager.Instance.NotifyStatChanged();
        }
    }

    void UpdateStat()
    {
        gpa += 0.1f;
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
