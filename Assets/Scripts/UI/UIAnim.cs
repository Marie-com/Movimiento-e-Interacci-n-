using System.Collections;
using UnityEngine;

// Animaciones de UI con tiempo real (no dependen de Time.timeScale ni de DOTween)
public static class UIAnim
{
    // Aparece con rebote elástico: escala "from" -> 1
    public static IEnumerator Pop(Transform target, float from = 0.8f, float duration = 0.45f)
    {
        if (target == null) yield break;
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float k = Mathf.Exp(-7f * t) * Mathf.Cos(18f * t);
            float s = 1f + (from - 1f) * k;
            target.localScale = new Vector3(s, s, 1f);
            yield return null;
        }
        target.localScale = Vector3.one;
    }

    public static IEnumerator Fade(CanvasGroup group, float to, float duration = 0.2f)
    {
        if (group == null) yield break;
        float from = group.alpha;
        float t = 0f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            group.alpha = Mathf.Lerp(from, to, t / duration);
            yield return null;
        }
        group.alpha = to;
    }

    public static void SetVisible(CanvasGroup group, bool visible)
    {
        if (group == null) return;
        group.alpha = visible ? 1f : 0f;
        group.interactable = visible;
        group.blocksRaycasts = visible;
    }
}
