using System;
using System.Threading.Tasks;
using TMPro;
using Unity.Netcode;
using Unity.Services.Authentication;
using Unity.Services.Core;
using Unity.Services.Multiplayer;
using UnityEngine;
using UnityEngine.UI;

public class OnlineMenu : MonoBehaviour
{
    [SerializeField] GameObject menuPanel;
    [SerializeField] Button createButton;
    [SerializeField] Button joinButton;
    [SerializeField] Button startButton;
    [SerializeField] TMP_InputField codeInput;
    [SerializeField] TMP_Text statusText;

    [Header("Sala (opcional)")]
    [SerializeField] GameObject roomCodeBadge;
    [SerializeField] TMP_Text roomCodeText;
    [SerializeField] TMP_Text playersText;

    [Header("Partida local (sin red)")]
    [SerializeField] Button localPlayButton;
    [SerializeField] Button[] localCountButtons;     // 2, 3 y 4 jugadores
    [SerializeField] GameObject[] localCountSelected; // marca visual de la opción elegida
    [SerializeField, Range(2, 4)] int selectedLocalPlayers = 4;

    ISession session;
    bool servicesReady;
    bool leaving;

    public bool InSession { get { return session != null; } }
    public bool IsHost { get { return session != null && session.IsHost; } }

    // Se dispara cuando la UI volvió al menú principal (pausa y resultados se cierran solos)
    public event Action ReturnedToMenu;

    async void Start()
    {
        startButton.gameObject.SetActive(false);
        createButton.interactable = false;
        joinButton.interactable = false;
        createButton.onClick.AddListener(CreateSession);
        joinButton.onClick.AddListener(JoinSession);
        startButton.onClick.AddListener(StartMatch);
        GameManager.Instance.GameStarted += () => menuPanel.SetActive(false);

        SetupLocalMode();
        RefreshRoomInfo();

        try
        {
            statusText.text = "Conectando a los servicios...";
            await UnityServices.InitializeAsync();
            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            servicesReady = true;
            statusText.text = "Listo. Crea una partida o únete con un código.";
            createButton.interactable = true;
            joinButton.interactable = true;
        }
        catch (Exception e)
        {
            statusText.text = "Sin conexión: puedes jugar en modo local.\n<size=80%>" + e.Message + "</size>";
            Debug.LogException(e);
        }
    }

    async void CreateSession()
    {
        try
        {
            createButton.interactable = false;
            joinButton.interactable = false;
            statusText.text = "Creando sala...";
            var options = new SessionOptions { MaxPlayers = 4 }.WithRelayNetwork();
            session = await MultiplayerService.Instance.CreateSessionAsync(options);
            HookSession();
            startButton.gameObject.SetActive(true);
            ShowHostStatus();
        }
        catch (Exception e)
        {
            statusText.text = "Error al crear: " + e.Message;
            Debug.LogException(e);
            createButton.interactable = servicesReady;
            joinButton.interactable = servicesReady;
        }
    }

    void OnPlayerJoined(string playerId)
    {
        ShowHostStatus();
    }

    void OnPlayersChanged(string playerId)
    {
        RefreshRoomInfo();
        if (session != null && session.IsHost) ShowHostStatus();
    }

    void ShowHostStatus()
    {
        if (session == null) return;
        statusText.text = "Comparte el código de la sala.\nJugadores: " + session.Players.Count + "/4";
        RefreshRoomInfo();
    }

    async void JoinSession()
    {
        try
        {
            string code = codeInput.text.Trim().ToUpper();
            if (string.IsNullOrEmpty(code)) { statusText.text = "Escribe el código de la sala."; return; }
            createButton.interactable = false;
            joinButton.interactable = false;
            statusText.text = "Entrando a la sala...";
            session = await MultiplayerService.Instance.JoinSessionByCodeAsync(code);
            HookSession();
            statusText.text = "Conectado. Esperando a que el anfitrión empiece...";
            RefreshRoomInfo();
        }
        catch (Exception e)
        {
            statusText.text = "No se pudo entrar: " + e.Message;
            Debug.LogException(e);
            createButton.interactable = servicesReady;
            joinButton.interactable = servicesReady;
        }
    }

    void StartMatch()
    {
        int players = session.Players.Count;
        if (players < 2) { statusText.text = "Necesitas al menos 2 jugadores."; return; }
        if (NetworkBridge.Instance == null) { statusText.text = "La red aún no está lista, espera un momento."; return; }
        NetworkBridge.Instance.StartGameForAll(players);
    }

