using UnityEngine;

// Paleta y nombres centralizados de RA Party.
// Todos los scripts (tablero, minijuegos, HUD) leen los colores de jugador desde aquí.
public static class UIThemeConfig
{
    // ---------- Jugadores ----------
    public static readonly string[] PlayerNames = { "Rojo", "Azul", "Verde", "Amarillo" };

    public static readonly Color[] PlayerColors =
    {
        Hex("FF4757"), // Jugador 1 - Rojo Coral
        Hex("1E90FF"), // Jugador 2 - Azul Cielo
        Hex("2ED573"), // Jugador 3 - Verde Esmeralda
        Hex("FFA502"), // Jugador 4 - Amarillo Sol
    };

    static readonly string[] PlayerHex = { "FF4757", "1E90FF", "2ED573", "FFA502" };

    public static Color PlayerColor(int player)
    {
        return player >= 0 && player < PlayerColors.Length ? PlayerColors[player] : Color.white;
    }

    public static string PlayerName(int player)
    {
        return player >= 0 && player < PlayerNames.Length ? PlayerNames[player] : "?";
    }

    // Nombre con color para textos TextMeshPro (rich text)
    public static string ColoredName(int player)
    {
        if (player < 0 || player >= PlayerNames.Length) return "?";
        return "<color=#" + PlayerHex[player] + ">" + PlayerNames[player] + "</color>";
    }

    // Marcador de minijuego: una línea por participante con su color
    public static string FormatCounts(int participantsMask, int c0, int c1, int c2, int c3)
    {
        int[] c = { c0, c1, c2, c3 };
        var sb = new System.Text.StringBuilder();
        for (int i = 0; i < PlayerNames.Length; i++)
            if ((participantsMask & (1 << i)) != 0)
                sb.Append(ColoredName(i)).Append(": <b>").Append(c[i]).Append("</b>\n");
        return sb.ToString().TrimEnd('\n');
    }

    // ---------- Madera / Piedra ----------
    public static readonly Color WoodFill = Hex("7A4928");
    public static readonly Color WoodBevel = Hex("9E6338");
    public static readonly Color WoodOutline = Hex("2E180B");
    public static readonly Color WoodLip = Hex("3D200E");
    public static readonly Color Cream = Hex("FFF3D6");
    public static readonly Color Gold = Hex("FFD166");
    public static readonly Color GoldDark = Hex("C8963E");

    // ---------- Botones CTA ----------
    public static readonly Color GreenTop = Hex("4CD137");
    public static readonly Color GreenBottom = Hex("27AE60");
    public static readonly Color BlueTop = Hex("00A8FF");
    public static readonly Color BlueBottom = Hex("0984E3");
    public static readonly Color AmberTop = Hex("F39C12");
    public static readonly Color AmberBottom = Hex("D35400");

    // ---------- Overlays ----------
    public static readonly Color Backdrop = new Color(15f / 255f, 10f / 255f, 5f / 255f, 0.72f);
    public static readonly Color BackdropLight = new Color(15f / 255f, 10f / 255f, 5f / 255f, 0.5f);
    public static readonly Color TextDark = Hex("1A0D05");

    // Conversión hexadecimal sin depender de APIs nativas (seguro en inicializadores estáticos)
    public static Color Hex(string hex, float alpha = 1f)
    {
        if (hex.StartsWith("#")) hex = hex.Substring(1);
        int v = System.Convert.ToInt32(hex.Substring(0, 6), 16);
        return new Color(((v >> 16) & 0xFF) / 255f, ((v >> 8) & 0xFF) / 255f, (v & 0xFF) / 255f, alpha);
    }

    public static Color Mul(Color c, float k)
    {
        return new Color(c.r * k, c.g * k, c.b * k, c.a);
    }
}
