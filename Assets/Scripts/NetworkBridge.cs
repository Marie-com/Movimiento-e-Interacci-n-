using System.Collections;
using System.Collections.Generic;
using Unity.Netcode;
using UnityEngine;

public class NetworkBridge : NetworkBehaviour
{
    public static NetworkBridge Instance { get; private set; }

    const int GameCount = 3;                              // 0 = tap, 1 = topos, 2 = patos
    static readonly int[] Seconds = { 10, 20, 20 };       // duración de cada minijuego

    // Estado del minijuego (el anfitrión lo controla; los demás solo leen currentGame)
    int[] points = new int[4];
    int minigameMask;
    bool minigameOn;
    int currentGame;

    // Solo anfitrión: topos
    bool[] hostMole = new bool[MoleMinigame.Holes];
    float[] hostMoleEnd = new float[MoleMinigame.Holes];

    // Solo anfitrión: patos (id -> momento en que sale de pantalla)
    Dictionary<int, float> hostDucks = new Dictionary<int, float>();
    int nextDuckId;

    public override void OnNetworkSpawn()
    {
        Instance = this;
        var gm = GameManager.Instance;
        gm.LocalPlayerIndexProvider = () => (int)NetworkManager.Singleton.LocalClientId;
        gm.RollRequestHandler = RollRequestRpc;
        gm.ChallengeStartHandler = ChallengeStart;
        gm.DuelBetHandler = DuelBetRpc;
    }

    public override void OnNetworkDespawn()
    {
        var gm = GameManager.Instance;
        if (gm != null)
        {
            gm.LocalPlayerIndexProvider = null;
            gm.RollRequestHandler = null;
            gm.ChallengeStartHandler = null;
            gm.DuelBetHandler = null;
        }
        if (Instance == this) Instance = null;
    }

    // Devuelve la pantalla del minijuego según su número
    IMinigameView View(int game)
    {
        switch (game)
        {
            case 0: return TapMinigame.Instance;
            case 1: return MoleMinigame.Instance;
            default: return DuckMinigame.Instance;
        }
    }

    // ----- Dado -----
    [Rpc(SendTo.Server)]
    void RollRequestRpc(int playerIndex)
    {
        var gm = GameManager.Instance;
        if (!gm.started || gm.gameOver || gm.busy || playerIndex != gm.currentPlayer) return;
        RollResultRpc(playerIndex, 5);
    }

    [Rpc(SendTo.Everyone)]
    void RollResultRpc(int playerIndex, int steps)
    {
        GameManager.Instance.ApplyRoll(playerIndex, steps);
    }

    // ----- Reto: juegan todos -----
    void ChallengeStart(int player)
    {
        if (!IsServer || minigameOn) return;
        int n = GameManager.Instance.activePlayers;
        StartCoroutine(RunMinigame((1 << n) - 1, false, 0, 0, 0));
    }

    // ----- Duelo: el que llegó manda la apuesta, juegan solo los 2 -----
    [Rpc(SendTo.Server)]
    void DuelBetRpc(int player, int bet)
    {
        var gm = GameManager.Instance;
        if (minigameOn || !gm.busy || player != gm.currentPlayer) return;
        int rival = gm.CurrentRival(player);
        if (rival < 0) return;
        int cap = Mathf.Min(gm.scores[player], gm.scores[rival]);
        if (cap <= 0) return;
        bet = Mathf.Clamp(bet, 1, cap);
        StartCoroutine(RunMinigame((1 << player) | (1 << rival), true, player, rival, bet));
    }

