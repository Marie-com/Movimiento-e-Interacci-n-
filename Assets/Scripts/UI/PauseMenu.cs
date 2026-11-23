using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Overlay de pausa VISUAL: no toca Time.timeScale (la partida online sigue sincronizada).
public class PauseMenu : MonoBehaviour
{
    [SerializeField] GameObject panel;
    [SerializeField] RectTransform card;
    [SerializeField] Button continueButton;
    [SerializeField] Button closeButton;
    [SerializeField] Button exitButton;
    [SerializeField] GameObject onlineNote;
    [SerializeField] OnlineMenu onlineMenu;

    GameManager gm;
    bool exiting;

    public bool IsOpen { get { return panel != null && panel.activeSelf; } }

    void Start()
    {
        gm = GameManager.Instance;
        if (continueButton != null) continueButton.onClick.AddListener(Close);
        if (closeButton != null) closeButton.onClick.AddListener(Close);
        if (exitButton != null) exitButton.onClick.AddListener(ExitToMenu);
        if (onlineMenu != null) onlineMenu.ReturnedToMenu += Close;
        gm.GameFinished += OnGameFinished;
        panel.SetActive(false);
    }

    void OnDestroy()
    {
        if (onlineMenu != null) onlineMenu.ReturnedToMenu -= Close;
        if (gm != null) gm.GameFinished -= OnGameFinished;
    }

    void OnGameFinished(int[] scores) { Close(); }

    public void Open()
    {
        if (gm == null || !gm.started || gm.gameOver) return;
        if (onlineNote != null) onlineNote.SetActive(gm.IsOnline);
        panel.SetActive(true);
        if (card != null) StartCoroutine(UIAnim.Pop(card, 0.8f, 0.45f));
    }

    public void Close()
    {
        if (panel != null) panel.SetActive(false);
    }

    async void ExitToMenu()
    {
        if (exiting || onlineMenu == null) return;
        exiting = true;
        exitButton.interactable = false;
        try { await onlineMenu.LeaveSessionAndResetAsync(); }
        finally
        {
            exiting = false;
            exitButton.interactable = true;
            Close();
        }
    }

    void Update()
    {
        // Botón "atrás" de Android / Escape (Input System)
        var keyboard = Keyboard.current;
        if (keyboard == null || !keyboard.escapeKey.wasPressedThisFrame) return;
        if (IsOpen) Close();
        else Open();
    }
}
