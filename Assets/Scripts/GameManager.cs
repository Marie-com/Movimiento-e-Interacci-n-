using System;
using System.Collections;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    public const int MaxPlayers = 4;
    public const int MaxRounds = 8; // gana quien tenga más puntos al terminar estas rondas

    [Header("Interfaz")]
    [SerializeField] TMP_Text turnText;
    [SerializeField] TMP_Text diceText;
    [SerializeField] TMP_Text scoreText;
    [SerializeField] Button rollButton;

    [Header("Apuesta de duelo")]
    [SerializeField] GameObject betPanel;
    [SerializeField] Slider betSlider;
    [SerializeField] TMP_Text betText;
    [SerializeField] Button betConfirmButton;

    [Header("Pruebas")]
    [SerializeField] bool autoStartLocal = true; // se desmarca en el modo online

    public int activePlayers;
    public int[] positions = new int[MaxPlayers];
    public int[] scores = new int[MaxPlayers];
    public int currentPlayer;
    public int round = 1;
    public bool started;
    public bool gameOver;
    public bool busy;

    public event Action GameStarted;
    public event Action<int, int, int> PieceMoved;

    // Ganchos para el modo online (null = modo local)
    public Func<int> LocalPlayerIndexProvider;
    public Action<int> RollRequestHandler;
    public Action<int> ChallengeStartHandler;
    public Action<int, int> DuelBetHandler;

    static readonly string[] Names = { "Rojo", "Azul", "Verde", "Amarillo" };

    // Resultados que llegan del anfitrión (o se calculan en modo local)
    bool resultReady;
    int[] challengeDeltas = new int[MaxPlayers];
    int duelWinner, duelLoser, duelAmount;
    int duelPlayer, duelRival;
    bool betConfirmed;

    void Awake() { Instance = this; }

    void Start()
    {
        rollButton.onClick.AddListener(RequestRoll);
        betConfirmButton.onClick.AddListener(ConfirmBet);
        betSlider.wholeNumbers = true;
        betSlider.onValueChanged.AddListener(v => betText.text = "Apuesta: " + Mathf.RoundToInt(v) + " pts");
        betPanel.SetActive(false);

        if (autoStartLocal) StartGame(MaxPlayers);
        else RefreshUI();
    }

    public void StartGame(int players)
    {
        activePlayers = players;
        for (int i = 0; i < MaxPlayers; i++) { positions[i] = 0; scores[i] = 0; }
        currentPlayer = 0;
        round = 1;
        gameOver = false;
        busy = false;
        resultReady = false;
        started = true;
        GameStarted?.Invoke();
        RefreshUI();
    }

    bool IsLocalPlayer(int player)
    {
        return LocalPlayerIndexProvider == null || LocalPlayerIndexProvider() == player;
    }

    bool IsMyTurn { get { return IsLocalPlayer(currentPlayer); } }

    public void RequestRoll()
    {
        if (!started || gameOver || busy || !IsMyTurn) return;
        if (RollRequestHandler != null) RollRequestHandler(currentPlayer);
        else ApplyRoll(currentPlayer, UnityEngine.Random.Range(1, 7));
    }

    // Se ejecuta IGUAL en todos los celulares
    public void ApplyRoll(int player, int steps)
    {
        if (busy || gameOver) return;
        int from = positions[player];
        int to = (from + steps) % BoardLayout.TileCount; // el circuito es cerrado
        positions[player] = to;
        busy = true;
        resultReady = false;
        ShowInfo(Names[player] + " sacó " + steps);
        PieceMoved?.Invoke(player, from, to);
        RefreshUI();
        StartCoroutine(TurnRoutine(player, steps, to));
    }

    IEnumerator TurnRoutine(int player, int steps, int to)
    {
        // 1. Esperar a que termine la animación del movimiento
        yield return new WaitForSeconds(steps * 0.35f + 0.3f);

        // 2. Resolver la casilla
        int rival = FindRivalOn(player, to);
        if (rival >= 0)
        {
            yield return DuelRoutine(player, rival);
        }
        else
        {
            switch (BoardLayout.TypeAt(to))
            {
                case TileType.Puntos:
                case TileType.Trampa:
                    AddPoints(player, BoardLayout.PointsAt(to));
                    break;
                case TileType.Reto:
                    yield return ChallengeRoutine(player);
                    break;
            }
        }

        // 3. El turno termina cuando los puntos ya se ganaron o se perdieron
        yield return new WaitForSeconds(1f);
        EndTurn();
    }

    // ---------- RETO (todos juegan; simulado hasta la Fase 6) ----------
    IEnumerator ChallengeRoutine(int player)
    {
        ShowInfo("¡Reto para todos! (simulado)");
        yield return new WaitForSeconds(1f);

        if (ChallengeStartHandler != null) ChallengeStartHandler(player); // online: lo decide el anfitrión
        else SetChallengeResult(BuildRandomChallengeDeltas(activePlayers)); // local

        yield return new WaitUntil(() => resultReady);
        resultReady = false;

        var sb = new StringBuilder("Reto: ");
        for (int i = 0; i < activePlayers; i++)
        {
            scores[i] += challengeDeltas[i];
            sb.Append(Names[i]).Append(" +").Append(challengeDeltas[i]).Append("  ");
        }
        ShowInfo(sb.ToString());
        RefreshUI();
        yield return new WaitForSeconds(1.5f);
    }

    public static int[] BuildRandomChallengeDeltas(int players)
    {
        int[] reward = { 20, 10, 5, 0 }; // puntos para el 1.º, 2.º, 3.º y 4.º
        int[] order = new int[players];
        for (int i = 0; i < players; i++) order[i] = i;
        for (int i = players - 1; i > 0; i--)
        {
            int j = UnityEngine.Random.Range(0, i + 1);
            int t = order[i]; order[i] = order[j]; order[j] = t;
        }
        int[] d = new int[MaxPlayers];
        for (int rank = 0; rank < players; rank++) d[order[rank]] = reward[rank];
        return d;
    }

    public void SetChallengeResult(int[] deltas)
    {
        for (int i = 0; i < MaxPlayers; i++) challengeDeltas[i] = deltas[i];
        resultReady = true;
    }

    // ---------- DUELO (simulado hasta la Fase 6) ----------
    IEnumerator DuelRoutine(int player, int rival)
    {
        duelPlayer = player;
        duelRival = rival;
        int cap = Mathf.Min(scores[player], scores[rival]);
        ShowInfo("¡Duelo! " + Names[player] + " vs " + Names[rival]);
        yield return new WaitForSeconds(1f);

        if (cap <= 0)
        {
            ShowInfo("Alguien tiene 0 puntos: no hay apuesta.");
            yield return new WaitForSeconds(1.5f);
            yield break;
        }

        if (IsLocalPlayer(player))
        {
            betConfirmed = false;
            betSlider.minValue = 1;
            betSlider.maxValue = cap;
            betSlider.value = Mathf.Min(5, cap);
            betText.text = "Apuesta: " + Mathf.RoundToInt(betSlider.value) + " pts";
            betPanel.SetActive(true);
            yield return new WaitUntil(() => betConfirmed);
            betPanel.SetActive(false);
        }
        else
        {
            ShowInfo(Names[player] + " está eligiendo la apuesta...");
        }

        yield return new WaitUntil(() => resultReady);
        resultReady = false;
        Transfer(duelWinner, duelLoser, duelAmount);
        yield return new WaitForSeconds(1.5f);
    }

    void ConfirmBet()
    {
        if (betConfirmed) return;
        betConfirmed = true;
        int bet = Mathf.RoundToInt(betSlider.value);
        if (DuelBetHandler != null) DuelBetHandler(duelPlayer, bet); // online: lo decide el anfitrión
        else
        {
            bool arriverWins = UnityEngine.Random.value < 0.5f;
            SetDuelResult(arriverWins ? duelPlayer : duelRival, arriverWins ? duelRival : duelPlayer, bet);
        }
    }

    public void SetDuelResult(int winner, int loser, int amount)
    {
        duelWinner = winner; duelLoser = loser; duelAmount = amount;
        resultReady = true;
    }

    public int CurrentRival(int player) { return FindRivalOn(player, positions[player]); }

    int FindRivalOn(int player, int tile)
    {
        if (tile == 0) return -1; // en el inicio no hay duelos
        for (int i = 0; i < activePlayers; i++)
            if (i != player && positions[i] == tile) return i;
        return -1;
    }

    // ---------- Puntos, turnos y fin del juego ----------
    void AddPoints(int player, int delta)
    {
        scores[player] = Mathf.Max(0, scores[player] + delta);
        ShowInfo(Names[player] + (delta >= 0 ? " gana " : " pierde ") + Mathf.Abs(delta) + " puntos");
        RefreshUI();
    }

    void Transfer(int winner, int loser, int amount)
    {
        int real = Mathf.Min(amount, scores[loser]);
        scores[loser] -= real;
        scores[winner] += real;
        ShowInfo(Names[winner] + " le quita " + real + " puntos a " + Names[loser]);
        RefreshUI();
    }

    void EndTurn()
    {
        int next = currentPlayer + 1;
        if (next >= activePlayers)
        {
            next = 0;
            round++;
            if (round > MaxRounds) { FinishGame(); return; }
        }
        currentPlayer = next;
        busy = false;
        RefreshUI();
    }

    void FinishGame()
    {
        gameOver = true;
        busy = false;
        int best = -1;
        for (int i = 0; i < activePlayers; i++) best = Mathf.Max(best, scores[i]);
        string winners = "";
        for (int i = 0; i < activePlayers; i++)
            if (scores[i] == best) winners += (winners == "" ? "" : " y ") + Names[i];
        ShowInfo("¡Fin del juego! Gana " + winners + " con " + best + " pts");
        RefreshUI();
    }

    void ShowInfo(string message) { diceText.text = message; }

    void RefreshUI()
    {
        if (!started)
        {
            turnText.text = "Esperando...";
            rollButton.interactable = false;
            return;
        }

        var sb = new StringBuilder();
        for (int i = 0; i < activePlayers; i++)
            sb.AppendLine(Names[i] + ": " + scores[i] + " pts");
        scoreText.text = sb.ToString();

        if (gameOver)
        {
            turnText.text = "Fin del juego";
            rollButton.interactable = false;
            return;
        }

        turnText.text = "Ronda " + round + "/" + MaxRounds + " - Turno: " + Names[currentPlayer];
        rollButton.interactable = !busy && IsMyTurn;
    }
}