    // ----- El anfitrión es el árbitro de cualquier minijuego -----
    IEnumerator RunMinigame(int participants, bool duel, int p, int r, int bet)
    {
        int game = 0;
        for (int i = 0; i < 4; i++) points[i] = 0;
        currentGame = game;
        minigameMask = participants;
        minigameOn = true;
        MinigameStartRpc(game, participants, Seconds[game]);

        if (game == 0) yield return StartCoroutine(TapLoop(Seconds[game]));
        else if (game == 1) yield return StartCoroutine(MoleLoop(Seconds[game]));
        else yield return StartCoroutine(DuckLoop(Seconds[game]));

        minigameOn = false;
        SendScores();
        MinigameEndRpc(game);

        if (duel)
        {
            bool arriverWins = points[p] > points[r] ? true : points[r] > points[p] ? false : Random.value < 0.5f;
            DuelResultRpc(arriverWins ? p : r, arriverWins ? r : p, bet);
        }
        else
        {
            int n = GameManager.Instance.activePlayers;
            int[] reward = { 20, 10, 5, 0 };
            int[] d = new int[4];
            int max = 0, min = int.MaxValue;
            for (int i = 0; i < n; i++) { max = Mathf.Max(max, points[i]); min = Mathf.Min(min, points[i]); }
            if (max != min) // si todos empatan, nadie gana puntos
            {
                for (int i = 0; i < n; i++)
                {
                    int rank = 0;
                    for (int j = 0; j < n; j++) if (points[j] > points[i]) rank++;
                    d[i] = reward[Mathf.Min(rank, 3)];
                }
            }
            ChallengeResultRpc(d[0], d[1], d[2], d[3]);
        }
    }

    void SendScores()
    {
        ScoresRpc(points[0], points[1], points[2], points[3]);
    }

    // ----- Minijuego 0: quien más toque -----
    IEnumerator TapLoop(int seconds)
    {
        float end = Time.time + seconds;
        float nextSend = 0f;
        while (Time.time < end)
        {
            if (Time.time >= nextSend) { SendScores(); nextSend = Time.time + 0.2f; }
            yield return null;
        }
    }

    public void SendTap()
    {
        if (!IsSpawned) return;
        TapRpc((int)NetworkManager.Singleton.LocalClientId);
    }

    [Rpc(SendTo.Server)]
    void TapRpc(int player)
    {
        if (!minigameOn || currentGame != 0 || player < 0 || player > 3) return;
        if ((minigameMask & (1 << player)) == 0) return;
        points[player]++;
    }

    // ----- Minijuego 1: topos -----
    IEnumerator MoleLoop(int seconds)
    {
        for (int h = 0; h < MoleMinigame.Holes; h++) hostMole[h] = false;
        float end = Time.time + seconds;
        float nextSpawn = Time.time + 0.5f;
        float nextSend = 0f;
        while (Time.time < end)
        {
            for (int h = 0; h < MoleMinigame.Holes; h++)
            {
                if (hostMole[h] && Time.time >= hostMoleEnd[h])
                {
                    hostMole[h] = false;
                    MoleSetRpc(h, false);
                }
            }
            if (Time.time >= nextSpawn)
            {
                int h = Random.Range(0, MoleMinigame.Holes);
                if (!hostMole[h])
                {
                    hostMole[h] = true;
                    hostMoleEnd[h] = Time.time + 1.2f;
                    MoleSetRpc(h, true);
                }
                nextSpawn = Time.time + 0.6f;
            }
            if (Time.time >= nextSend) { SendScores(); nextSend = Time.time + 0.2f; }
            yield return null;
        }
        for (int h = 0; h < MoleMinigame.Holes; h++) hostMole[h] = false;
    }

    public void SendWhack(int hole)
    {
        if (!IsSpawned) return;
        WhackRpc((int)NetworkManager.Singleton.LocalClientId, hole);
    }

    [Rpc(SendTo.Server)]
    void WhackRpc(int player, int hole)
    {
        if (!minigameOn || currentGame != 1) return;
        if (player < 0 || player > 3 || hole < 0 || hole >= MoleMinigame.Holes) return;
        if ((minigameMask & (1 << player)) == 0) return;
        bool hit = hostMole[hole];
        if (hit)
        {
            hostMole[hole] = false;
            points[player]++;
        }
        WhackEventRpc(player, hole, hit);
    }

    [Rpc(SendTo.Everyone)]
    void MoleSetRpc(int hole, bool up)
    {
        MoleMinigame.Instance.SetMole(hole, up);
    }

    [Rpc(SendTo.Everyone)]
    void WhackEventRpc(int player, int hole, bool hit)
    {
        if (hit) MoleMinigame.Instance.SetMole(hole, false);
        MoleMinigame.Instance.Hammer(player, hole);
    }

