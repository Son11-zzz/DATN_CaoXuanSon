using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class ZoneFadeTrigger : MonoBehaviour
{
    [SerializeField] private ScreenFader screenFader;
    [SerializeField] private float holdDuration = 2f;
    [SerializeField] private bool triggerOnce = true;

    private bool triggered;

    private void Awake()
    {
        TryResolveFader();
    }

    private void OnEnable()
    {
        TryResolveFader();
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (triggerOnce && triggered) return;
        if (!other.CompareTag("Player")) return;

        TryResolveFader();
        if (screenFader == null) return;

        triggered = true;
        screenFader.Fade(holdDuration);
    }

    private void TryResolveFader()
    {
        if (screenFader != null) return;
        screenFader = FindAnyObjectByType<ScreenFader>();
    }
}
