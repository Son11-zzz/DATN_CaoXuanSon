using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering.Universal;

/// <summary>
/// Ngày / đêm theo giờ nguyên của <see cref="GameTimeManager"/>:
/// trời sáng từ 5h đến hết 17h (giờ 5…17), từ 18h đến 4h là đêm.
/// Ban đêm: Global Light 2D tối hẳn + bật các đèn trong danh sách (point / mesh đèn đường).
/// </summary>
public class TimeOfDayLightingController : MonoBehaviour
{
    [Header("Day / night (integer hours)")]
    [SerializeField, Range(0, 23)] private int dayStartHour = 5;
    [Tooltip("Giờ đầu tiên KHÔNG còn ban ngày (vd. 18 = ban ngày 5…17, từ 18h là đêm).")]
    [SerializeField, Range(1, 24)] private int dayEndHourExclusive = 18;

    [Header("Global Light 2D (URP)")]
    [SerializeField] private Light2D globalLight2D;
    [SerializeField] private bool autoFindGlobalLightAtRuntime = true;
    [SerializeField] private string globalLightObjectName = "Global Light 2D";
    [SerializeField, Min(0f)] private float dayGlobalLightIntensity = 1f;
    [SerializeField, Min(0f)] private float nightGlobalLightIntensity = 0.28f;

    [Header("Transition (global light only)")]
    [SerializeField] private bool smoothTransition = true;
    [SerializeField] private float transitionSpeed = 3f;
    [Tooltip("Khi đổi giờ (chổi ngày/đêm), dù thời gian thật này để giảm sáng/ tăng sáng mượt thay vì tối ngay. Đèn đường bật khi hết lerp nếu đã là giờ đêm.")]
    [SerializeField, Min(0f)] private float dayNightChangeBlendSeconds = 2.2f;

    [Header("Street / scene lamps (on = night only)")]
    [SerializeField] private List<GameObject> lampObjects = new List<GameObject>();
    [Tooltip("Bật/tắt cả root prefab đèn (true) hoặc chỉ bật Light / node emission bên trong (false).")]
    [SerializeField] private bool toggleLampRootActive = true;

    private bool subscribed;
    private float currentLightIntensity;
    private float targetLightIntensity;
    private bool dayNightLerpActive;
    private float dayNightLerpT0;
    private float dayNightLerpFrom;
    private float dayNightLerpTo;
    private int pendingLampStateHour;

    private void OnEnable()
    {
        TryResolveGlobalLightReference();
        TrySubscribe();
        RefreshFromCurrentTime(immediate: true);
    }

    private void Start()
    {
        TryResolveGlobalLightReference();
        RefreshFromCurrentTime(immediate: true);
    }

    private void OnDisable()
    {
        if (subscribed && GameTimeManager.Instance != null)
        {
            GameTimeManager.Instance.OnTimeChanged -= HandleTimeChanged;
        }

        subscribed = false;

        if (globalLight2D != null)
        {
            globalLight2D.intensity = dayGlobalLightIntensity;
        }

        ForceAllLampsOff();
    }

    private void Update()
    {
        if (globalLight2D == null && autoFindGlobalLightAtRuntime)
        {
            TryResolveGlobalLightReference();
        }

        if (!subscribed)
        {
            TrySubscribe();
        }

        if (globalLight2D == null) return;

        if (dayNightLerpActive)
        {
            float t = (Time.time - dayNightLerpT0) / Mathf.Max(0.001f, dayNightChangeBlendSeconds);
            t = Mathf.Clamp01(t);
            currentLightIntensity = Mathf.Lerp(dayNightLerpFrom, dayNightLerpTo, t);
            globalLight2D.intensity = currentLightIntensity;
            if (t >= 1f)
            {
                dayNightLerpActive = false;
                targetLightIntensity = dayNightLerpTo;
                ApplyLampState(pendingLampStateHour);
            }

            return;
        }

        float step = Mathf.Max(0f, transitionSpeed) * Time.deltaTime;

        if (smoothTransition)
        {
            currentLightIntensity = Mathf.MoveTowards(currentLightIntensity, targetLightIntensity, step);
            globalLight2D.intensity = currentLightIntensity;
        }
        else
        {
            currentLightIntensity = targetLightIntensity;
            globalLight2D.intensity = currentLightIntensity;
        }
    }

    private void TrySubscribe()
    {
        if (subscribed) return;
        if (GameTimeManager.Instance == null) return;

        GameTimeManager.Instance.OnTimeChanged -= HandleTimeChanged;
        GameTimeManager.Instance.OnTimeChanged += HandleTimeChanged;
        subscribed = true;
    }

    private void HandleTimeChanged()
    {
        TryResolveGlobalLightReference();
        RefreshFromCurrentTime(immediate: false);
    }

