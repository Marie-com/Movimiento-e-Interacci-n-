using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// HUD de partida: ronda, turno, tarjetas de jugador, botón de dado y banner de mensajes.
// Solo escucha eventos de GameManager; no cambia reglas ni estado de red.
public class GameHUD : MonoBehaviour
{
    [Header("Contenedor")]
    [SerializeField] CanvasGroup hudGroup;

    [Header("Barra superior")]
    [SerializeField] TMP_Text roundText;
    [SerializeField] Image turnDot;
    [SerializeField] PlayerCardView[] cards;
    [SerializeField] Button pauseButton;
    [SerializeField] PauseMenu pauseMenu;

    [Header("Botón de dado")]
    [SerializeField] Button rollButton;
    [SerializeField] RectTransform rollPulse;   // contenedor que late cuando es tu turno
    [SerializeField] Image rollGlow;

    [Header("Banner de mensajes")]
    [SerializeField] RectTransform infoBanner;
    [SerializeField] CanvasGroup infoGroup;
    [SerializeField] float infoDuration = 4f;

    GameManager gm;
    Coroutine infoRoutine;

    void Start()
    {
        gm = GameManager.Instance;
        gm.GameStarted += OnGameStarted;
        gm.TurnChanged += OnTurnChanged;
        gm.ScoresChanged += OnScoresChanged;
        gm.InfoMessage += OnInfoMessage;
        gm.PieceMoved += OnPieceMoved;
        gm.GameFinished += OnGameFinished;
        gm.GameReset += OnGameReset;

        if (pauseButton != null && pauseMenu != null) pauseButton.onClick.AddListener(pauseMenu.Open);
        for (int i = 0; i < cards.Length; i++) if (cards[i] != null) cards[i].Setup(i);
        if (infoGroup != null) infoGroup.alpha = 0f;

        UIAnim.SetVisible(hudGroup, gm.started);
        if (gm.started) { OnGameStarted(); OnTurnChanged(gm.currentPlayer, gm.round); }
    }

    void OnDestroy()
    {
        if (gm == null) return;
        gm.GameStarted -= OnGameStarted;
        gm.TurnChanged -= OnTurnChanged;
        gm.ScoresChanged -= OnScoresChanged;
        gm.InfoMessage -= OnInfoMessage;
        gm.PieceMoved -= OnPieceMoved;
        gm.GameFinished -= OnGameFinished;
        gm.GameReset -= OnGameReset;
    }

    void OnGameStarted()
    {
        for (int i = 0; i < cards.Length; i++)
        {
            if (cards[i] == null) continue;
            cards[i].gameObject.SetActive(i < gm.activePlayers);
            cards[i].Setup(i);
        }
        UIAnim.SetVisible(hudGroup, true);
    }

    void OnTurnChanged(int player, int round)
    {
        if (roundText != null) roundText.text = "RONDA <b>" + round + "</b> / " + GameManager.MaxRounds;
        if (turnDot != null)
        {
            turnDot.color = UIThemeConfig.PlayerColor(player);
            StartCoroutine(UIAnim.Pop(turnDot.transform, 1.6f, 0.5f));
        }
        for (int i = 0; i < cards.Length; i++) if (cards[i] != null) cards[i].SetTurn(i == player);
        if (gm.IsLocalTurn) AudioManager.PlaySfx(AudioManager.Sfx.TurnAlert);
    }

    void OnScoresChanged(int[] scores)
    {
        for (int i = 0; i < cards.Length && i < scores.Length; i++)
            if (cards[i] != null) cards[i].SetScore(scores[i]);
    }

    void OnPieceMoved(int player, int from, int to)
    {
        AudioManager.PlaySfx(AudioManager.Sfx.DiceRoll);
    }

    void OnGameFinished(int[] finalScores)
    {
        for (int i = 0; i < cards.Length; i++) if (cards[i] != null) cards[i].SetTurn(false);
    }

    void OnGameReset()
    {
        if (infoRoutine != null) StopCoroutine(infoRoutine);
        if (infoGroup != null) infoGroup.alpha = 0f;
        UIAnim.SetVisible(hudGroup, false);
    }

    void OnInfoMessage(string message)
    {
        if (string.IsNullOrEmpty(message) || infoGroup == null) return;
        if (infoRoutine != null) StopCoroutine(infoRoutine);
        infoRoutine = StartCoroutine(InfoRoutine());
    }

    IEnumerator InfoRoutine()
    {
        infoGroup.alpha = 1f;
        StartCoroutine(UIAnim.Pop(infoBanner, 0.82f, 0.45f));
        yield return new WaitForSecondsRealtime(infoDuration);
        yield return UIAnim.Fade(infoGroup, 0f, 0.4f);
        infoRoutine = null;
    }

    void Update()
    {
        if (gm == null || rollPulse == null) return;
        bool ready = rollButton != null && rollButton.interactable && gm.IsLocalTurn;
        float wave = (Mathf.Sin(Time.unscaledTime * 5f) + 1f) * 0.5f;
        float s = ready ? 1f + 0.045f * wave : 1f;
        rollPulse.localScale = new Vector3(s, s, 1f);
        if (rollGlow != null)
        {
            Color c = rollGlow.color;
            c.a = ready ? 0.25f + 0.35f * wave : 0f;
            rollGlow.color = c;
        }
    }
}