    // ----- Minijuego 2: patos -----
    IEnumerator DuckLoop(int seconds)
    {
        hostDucks.Clear();
        float end = Time.time + seconds;
        float nextSpawn = Time.time + 0.5f;
        float nextSend = 0f;
        while (Time.time < end)
        {
            if (Time.time >= nextSpawn)
            {
                int id = nextDuckId++;
                float duration = Random.Range(2.5f, 4f);
                float height = Random.Range(0.15f, 0.85f);
                bool toRight = Random.value < 0.5f;
                hostDucks[id] = Time.time + duration;
                DuckSpawnRpc(id, height, toRight, duration);
                nextSpawn = Time.time + Random.Range(0.5f, 1.0f);
            }
            if (Time.time >= nextSend) { SendScores(); nextSend = Time.time + 0.2f; }
            yield return null;
        }
        hostDucks.Clear();
    }

    public void SendShoot(int duckId)
    {
        if (!IsSpawned) return;
        ShootRpc((int)NetworkManager.Singleton.LocalClientId, duckId);
    }

    public void SendShot(float nx, float ny)
    {
        if (!IsSpawned) return;
        ShotRpc((int)NetworkManager.Singleton.LocalClientId, nx, ny);
    }

    // El primer disparo que llegue al anfitrión se lleva el pato
    [Rpc(SendTo.Server)]
    void ShootRpc(int player, int duckId)
    {
        if (!minigameOn || currentGame != 2 || player < 0 || player > 3) return;
        if ((minigameMask & (1 << player)) == 0) return;
        float expire;
        if (hostDucks.TryGetValue(duckId, out expire) && Time.time <= expire + 0.3f)
        {
            hostDucks.Remove(duckId);
            points[player]++;
            DuckHitRpc(player, duckId);
        }
    }

    // Disparo al cielo: solo se reenvía para que todos vean el punto de color
    [Rpc(SendTo.Server)]
    void ShotRpc(int player, float nx, float ny)
    {
        if (!minigameOn || currentGame != 2 || player < 0 || player > 3) return;
        if ((minigameMask & (1 << player)) == 0) return;
        ShotEventRpc(player, nx, ny);
    }

    [Rpc(SendTo.Everyone)]
    void DuckSpawnRpc(int id, float height, bool toRight, float duration)
    {
        DuckMinigame.Instance.SpawnDuck(id, height, toRight, duration);
    }

    [Rpc(SendTo.Everyone)]
    void DuckHitRpc(int player, int duckId)
    {
        DuckMinigame.Instance.DuckHit(player, duckId);
    }

    [Rpc(SendTo.Everyone)]
    void ShotEventRpc(int player, float nx, float ny)
    {
        DuckMinigame.Instance.ShotMarker(player, nx, ny);
    }

    // ----- Mensajes comunes de los minijuegos -----
    [Rpc(SendTo.Everyone)]
    void MinigameStartRpc(int game, int participants, int seconds)
    {
        currentGame = game;
        View(game).Show(participants, seconds);
    }

    [Rpc(SendTo.Everyone)]
    void ScoresRpc(int c0, int c1, int c2, int c3)
    {
        View(currentGame).UpdateCounts(c0, c1, c2, c3);
    }

    [Rpc(SendTo.Everyone)]
    void MinigameEndRpc(int game)
    {
        View(game).End();
    }

    // ----- Resultados (igual que en la Fase 5) -----
    [Rpc(SendTo.Everyone)]
    void ChallengeResultRpc(int d0, int d1, int d2, int d3)
    {
        GameManager.Instance.SetChallengeResult(new int[] { d0, d1, d2, d3 });
    }

    [Rpc(SendTo.Everyone)]
    void DuelResultRpc(int winner, int loser, int amount)
    {
        GameManager.Instance.SetDuelResult(winner, loser, amount);
    }

    // ----- Inicio de partida -----
    public void StartGameForAll(int players)
    {
        if (IsServer) StartGameRpc(players);
    }

    [Rpc(SendTo.Everyone)]
    void StartGameRpc(int players)
    {
        GameManager.Instance.StartGame(players);
    }
}