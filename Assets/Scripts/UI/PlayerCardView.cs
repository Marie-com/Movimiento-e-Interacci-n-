using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Tarjeta compacta de jugador en la barra superior del HUD
public class PlayerCardView : MonoBehaviour
{
    [SerializeField] Image frame;
    [SerializeField] Image dot;
    [SerializeField] TMP_Text nameText;
    [SerializeField] TMP_Text scoreText;
    [SerializeField] GameObject turnHighlight;

    int lastScore = int.MinValue;
    bool isTurn;

    public void Setup(int player)
    {
        Color c = UIThemeConfig.PlayerColor(player);
        if (frame != null) frame.color = c;
        if (dot != null) dot.color = c;
        if (nameText != null) nameText.text = "J" + (player + 1) + " · " + UIThemeConfig.PlayerName(player);
        if (turnHighlight != null)
        {
            var img = turnHighlight.GetComponent<Image>();
            if (img != null) img.color = new Color(c.r, c.g, c.b, 0.55f);
        }
        lastScore = int.MinValue;
        SetScore(0, false);
        SetTurn(false);
    }

    public void SetScore(int score, bool animate = true)
    {
        if (score == lastScore) return;
        lastScore = score;
        if (scoreText == null) return;
        scoreText.text = score.ToString();
        if (animate && isActiveAndEnabled) StartCoroutine(UIAnim.Pop(scoreText.transform, 1.45f, 0.5f));
    }

    public void SetTurn(bool turn)
    {
        isTurn = turn;
        if (turnHighlight != null) turnHighlight.SetActive(turn);
        transform.localScale = turn ? new Vector3(1.06f, 1.06f, 1f) : Vector3.one;
    }

    void Update()
    {
        if (!isTurn || turnHighlight == null) return;
        float s = 1f + 0.04f * Mathf.Sin(Time.unscaledTime * 5f);
        turnHighlight.transform.localScale = new Vector3(s, s, 1f);
    }
}
