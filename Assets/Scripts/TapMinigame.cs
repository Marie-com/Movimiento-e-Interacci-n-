using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class TapMinigame : MonoBehaviour, IMinigameView
{
    public static TapMinigame Instance { get; private set; }

    [SerializeField] GameObject panel;
    [SerializeField] TMP_Text titleText;
    [SerializeField] TMP_Text timerText;
    [SerializeField] TMP_Text countsText;
    [SerializeField] Button tapButton;

    int mask;
    float endTime;
    bool running;

    void Awake()
    {
        Instance = this;
        tapButton.onClick.AddListener(OnTap);
        panel.SetActive(false);
    }

    // Lo llama el anfitrión (por red) en todos los celulares
    public void Show(int participantsMask, int seconds)
    {
        CancelInvoke();
        mask = participantsMask;
        endTime = Time.time + seconds;
        running = true;

        var provider = GameManager.Instance.LocalPlayerIndexProvider;
        int me = provider != null ? provider() : -1;
        bool canTap = me >= 0 && (mask & (1 << me)) != 0;

        tapButton.interactable = canTap;
        titleText.text = canTap ? "¡TOCA LO MÁS RÁPIDO QUE PUEDAS!" : "Mirando el minijuego...";
        UpdateCounts(0, 0, 0, 0);
        panel.SetActive(true);
        AudioManager.PlaySfx(AudioManager.Sfx.TurnAlert);
    }

    public void UpdateCounts(int c0, int c1, int c2, int c3)
    {
        countsText.text = UIThemeConfig.FormatCounts(mask, c0, c1, c2, c3);
    }

    public void End()
    {
        running = false;
        tapButton.interactable = false;
        titleText.text = "¡Tiempo!";
        Invoke(nameof(Hide), 1.5f);
    }

    public void ForceClose()
    {
        CancelInvoke();
        running = false;
        tapButton.interactable = false;
        panel.SetActive(false);
    }

    void Hide() { panel.SetActive(false); }

    void Update()
    {
        if (!running) return;
        timerText.text = Mathf.Max(0, Mathf.CeilToInt(endTime - Time.time)).ToString();
    }

    void OnTap()
    {
        if (!running || NetworkBridge.Instance == null) return;
        NetworkBridge.Instance.SendTap();
        AudioManager.PlaySfx(AudioManager.Sfx.MinigameHit);
    }
}
