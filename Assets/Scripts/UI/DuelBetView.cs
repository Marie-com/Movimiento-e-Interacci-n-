using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Completa el modal de duelo (nombres y colores de los rivales). GameManager sigue
// controlando el panel, el slider y el botón de confirmar.
public class DuelBetView : MonoBehaviour
{
    [SerializeField] RectTransform card;
    [SerializeField] TMP_Text vsText;
    [SerializeField] Image leftDot;
    [SerializeField] Image rightDot;
    [SerializeField] TMP_Text rangeText;

    GameManager gm;

    void Start()
    {
        gm = GameManager.Instance;
        gm.DuelBetRequested += OnDuelBetRequested;
    }

    void OnDestroy()
    {
        if (gm != null) gm.DuelBetRequested -= OnDuelBetRequested;
    }

    void OnDuelBetRequested(int player, int rival, int maxBet)
    {
        if (vsText != null)
            vsText.text = UIThemeConfig.ColoredName(player) + "  <size=70%><color=#FFF3D6>VS</color></size>  " + UIThemeConfig.ColoredName(rival);
        if (leftDot != null) leftDot.color = UIThemeConfig.PlayerColor(player);
        if (rightDot != null) rightDot.color = UIThemeConfig.PlayerColor(rival);
        if (rangeText != null) rangeText.text = "Elige tu apuesta: de 1 a " + maxBet + " pts";
        if (card != null) StartCoroutine(UIAnim.Pop(card, 0.7f, 0.5f));
        AudioManager.PlaySfx(AudioManager.Sfx.TurnAlert);
    }
}
