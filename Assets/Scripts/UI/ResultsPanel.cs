using System.Collections;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Podio de fin de partida. Aparece automáticamente con GameManager.GameFinished.
public class ResultsPanel : MonoBehaviour
{
    [SerializeField] GameObject panel;
    [SerializeField] RectTransform card;
    [SerializeField] Image crown;
    [SerializeField] TMP_Text winnerText;
    [SerializeField] ResultRowView[] rows;
    [SerializeField] Button rematchButton;
    [SerializeField] TMP_Text waitingText;
    [SerializeField] Button menuButton;
    [SerializeField] OnlineMenu onlineMenu;
    [SerializeField] float showDelay = 1.6f;

    GameManager gm;
    Coroutine showRoutine;
    bool busy;

    void Start()
    {
        gm = GameManager.Instance;
        gm.GameFinished += OnGameFinished;
        gm.GameStarted += Hide;
        if (onlineMenu != null) onlineMenu.ReturnedToMenu += Hide;
        if (rematchButton != null) rematchButton.onClick.AddListener(Rematch);
        if (menuButton != null) menuButton.onClick.AddListener(BackToMenu);
        panel.SetActive(false);
    }

    void OnDestroy()
    {
        if (gm != null)
        {
            gm.GameFinished -= OnGameFinished;
            gm.GameStarted -= Hide;
        }
        if (onlineMenu != null) onlineMenu.ReturnedToMenu -= Hide;
    }

    void OnGameFinished(int[] finalScores)
    {
        if (showRoutine != null) StopCoroutine(showRoutine);
        showRoutine = StartCoroutine(ShowRoutine(finalScores));
    }

    IEnumerator ShowRoutine(int[] scores)
    {
        yield return new WaitForSecondsRealtime(showDelay);
        Fill(scores);
        panel.SetActive(true);
        if (card != null) StartCoroutine(UIAnim.Pop(card, 0.75f, 0.55f));
        if (crown != null) StartCoroutine(UIAnim.Pop(crown.transform, 0.3f, 0.8f));
        AudioManager.PlaySfx(AudioManager.Sfx.Victory);
        showRoutine = null;
    }

    void Fill(int[] scores)
    {
        int n = gm.activePlayers;

        // Orden de mayor a menor puntaje (estable por número de jugador)
        int[] order = new int[n];
        for (int i = 0; i < n; i++) order[i] = i;
        for (int i = 1; i < n; i++)
        {
            int key = order[i], j = i - 1;
            while (j >= 0 && scores[order[j]] < scores[key]) { order[j + 1] = order[j]; j--; }
            order[j + 1] = key;
        }

        int best = n > 0 ? scores[order[0]] : 0;
        string winners = "";
        int winnerCount = 0;
        for (int i = 0; i < n; i++)
        {
            if (scores[i] != best) continue;
            winners += (winnerCount == 0 ? "" : " y ") + UIThemeConfig.ColoredName(i);
            winnerCount++;
        }

        if (winnerText != null)
            winnerText.text = winnerCount > 1 ? "¡EMPATE! " + winners : "¡GANA " + winners + "!";
        if (crown != null)
            crown.color = winnerCount == 1 ? Color.Lerp(UIThemeConfig.Gold, UIThemeConfig.PlayerColor(order[0]), 0.25f) : UIThemeConfig.Gold;

        for (int r = 0; r < rows.Length; r++)
        {
            if (rows[r] == null) continue;
            bool used = r < n;
            rows[r].gameObject.SetActive(used);
            if (!used) continue;
            int player = order[r];
            int place = 1;
            for (int k = 0; k < n; k++) if (scores[k] > scores[player]) place++;
            rows[r].Set(place, player, scores[player], scores[player] == best);
        }

        bool canRematch = !gm.IsOnline || (NetworkBridge.Instance != null && NetworkBridge.Instance.IsServer);
        if (rematchButton != null) rematchButton.gameObject.SetActive(canRematch);
        if (waitingText != null) waitingText.gameObject.SetActive(!canRematch);
    }

    void Rematch()
    {
        if (gm.IsOnline)
        {
            if (NetworkBridge.Instance != null) NetworkBridge.Instance.StartGameForAll(gm.activePlayers);
        }
        else
        {
            gm.StartGame(gm.activePlayers);
        }
    }

    async void BackToMenu()
    {
        if (busy || onlineMenu == null) return;
        busy = true;
        try { await onlineMenu.LeaveSessionAndResetAsync(); }
        finally { busy = false; Hide(); }
    }

    void Hide()
    {
        if (showRoutine != null) { StopCoroutine(showRoutine); showRoutine = null; }
        if (panel != null) panel.SetActive(false);
    }
}