    // ---------- Sesión ----------
    void HookSession()
    {
        if (session == null) return;
        session.PlayerJoined += OnPlayerJoined;
        session.PlayerHasLeft += OnPlayersChanged;
        session.RemovedFromSession += OnSessionLost;
        session.Deleted += OnSessionLost;
    }

    void UnhookSession()
    {
        if (session == null) return;
        session.PlayerJoined -= OnPlayerJoined;
        session.PlayerHasLeft -= OnPlayersChanged;
        session.RemovedFromSession -= OnSessionLost;
        session.Deleted -= OnSessionLost;
    }

    // El anfitrión cerró la sala o nos expulsaron
    async void OnSessionLost()
    {
        if (leaving) return;
        await LeaveSessionAndResetAsync();
        statusText.text = "La sala se cerró. Crea otra o juega en modo local.";
    }

    void RefreshRoomInfo()
    {
        bool inRoom = session != null;
        if (roomCodeBadge != null) roomCodeBadge.SetActive(inRoom);
        if (roomCodeText != null) roomCodeText.text = inRoom ? "SALA: <b>" + session.Code + "</b>" : "";
        if (playersText != null) playersText.text = inRoom ? session.Players.Count + "/4" : "-/4";
    }

    // Sale de la sala (si hay), apaga la red y devuelve la UI al menú principal
    public async Task LeaveSessionAndResetAsync()
    {
        if (leaving) return;
        leaving = true;
        try
        {
            await LeaveSessionOnlyAsync();
        }
        finally
        {
            ResetToMainMenu();
            leaving = false;
        }
    }

    async Task LeaveSessionOnlyAsync()
    {
        var current = session;
        UnhookSession();
        session = null;

        if (current != null)
        {
            try { await current.LeaveAsync(); }
            catch (Exception e) { Debug.LogWarning("LeaveAsync: " + e.Message); }
        }

        var nm = NetworkManager.Singleton;
        if (nm != null && nm.IsListening && !nm.ShutdownInProgress) nm.Shutdown();

        // Espera (máx. 3 s) a que la red termine de apagarse y NetworkBridge suelte los ganchos
        float limit = Time.realtimeSinceStartup + 3f;
        while (nm != null && (nm.IsListening || nm.ShutdownInProgress) && Time.realtimeSinceStartup < limit)
            await Task.Yield();
    }

    void ResetToMainMenu()
    {
        if (TapMinigame.Instance != null) TapMinigame.Instance.ForceClose();
        if (MoleMinigame.Instance != null) MoleMinigame.Instance.ForceClose();
        if (DuckMinigame.Instance != null) DuckMinigame.Instance.ForceClose();
        if (GameManager.Instance != null) GameManager.Instance.ResetToMenu();

        startButton.gameObject.SetActive(false);
        createButton.interactable = servicesReady;
        joinButton.interactable = servicesReady;
        statusText.text = servicesReady
            ? "Listo. Crea una partida o únete con un código."
            : "Sin conexión: puedes jugar en modo local.";
        RefreshRoomInfo();
        menuPanel.SetActive(true);
        ReturnedToMenu?.Invoke();
    }

    // ---------- Modo local ----------
    void SetupLocalMode()
    {
        if (localCountButtons != null)
        {
            for (int i = 0; i < localCountButtons.Length; i++)
            {
                int players = i + 2;
                if (localCountButtons[i] != null)
                    localCountButtons[i].onClick.AddListener(() => SelectLocalPlayers(players));
            }
        }
        if (localPlayButton != null) localPlayButton.onClick.AddListener(StartLocalGame);
        SelectLocalPlayers(selectedLocalPlayers);
    }

    public void SelectLocalPlayers(int players)
    {
        selectedLocalPlayers = Mathf.Clamp(players, 2, GameManager.MaxPlayers);
        if (localCountSelected == null) return;
        for (int i = 0; i < localCountSelected.Length; i++)
            if (localCountSelected[i] != null) localCountSelected[i].SetActive(i + 2 == selectedLocalPlayers);
    }

    async void StartLocalGame()
    {
        // Si había una sala abierta, se sale de ella antes de jugar sin red
        if (session != null || (NetworkManager.Singleton != null && NetworkManager.Singleton.IsListening))
        {
            leaving = true;
            try { await LeaveSessionOnlyAsync(); }
            finally { leaving = false; }
            RefreshRoomInfo();
        }
        GameManager.Instance.StartGame(selectedLocalPlayers);
    }
}
