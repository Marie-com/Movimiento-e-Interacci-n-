using TMPro;
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

    ISession session;

    async void Start()
    {
        startButton.gameObject.SetActive(false);
        createButton.interactable = false;
        joinButton.interactable = false;
        createButton.onClick.AddListener(CreateSession);
        joinButton.onClick.AddListener(JoinSession);
        startButton.onClick.AddListener(StartMatch);
        GameManager.Instance.GameStarted += () => menuPanel.SetActive(false);

        try
        {
            statusText.text = "Conectando a los servicios...";
            await UnityServices.InitializeAsync();
            if (!AuthenticationService.Instance.IsSignedIn)
                await AuthenticationService.Instance.SignInAnonymouslyAsync();
            statusText.text = "Listo. Crea una partida o únete con un código.";
            createButton.interactable = true;
            joinButton.interactable = true;
        }
        catch (System.Exception e)
        {
            statusText.text = "Error: " + e.Message;
            Debug.LogException(e);
        }
    }

    async void CreateSession()
    {
        try
        {
            var options = new SessionOptions { MaxPlayers = 4 }.WithRelayNetwork();
            session = await MultiplayerService.Instance.CreateSessionAsync(options);
            session.PlayerJoined += OnPlayerJoined;
            createButton.interactable = false;
            joinButton.interactable = false;
            startButton.gameObject.SetActive(true);
            ShowHostStatus();
        }
        catch (System.Exception e)
        {
            statusText.text = "Error al crear: " + e.Message;
            Debug.LogException(e);
        }
    }

    void OnPlayerJoined(string playerId)
    {
        ShowHostStatus();
    }

    void ShowHostStatus()
    {
        statusText.text = "Código de la sala: " + session.Code + "\nJugadores: " + session.Players.Count + "/4";
    }

    async void JoinSession()
    {
        try
        {
            string code = codeInput.text.Trim().ToUpper();
            session = await MultiplayerService.Instance.JoinSessionByCodeAsync(code);
            createButton.interactable = false;
            joinButton.interactable = false;
            statusText.text = "Conectado. Esperando a que el anfitrión empiece...";
        }
        catch (System.Exception e)
        {
            statusText.text = "No se pudo entrar: " + e.Message;
            Debug.LogException(e);
        }
    }

    void StartMatch()
    {
        int players = session.Players.Count;
        if (players < 2) { statusText.text = "Necesitas al menos 2 jugadores."; return; }
        if (NetworkBridge.Instance == null) { statusText.text = "La red aún no está lista, espera un momento."; return; }
        NetworkBridge.Instance.StartGameForAll(players);
    }
}