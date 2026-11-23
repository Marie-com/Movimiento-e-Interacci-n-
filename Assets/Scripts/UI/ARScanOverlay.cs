using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Guía de escaneo AR: marco con esquinas y píldora inferior mientras el tablero no esté colocado.
// No bloquea toques (blocksRaycasts = false) y deja el centro transparente para ver el marcador.
public class ARScanOverlay : MonoBehaviour
{
    [SerializeField] BoardPlacer boardPlacer;
    [SerializeField] CanvasGroup group;
    [SerializeField] RectTransform frame;
    [SerializeField] Image[] corners;
    [SerializeField] TMP_Text hintText;
    [SerializeField] string searchingMessage = "Apunta la cámara al tablero físico";
    [SerializeField] string foundMessage = "¡Tablero detectado!";
    [SerializeField] float foundHold = 1.2f;

    float placedAt = -1f;
    Color cornerBase = Color.white;

    void Start()
    {
        if (boardPlacer == null) boardPlacer = FindFirstObjectByType<BoardPlacer>();
        if (boardPlacer != null) boardPlacer.OnBoardTrackingChanged += OnTrackingChanged;
        if (corners != null && corners.Length > 0 && corners[0] != null) cornerBase = corners[0].color;
        if (group != null)
        {
            group.blocksRaycasts = false;
            group.interactable = false;
            group.alpha = Visible ? 1f : 0f;
        }
        if (hintText != null) hintText.text = searchingMessage;
    }

    void OnDestroy()
    {
        if (boardPlacer != null) boardPlacer.OnBoardTrackingChanged -= OnTrackingChanged;
    }

    void OnTrackingChanged(bool tracked)
    {
        if (tracked && placedAt < 0f && boardPlacer.IsBoardPlaced)
        {
            placedAt = Time.unscaledTime;
            if (hintText != null) hintText.text = foundMessage;
            SetCornerColor(UIThemeConfig.GreenTop);
        }
    }

    bool Visible
    {
        get
        {
            if (boardPlacer == null) return false;
            if (!boardPlacer.IsBoardPlaced) return true;
            return placedAt >= 0f && Time.unscaledTime - placedAt < foundHold;
        }
    }

    void SetCornerColor(Color c)
    {
        if (corners == null) return;
        foreach (var img in corners) if (img != null) img.color = c;
    }

    void Update()
    {
        if (group == null) return;
        float target = Visible ? 1f : 0f;
        group.alpha = Mathf.MoveTowards(group.alpha, target, Time.unscaledDeltaTime * 3f);

        if (frame != null && group.alpha > 0f)
        {
            float s = 1f + 0.015f * Mathf.Sin(Time.unscaledTime * 2.5f);
            frame.localScale = new Vector3(s, s, 1f);
        }

        // Si se pierde el tablero antes de colocarse, vuelve el texto de búsqueda
        if (boardPlacer != null && !boardPlacer.IsBoardPlaced && hintText != null && hintText.text != searchingMessage)
        {
            hintText.text = searchingMessage;
            SetCornerColor(cornerBase);
        }
    }
}
