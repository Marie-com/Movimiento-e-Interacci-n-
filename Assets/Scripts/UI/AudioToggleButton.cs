using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Botón de sonido (Mute/Unmute) conectado a AudioManager
[RequireComponent(typeof(Button))]
public class AudioToggleButton : MonoBehaviour
{
    [SerializeField] Image icon;
    [SerializeField] Sprite soundOnIcon;
    [SerializeField] Sprite soundOffIcon;
    [SerializeField] TMP_Text label;

    AudioManager subscribed;

    void Awake()
    {
        GetComponent<Button>().onClick.AddListener(Toggle);
    }

    void OnEnable()
    {
        TrySubscribe();
        Refresh(AudioManager.Instance != null && AudioManager.Instance.Muted);
    }

    void Start()
    {
        TrySubscribe();
        Refresh(AudioManager.Instance != null && AudioManager.Instance.Muted);
    }

    void OnDestroy()
    {
        if (subscribed != null) subscribed.MuteChanged -= Refresh;
    }

    void TrySubscribe()
    {
        if (subscribed != null || AudioManager.Instance == null) return;
        subscribed = AudioManager.Instance;
        subscribed.MuteChanged += Refresh;
    }

    void Toggle()
    {
        if (AudioManager.Instance != null) AudioManager.Instance.ToggleMute();
    }

    void Refresh(bool muted)
    {
        if (icon != null) icon.sprite = muted ? soundOffIcon : soundOnIcon;
        if (label != null) label.text = muted ? "SONIDO: NO" : "SONIDO: SÍ";
    }
}
