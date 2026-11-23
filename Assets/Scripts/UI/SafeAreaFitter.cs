using UnityEngine;

// Ajusta el RectTransform a Screen.safeArea (cámara frontal / bordes curvos en Android).
[RequireComponent(typeof(RectTransform))]
public class SafeAreaFitter : MonoBehaviour
{
    RectTransform rect;
    Rect lastSafeArea;
    Vector2Int lastScreen;
    ScreenOrientation lastOrientation;

    void Awake()
    {
        rect = (RectTransform)transform;
        Apply();
    }

    void Update()
    {
        if (Screen.safeArea != lastSafeArea
            || Screen.width != lastScreen.x || Screen.height != lastScreen.y
            || Screen.orientation != lastOrientation)
        {
            Apply();
        }
    }

    void Apply()
    {
        if (rect == null) rect = (RectTransform)transform;
        lastSafeArea = Screen.safeArea;
        lastScreen = new Vector2Int(Screen.width, Screen.height);
        lastOrientation = Screen.orientation;
        if (Screen.width <= 0 || Screen.height <= 0) return;

        Rect safe = Screen.safeArea;
        Vector2 min = safe.position;
        Vector2 max = safe.position + safe.size;
        min.x /= Screen.width; max.x /= Screen.width;
        min.y /= Screen.height; max.y /= Screen.height;

        rect.anchorMin = min;
        rect.anchorMax = max;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }
}
