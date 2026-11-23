using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Respuesta táctil para botones: se hunde al pulsar (escala + labio -4px + oscurecer)
// y rebota elásticamente al soltar. Usa tiempo real, así que funciona aunque cambie Time.timeScale.
[RequireComponent(typeof(RectTransform))]
public class UIButtonJuice : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerClickHandler
{
    [SerializeField] RectTransform content;        // texto/ícono que baja con el labio
    [SerializeField] Graphic tintTarget;           // imagen del botón que se oscurece
    [SerializeField] float pressedScale = 0.93f;
    [SerializeField] float pressOffset = 4f;
    [SerializeField, Range(0f, 1f)] float pressedDarken = 0.85f;
    [SerializeField] bool playClickSound = true;
    [SerializeField] AudioManager.Sfx clickSound = AudioManager.Sfx.Click;

    Selectable selectable;
    CanvasGroup group;
    Vector2 contentBase;
    Color baseColor = Color.white;
    Coroutine anim;
    bool pressed;
    bool lastInteractable = true;

    void Awake()
    {
        selectable = GetComponent<Selectable>();
        if (tintTarget == null && selectable != null) tintTarget = selectable.targetGraphic;
        if (tintTarget != null) baseColor = tintTarget.color;
        if (content != null) contentBase = content.anchoredPosition;
        group = GetComponent<CanvasGroup>();
        lastInteractable = Interactable;
        ApplyInteractableVisuals(lastInteractable);
    }

    bool Interactable { get { return selectable == null || selectable.IsInteractable(); } }

    public void OnPointerDown(PointerEventData eventData)
    {
        if (!Interactable || eventData.button != PointerEventData.InputButton.Left) return;
        pressed = true;
        if (content != null) content.anchoredPosition = contentBase + new Vector2(0f, -pressOffset);
        if (tintTarget != null) tintTarget.color = UIThemeConfig.Mul(baseColor, pressedDarken);
        Run(PressRoutine());
    }

    public void OnPointerUp(PointerEventData eventData)
    {
        if (!pressed) return;
        pressed = false;
        if (content != null) content.anchoredPosition = contentBase;
        if (tintTarget != null) tintTarget.color = Interactable ? baseColor : DisabledColor;
        Run(UIAnim.Pop(transform, transform.localScale.x, 0.45f));
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        if (playClickSound && Interactable) AudioManager.PlaySfx(clickSound);
    }

    IEnumerator PressRoutine()
    {
        float from = transform.localScale.x;
        float t = 0f;
        const float duration = 0.07f;
        while (t < duration)
        {
            t += Time.unscaledDeltaTime;
            float s = Mathf.Lerp(from, pressedScale, t / duration);
            transform.localScale = new Vector3(s, s, 1f);
            yield return null;
        }
        transform.localScale = new Vector3(pressedScale, pressedScale, 1f);
    }

    void Run(IEnumerator routine)
    {
        if (anim != null) StopCoroutine(anim);
        if (isActiveAndEnabled) anim = StartCoroutine(routine);
    }

    void LateUpdate()
    {
        bool interactable = Interactable;
        if (interactable == lastInteractable) return;
        lastInteractable = interactable;
        ApplyInteractableVisuals(interactable);
    }

    Color DisabledColor
    {
        get
        {
            float g = baseColor.grayscale * 0.75f;
            return new Color(g, g, g, baseColor.a);
        }
    }

    void ApplyInteractableVisuals(bool interactable)
    {
        if (tintTarget != null && !pressed) tintTarget.color = interactable ? baseColor : DisabledColor;
        if (group != null) group.alpha = interactable ? 1f : 0.7f;
    }

    void OnDisable()
    {
        pressed = false;
        anim = null;
        transform.localScale = Vector3.one;
        if (content != null) content.anchoredPosition = contentBase;
        if (tintTarget != null) tintTarget.color = Interactable ? baseColor : DisabledColor;
    }
}
