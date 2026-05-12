using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HUDUI : MonoBehaviour
{
    [Header("Text")]
    [SerializeField] private TextMeshProUGUI dayText;
    [SerializeField] private TextMeshProUGUI timeText;
    [SerializeField] private TextMeshProUGUI moneyText;

    [Header("Sliders")]
    [SerializeField] private Slider gpaSlider;
    [SerializeField] private Slider stressSlider;
    [SerializeField] private Slider energySlider;
    [SerializeField] private Slider healthSlider;
    [SerializeField] private Slider socialSlider;


    [Header("Slider Ranges")]
    [SerializeField] private float gpaMax = 4f;
    [SerializeField] private float stressMax = 100f;
    [SerializeField] private float energyMax = 100f;
    [SerializeField] private float healthMax = 100f;
    [SerializeField] private float socialMax = 100f;

    private bool subscribed;
    private bool warnedMissingRefs;

    private void OnEnable()
    {
        TrySubscribe();
        Refresh();
    }

    private void OnDisable()
    {
        Unsubscribe();
    }

    private void Update()
    {
        // Always refresh so the HUD stays updated even if events/subscription are not configured.
        // This is lightweight (a few text/slider assignments).
        TrySubscribe();
        Refresh();
    }

    private void TrySubscribe()
    {
        if (subscribed) return;

        bool hasEvent = EventManager.Instance != null;
        bool hasTime = GameTimeManager.Instance != null;

        if (hasEvent)
        {
            EventManager.Instance.OnStatChanged -= Refresh;
            EventManager.Instance.OnStatChanged += Refresh;
        }

        if (hasTime)
        {
            GameTimeManager.Instance.OnTimeChanged -= Refresh;
            GameTimeManager.Instance.OnTimeChanged += Refresh;
        }

        subscribed = hasEvent || hasTime;
        if (!subscribed && !warnedMissingRefs)
        {
            warnedMissingRefs = true;
            Debug.LogWarning("HUDUI: Đang chờ EventManager hoặc GameTimeManager xuất hiện trong scene.");
        }
    }

    private void Unsubscribe()
    {
        if (EventManager.Instance != null)
        {
            EventManager.Instance.OnStatChanged -= Refresh;
        }

        if (GameTimeManager.Instance != null)
        {
            GameTimeManager.Instance.OnTimeChanged -= Refresh;
        }

        subscribed = false;
    }

    public void Refresh()
    {
        if (!warnedMissingRefs)
        {
            if (dayText == null || timeText == null || moneyText == null)
            {
                warnedMissingRefs = true;
                Debug.LogWarning("HUDUI: Thiếu một hoặc nhiều tham chiếu chữ (Ngày/Giờ/Tiền). Gán trong Inspector.");
            }
            else if (gpaSlider == null || stressSlider == null || energySlider == null || healthSlider == null || socialSlider == null)
            {
                warnedMissingRefs = true;
                Debug.LogWarning("HUDUI: Thiếu một hoặc nhiều thanh Slider (GPA/Căng thẳng/Năng lượng/Sinh lực/Xã hội). Gán trong Inspector.");
            }
        }

        var sm = StatManager.Instance;
        if (sm == null)
        {
            if (!warnedMissingRefs)
            {
                warnedMissingRefs = true;
                Debug.LogWarning("HUDUI: StatManager.Instance đang null. Thêm StatManager vào scene.");
            }
            return;
        }

        var tm = GameTimeManager.Instance;
        if (tm != null)
        {
            if (dayText != null)
            {
                dayText.text = $"Ngày {tm.DayInSemester}  ·  Học kỳ {tm.Semester}";
            }

            if (timeText != null)
            {
                timeText.text = $"Giờ {tm.Hour:00}:00";
            }
        }
        else
        {
            // fallback nếu chưa có GameTimeManager trong scene
            if (dayText != null) dayText.text = $"Ngày {sm.day}  ·  Học kỳ 1";
            if (timeText != null) timeText.text = $"Giờ {sm.time:00}:00";
        }

        if (moneyText != null)
        {
            moneyText.text = $"Tiền: {sm.money:0}";
        }

        if (gpaSlider != null) gpaSlider.value = Normalize(sm.gpa, gpaMax);
        if (stressSlider != null) stressSlider.value = Normalize(sm.stress, stressMax);
        if (energySlider != null) energySlider.value = Normalize(sm.energy, energyMax);
        if (healthSlider != null) healthSlider.value = Normalize(sm.health, healthMax);
        if (socialSlider != null) socialSlider.value = Normalize(sm.social, socialMax);
    }

    private static float Normalize(float value, float max)
    {
        if (max <= 0f) return 0f;
        return Mathf.Clamp01(value / max);
    }
}