    private void TryResolveGlobalLightReference()
    {
        if (globalLight2D != null) return;
        if (!autoFindGlobalLightAtRuntime) return;

        var lights = UnityEngine.Object.FindObjectsByType<Light2D>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        if (lights == null || lights.Length == 0) return;

        for (int i = 0; i < lights.Length; i++)
        {
            var l = lights[i];
            if (l == null) continue;
            if (l.lightType == Light2D.LightType.Global)
            {
                globalLight2D = l;
                return;
            }
        }

        if (!string.IsNullOrWhiteSpace(globalLightObjectName))
        {
            for (int i = 0; i < lights.Length; i++)
            {
                var l = lights[i];
                if (l == null) continue;
                if (l.name != null && l.name.IndexOf(globalLightObjectName, StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    globalLight2D = l;
                    return;
                }
            }
        }
    }

    private void RefreshFromCurrentTime(bool immediate)
    {
        if (GameTimeManager.Instance == null)
        {
            dayNightLerpActive = false;
            targetLightIntensity = dayGlobalLightIntensity;
            if (immediate && globalLight2D != null)
            {
                currentLightIntensity = targetLightIntensity;
                globalLight2D.intensity = currentLightIntensity;
            }

            ForceAllLampsOff();
            return;
        }

        int hour = GameTimeManager.Instance.Hour;
        float newIntensity = EvaluateGlobalLightIntensity(hour);
        targetLightIntensity = newIntensity;

        if (immediate)
        {
            dayNightLerpActive = false;
            if (globalLight2D != null)
            {
                currentLightIntensity = newIntensity;
                globalLight2D.intensity = currentLightIntensity;
            }

            ApplyLampState(hour);
            return;
        }

        if (dayNightChangeBlendSeconds > 0.001f && globalLight2D != null)
        {
            if (Mathf.Approximately(newIntensity, currentLightIntensity))
            {
                if (globalLight2D != null)
                {
                    globalLight2D.intensity = currentLightIntensity;
                }

                ApplyLampState(hour);
                return;
            }

            dayNightLerpActive = true;
            dayNightLerpT0 = Time.time;
            dayNightLerpFrom = currentLightIntensity;
            dayNightLerpTo = newIntensity;
            pendingLampStateHour = hour;
            return;
        }

        if (globalLight2D != null)
        {
            currentLightIntensity = newIntensity;
            globalLight2D.intensity = currentLightIntensity;
        }

        ApplyLampState(hour);
    }

    private int GetDayStartClamped()
    {
        return Mathf.Clamp(dayStartHour, 0, 23);
    }

    private int GetDayEndExclusiveClamped()
    {
        int start = GetDayStartClamped();
        int end = Mathf.Clamp(dayEndHourExclusive, 1, 24);
        if (end <= start) end = Mathf.Min(24, start + 1);
        return end;
    }

    /// <summary>Ban ngày: [dayStartHour, dayEndHourExclusive) — mặc định 5…17.</summary>
    public bool IsDaylightHour(int hour)
    {
        int start = GetDayStartClamped();
        int endEx = GetDayEndExclusiveClamped();
        return hour >= start && hour < endEx;
    }

    /// <summary>0 = ban ngày (sáng), 1 = đêm (tối hẳn).</summary>
    private float GetNightBlend01(int hour)
    {
        return IsDaylightHour(hour) ? 0f : 1f;
    }

    private float EvaluateGlobalLightIntensity(int hour)
    {
        float night = GetNightBlend01(hour);
        return Mathf.Lerp(dayGlobalLightIntensity, nightGlobalLightIntensity, night);
    }

    private void ApplyLampState(int hour)
    {
        bool lampsOn = !IsDaylightHour(hour);

        for (int i = 0; i < lampObjects.Count; i++)
        {
            var go = lampObjects[i];
            if (go == null) continue;

            if (toggleLampRootActive)
            {
                if (go.activeSelf != lampsOn)
                {
                    go.SetActive(lampsOn);
                }

                continue;
            }

            ApplyLampEmissionState(go, lampsOn);
        }
    }

    private void ForceAllLampsOff()
    {
        for (int i = 0; i < lampObjects.Count; i++)
        {
            var go = lampObjects[i];
            if (go == null) continue;

            if (toggleLampRootActive)
            {
                if (go.activeSelf) go.SetActive(false);
                continue;
            }

            ApplyLampEmissionState(go, false);
        }
    }

    private static void ApplyLampEmissionState(GameObject lampRoot, bool on)
    {
        if (lampRoot == null) return;

        var lights3D = lampRoot.GetComponentsInChildren<Light>(true);
        for (int i = 0; i < lights3D.Length; i++)
        {
            if (lights3D[i] != null)
            {
                lights3D[i].enabled = on;
            }
        }

        var behaviours = lampRoot.GetComponentsInChildren<Behaviour>(true);
        for (int i = 0; i < behaviours.Length; i++)
        {
            var b = behaviours[i];
            if (b == null) continue;
            if (b is Light) continue;

            if (string.Equals(b.GetType().Name, "Light2D", StringComparison.Ordinal))
            {
                if (b is Light2D l2d && l2d.lightType == Light2D.LightType.Global)
                {
                    continue;
                }

                b.enabled = on;
            }
        }

        var allTransforms = lampRoot.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < allTransforms.Length; i++)
        {
            var t = allTransforms[i];
            if (t == null || t == lampRoot.transform) continue;

            string n = t.name;
            if (string.IsNullOrWhiteSpace(n)) continue;

            bool isEmissionNode = n.IndexOf("light", StringComparison.OrdinalIgnoreCase) >= 0
                                  || n.IndexOf("glow", StringComparison.OrdinalIgnoreCase) >= 0
                                  || n.IndexOf("emiss", StringComparison.OrdinalIgnoreCase) >= 0;
            if (!isEmissionNode) continue;

            if (t.gameObject.activeSelf != on)
            {
                t.gameObject.SetActive(on);
            }
        }
    }
}
