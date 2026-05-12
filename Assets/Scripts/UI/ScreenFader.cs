using System.Collections;
using System;
using UnityEngine;

public class ScreenFader : MonoBehaviour
{
    [SerializeField] private CanvasGroup canvasGroup;
    [SerializeField] private float defaultFadeOut = 0.3f;
    [SerializeField] private float defaultHold = 2f;
    [SerializeField] private float defaultFadeIn = 0.3f;

    private Coroutine fadeRoutine;

    private void Awake()
    {
        if (canvasGroup == null)
        {
            canvasGroup = GetComponent<CanvasGroup>();
        }

        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false;
            canvasGroup.interactable = false;
        }
    }

    public void Fade()
    {
        Fade(defaultFadeOut, defaultHold, defaultFadeIn);
    }

    public void Fade(float holdDuration)
    {
        Fade(defaultFadeOut, holdDuration, defaultFadeIn);
    }

    public void Fade(float fadeOutDuration, float holdDuration, float fadeInDuration)
    {
        if (canvasGroup == null) return;

        if (fadeRoutine != null)
        {
            StopCoroutine(fadeRoutine);
        }

        fadeRoutine = StartCoroutine(FadeRoutine(fadeOutDuration, holdDuration, fadeInDuration));
    }

    /// <param name="holdBeforeMidAction">Thời gian giữ màn hình đen sau fade out, trước khi gọi mid (vd. nghỉ cuối ngày).</param>
    /// <param name="holdAfterMidAction">Thời gian giữ màn hình đen sau mid, trước khi fade in.</param>
    public void FadeWithMidAction(
        Action midAction,
        float fadeOutDuration,
        float holdBeforeMidAction,
        float holdAfterMidAction,
        float fadeInDuration,
        Action onComplete = null)
    {
        if (canvasGroup == null) return;

        if (fadeRoutine != null)
        {
            StopCoroutine(fadeRoutine);
        }

        fadeRoutine = StartCoroutine(FadeRoutineWithMidAction(
            midAction, fadeOutDuration, holdBeforeMidAction, holdAfterMidAction, fadeInDuration, onComplete));
    }

    private IEnumerator FadeRoutine(float fadeOutDuration, float holdDuration, float fadeInDuration)
    {
        canvasGroup.blocksRaycasts = true;

        if (fadeOutDuration > 0f)
        {
            yield return FadeTo(1f, fadeOutDuration);
        }
        else
        {
            canvasGroup.alpha = 1f;
        }

        if (holdDuration > 0f)
        {
            yield return new WaitForSeconds(holdDuration);
        }

        if (fadeInDuration > 0f)
        {
            yield return FadeTo(0f, fadeInDuration);
        }
        else
        {
            canvasGroup.alpha = 0f;
        }

        canvasGroup.blocksRaycasts = false;
        fadeRoutine = null;
    }

    private IEnumerator FadeRoutineWithMidAction(
        Action midAction,
        float fadeOutDuration,
        float holdBeforeMidAction,
        float holdAfterMidAction,
        float fadeInDuration,
        Action onComplete)
    {
        canvasGroup.blocksRaycasts = true;

        if (fadeOutDuration > 0f)
        {
            yield return FadeTo(1f, fadeOutDuration);
        }
        else
        {
            canvasGroup.alpha = 1f;
        }

        if (holdBeforeMidAction > 0f)
        {
            yield return new WaitForSeconds(holdBeforeMidAction);
        }

        try
        {
            midAction?.Invoke();
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
        }

        if (holdAfterMidAction > 0f)
        {
            yield return new WaitForSeconds(holdAfterMidAction);
        }

        if (fadeInDuration > 0f)
        {
            yield return FadeTo(0f, fadeInDuration);
        }
        else
        {
            canvasGroup.alpha = 0f;
        }

        canvasGroup.blocksRaycasts = false;
        fadeRoutine = null;

        try
        {
            onComplete?.Invoke();
        }
        catch (Exception ex)
        {
            Debug.LogException(ex);
        }
    }

    private IEnumerator FadeTo(float target, float duration)
    {
        float start = canvasGroup.alpha;
        float elapsed = 0f;

        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            float t = Mathf.Clamp01(elapsed / duration);
            canvasGroup.alpha = Mathf.Lerp(start, target, t);
            yield return null;
        }

        canvasGroup.alpha = target;
    }
}
