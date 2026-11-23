using TMPro;
using UnityEngine;
using UnityEngine.UI;

// Fila del ranking final
public class ResultRowView : MonoBehaviour
{
    [SerializeField] TMP_Text rankText;
    [SerializeField] Image dot;
    [SerializeField] TMP_Text nameText;
    [SerializeField] TMP_Text scoreText;
    [SerializeField] GameObject winnerFrame;

    public void Set(int place, int player, int score, bool isWinner)
    {
        if (rankText != null) rankText.text = place + "º";
        if (dot != null) dot.color = UIThemeConfig.PlayerColor(player);
        if (nameText != null) nameText.text = UIThemeConfig.ColoredName(player);
        if (scoreText != null) scoreText.text = score + " pts";
        if (winnerFrame != null) winnerFrame.SetActive(isWinner);
    }
}
