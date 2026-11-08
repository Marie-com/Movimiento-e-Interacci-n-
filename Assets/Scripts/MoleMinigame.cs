using System.Collections;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MoleMinigame : MonoBehaviour, IMinigameView
{
    public static MoleMinigame Instance { get; private set; }

    public const int Holes = 9;

    [SerializeField] GameObject panel;
    [SerializeField] RectTransform grid;   // contenedor con Grid Layout Group
    [SerializeField] TMP_Text titleText;
    [SerializeField] TMP_Text timerText;
    [SerializeField] TMP_Text countsText;

    static readonly string[] Names = { "Rojo", "Azul", "Verde", "Amarillo" };
    static readonly Color[] PlayerColors =
        { Color.red, new Color(0.2f, 0.4f, 1f), Color.green, Color.yellow };
    static readonly Color HoleColor = new Color(0.30f, 0.20f, 0.12f);
    static readonly Color MoleColor = new Color(0.95f, 0.75f, 0.45f);

    Image[] holeImages = new Image[Holes];
    bool[] moleUp = new bool[Holes];
    int mask;
    float endTime;
    bool running;
    bool canPlay;

    void Awake()
    {
        Instance = this;
        for (int i = 0; i < Holes; i++)
        {
            int index = i;
            var go = new GameObject("Hoyo_" + i, typeof(RectTransform), typeof(Image), typeof(Button));
            go.transform.SetParent(grid, false);
            holeImages[i] = go.GetComponent<Image>();
            holeImages[i].color = HoleColor;
            go.GetComponent<Button>().onClick.AddListener(() => OnHoleClicked(index));
        }
        panel.SetActive(false);
    }

    public void Show(int participantsMask, int seconds)
    {
        CancelInvoke();
        mask = participantsMask;
        endTime = Time.time + seconds;
        running = true;

        var provider = GameManager.Instance.LocalPlayerIndexProvider;
        int me = provider != null ? provider() : -1;
        canPlay = me >= 0 && (mask & (1 << me)) != 0;

        for (int i = 0; i < Holes; i++) { moleUp[i] = false; holeImages[i].color = HoleColor; }
        titleText.text = canPlay ? "¡GOLPEA LOS TOPOS!" : "Mirando el minijuego...";
        UpdateCounts(0, 0, 0, 0);
        panel.SetActive(true);
    }

    public void SetMole(int hole, bool up)
    {
        moleUp[hole] = up;
        holeImages[hole].color = up ? MoleColor : HoleColor;
    }

    // Destello del «martillo» del jugador que tocó ese hueco
    public void Hammer(int player, int hole)
    {
        StartCoroutine(Flash(player, hole));
    }

    IEnumerator Flash(int player, int hole)
    {
        holeImages[hole].color = PlayerColors[player];
        yield return new WaitForSeconds(0.25f);
        holeImages[hole].color = moleUp[hole] ? MoleColor : HoleColor;
    }

    public void UpdateCounts(int c0, int c1, int c2, int c3)
    {
        int[] c = { c0, c1, c2, c3 };
        var sb = new StringBuilder();
        for (int i = 0; i < 4; i++)
            if ((mask & (1 << i)) != 0) sb.AppendLine(Names[i] + ": " + c[i]);
        countsText.text = sb.ToString();
    }

    public void End()
    {
        running = false;
        titleText.text = "¡Tiempo!";
        Invoke(nameof(Hide), 1.5f);
    }

    void Hide() { panel.SetActive(false); }

    void Update()
    {
        if (!running) return;
        timerText.text = Mathf.Max(0, Mathf.CeilToInt(endTime - Time.time)).ToString();
    }

    void OnHoleClicked(int hole)
    {
        if (running && canPlay && NetworkBridge.Instance != null)
            NetworkBridge.Instance.SendWhack(hole);
    }
}