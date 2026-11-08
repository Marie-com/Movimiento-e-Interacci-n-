using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

// Ayudante: avisa en qué punto se tocó el cielo
public class ClickPositionRelay : MonoBehaviour, IPointerClickHandler
{
    public System.Action<PointerEventData> Clicked;
    public void OnPointerClick(PointerEventData e) { if (Clicked != null) Clicked(e); }
}

public class DuckMinigame : MonoBehaviour, IMinigameView
{
    public static DuckMinigame Instance { get; private set; }

    [SerializeField] GameObject panel;
    [SerializeField] RectTransform field;   // el cielo (Image que ocupa toda la pantalla)
    [SerializeField] TMP_Text titleText;
    [SerializeField] TMP_Text timerText;
    [SerializeField] TMP_Text countsText;

    static readonly string[] Names = { "Rojo", "Azul", "Verde", "Amarillo" };
    static readonly Color[] PlayerColors =
        { Color.red, new Color(0.2f, 0.4f, 1f), Color.green, Color.yellow };
    static readonly Color DuckColor = new Color(0.55f, 0.35f, 0.15f);

    class Duck
    {
        public int id;
        public RectTransform rt;
        public Image img;
        public float spawn, duration, startX, endX;
    }

    List<Duck> ducks = new List<Duck>();
    int mask;
    float endTime;
    bool running;
    bool canPlay;

    void Awake()
    {
        Instance = this;
        var relay = field.gameObject.AddComponent<ClickPositionRelay>();
        relay.Clicked = OnFieldClicked;
        panel.SetActive(false);
    }

    public void Show(int participantsMask, int seconds)
    {
        CancelInvoke();
        ClearDucks();
        mask = participantsMask;
        endTime = Time.time + seconds;
        running = true;

        var provider = GameManager.Instance.LocalPlayerIndexProvider;
        int me = provider != null ? provider() : -1;
        canPlay = me >= 0 && (mask & (1 << me)) != 0;

        titleText.text = canPlay ? "¡DISPARA A LOS PATOS!" : "Mirando el minijuego...";
        UpdateCounts(0, 0, 0, 0);
        panel.SetActive(true);
    }

    void ClearDucks()
    {
        foreach (var d in ducks)
            if (d.rt != null) Destroy(d.rt.gameObject);
        ducks.Clear();
    }

    public void SpawnDuck(int id, float ny, bool toRight, float duration)
    {
        if (!running) return;
        float edge = field.rect.width * 0.5f + 100f;
        float y = Rect.NormalizedToPoint(field.rect, new Vector2(0.5f, ny)).y;

        var go = new GameObject("Pato_" + id, typeof(RectTransform), typeof(Image), typeof(Button));
        var rt = (RectTransform)go.transform;
        rt.SetParent(field, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(140f, 90f);
        var img = go.GetComponent<Image>();
        img.color = DuckColor;
        go.GetComponent<Button>().onClick.AddListener(() => OnDuckClicked(id));

        var d = new Duck
        {
            id = id,
            rt = rt,
            img = img,
            spawn = Time.time,
            duration = duration,
            startX = toRight ? -edge : edge,
            endX = toRight ? edge : -edge
        };
        rt.anchoredPosition = new Vector2(d.startX, y);
        ducks.Add(d);
    }

    public void DuckHit(int player, int id)
    {
        for (int i = 0; i < ducks.Count; i++)
        {
            if (ducks[i].id != id) continue;
            var d = ducks[i];
            ducks.RemoveAt(i);
            d.img.color = PlayerColors[player];
            d.rt.GetComponent<Button>().interactable = false;
            Destroy(d.rt.gameObject, 0.3f);
            return;
        }
    }

    // Punto de color donde otro jugador (o tú) disparó al cielo
    public void ShotMarker(int player, float nx, float ny)
    {
        var go = new GameObject("Disparo", typeof(RectTransform), typeof(Image));
        var rt = (RectTransform)go.transform;
        rt.SetParent(field, false);
        rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
        rt.sizeDelta = new Vector2(40f, 40f);
        rt.anchoredPosition = Rect.NormalizedToPoint(field.rect, new Vector2(nx, ny));
        var img = go.GetComponent<Image>();
        img.color = PlayerColors[player];
        img.raycastTarget = false;
        Destroy(go, 0.3f);
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
        ClearDucks();
        titleText.text = "¡Tiempo!";
        Invoke(nameof(Hide), 1.5f);
    }

    void Hide() { panel.SetActive(false); }

    void Update()
    {
        if (!running) return;
        timerText.text = Mathf.Max(0, Mathf.CeilToInt(endTime - Time.time)).ToString();

        for (int i = ducks.Count - 1; i >= 0; i--)
        {
            var d = ducks[i];
            float p = (Time.time - d.spawn) / d.duration;
            if (p >= 1f)
            {
                Destroy(d.rt.gameObject);
                ducks.RemoveAt(i);
                continue;
            }
            Vector2 pos = d.rt.anchoredPosition;
            pos.x = Mathf.Lerp(d.startX, d.endX, p);
            d.rt.anchoredPosition = pos;
        }
    }

    void OnDuckClicked(int id)
    {
        if (running && canPlay && NetworkBridge.Instance != null)
            NetworkBridge.Instance.SendShoot(id);
    }

    void OnFieldClicked(PointerEventData e)
    {
        if (!running || !canPlay || NetworkBridge.Instance == null) return;
        Vector2 local;
        if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(field, e.position, e.pressEventCamera, out local)) return;
        Vector2 n = Rect.PointToNormalized(field.rect, local);
        NetworkBridge.Instance.SendShot(n.x, n.y);
    }
}
