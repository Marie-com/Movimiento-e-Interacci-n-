using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;
using Sd = UISpriteGenerator.Sd;
using Raster = UISpriteGenerator.Raster;
using Sdf = UISpriteGenerator.Sdf;

// Genera el icono de la app (ficha hexagonal de madera con un dado sobre cielo azul)
// y lo asigna en Player Settings: icono por defecto + iconos de Android (adaptive, round y legacy).
[InitializeOnLoad]
public static class AppIconGenerator
{
    public const string Folder = "Assets/UI/AppIcon";
    const int S = 1024;
    const float C = S * 0.5f;

    static AppIconGenerator()
    {
        // Cuando se instale el módulo de Android, asigna los iconos de Android si aún están vacíos
        EditorApplication.delayCall += () =>
        {
            if (File.Exists(Folder + "/app_icon.png")) AssignIcons(onlyIfEmpty: true);
        };
    }

    [MenuItem("RA Party/3. Generar y Asignar Icono de la App")]
    public static void GenerateFromMenu()
    {
        GenerateAndAssign();
        EditorUtility.DisplayDialog("RA Party", "Icono generado en " + Folder + " y asignado en Player Settings.", "OK");
    }

    public static void GenerateFromCommandLine()
    {
        try
        {
            GenerateAndAssign();
            EditorApplication.Exit(0);
        }
        catch (System.Exception e)
        {
            Debug.LogException(e);
            EditorApplication.Exit(1);
        }
    }

    public static void GenerateAndAssign()
    {
        Directory.CreateDirectory(Folder);
        var background = Background();
        var foreground = Foreground();

        var legacy = Background();
        legacy.Draw(foreground);
        legacy.Mask((x, y) => Sd.Box(x, y, 0, 0, S, S, 180f));     // cuadrado redondeado

        var round = Background();
        round.Draw(foreground);
        round.Mask((x, y) => Sd.Circle(x, y, C, C, C));             // círculo

        Save("app_icon", legacy);
        Save("app_icon_round", round);
        Save("app_icon_background", background);
        Save("app_icon_foreground", foreground);
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        foreach (var n in new[] { "app_icon", "app_icon_round", "app_icon_background", "app_icon_foreground" })
            ConfigureImporter(Folder + "/" + n + ".png");

        AssignIcons(onlyIfEmpty: false);
        AssetDatabase.SaveAssets();
    }

    static Texture2D Load(string name)
    {
        return AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/" + name + ".png");
    }

    static void AssignIcons(bool onlyIfEmpty)
    {
        var main = Load("app_icon");
        var round = Load("app_icon_round");
        var bg = Load("app_icon_background");
        var fg = Load("app_icon_foreground");
        if (main == null) return;

        if (!onlyIfEmpty) PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { main }, IconKind.Any);

        // Iconos específicos de Android (solo existen si está instalado Android Build Support)
        var kinds = PlayerSettings.GetSupportedIconKinds(NamedBuildTarget.Android);
        if (kinds == null || kinds.Length == 0)
        {
            if (!onlyIfEmpty)
                Debug.Log("[AppIcon] Módulo de Android no instalado: se asignó el icono por defecto. " +
                          "Los iconos adaptive/round se asignarán solos al instalar el módulo.");
            return;
        }

        bool changed = false;
        foreach (var kind in kinds)
        {
            var icons = PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android, kind);
            if (icons == null || icons.Length == 0) continue;
            if (onlyIfEmpty && icons[0].GetTexture() != null) continue;

            bool isRound = kind.ToString().ToLowerInvariant().Contains("round");
            foreach (var icon in icons)
            {
                if (icon.maxLayerCount >= 2) icon.SetTextures(bg, fg);   // adaptive: fondo + primer plano
                else icon.SetTextures(isRound ? round : main);
            }
            PlayerSettings.SetPlatformIcons(NamedBuildTarget.Android, kind, icons);
            changed = true;
        }
        if (changed)
        {
            AssetDatabase.SaveAssets();
            Debug.Log("[AppIcon] Iconos de Android asignados (adaptive, round y legacy).");
        }
    }

    static void Save(string name, Raster r)
    {
        var tex = r.ToTexture();
        File.WriteAllBytes(Path.Combine(Folder, name + ".png"), tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
    }

    static void ConfigureImporter(string path)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) return;
        importer.textureType = TextureImporterType.Default;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.npotScale = TextureImporterNPOTScale.None;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.maxTextureSize = 1024;
        importer.SaveAndReimport();
    }

    static Color Hex(string h, float a = 1f) { return UIThemeConfig.Hex(h, a); }

    // ---------- Capa de fondo: cielo azul con rayos y marcas AR ----------
    static Raster Background()
    {
        var r = new Raster(S, S);
        Color inner = Hex("7AD8FF"), outer = Hex("0A6FD1");
        r.Fill((x, y) => -1000f, (x, y) =>
        {
            float dx = x - C, dy = y - C * 0.9f;
            float t = Mathf.Clamp01(Mathf.Sqrt(dx * dx + dy * dy) / (S * 0.72f));
            Color c = Color.Lerp(inner, outer, t * t);
            float ang = Mathf.Atan2(dy, dx) * Mathf.Rad2Deg + 360f;
            if (Mathf.FloorToInt(ang / 22.5f) % 2 == 0) c = UIThemeConfig.Mul(c, 1.06f);
            c.a = 1f;
            return c;
        });

        // Esquinas de escaneo AR (pueden recortarse en máscaras circulares)
        const float m = 150f, len = 150f, w = 18f;
        Sdf corners = (x, y) =>
        {
            float d = float.MaxValue;
            float[] xs = { m, S - m }, ys = { m, S - m };
            foreach (float cx in xs)
                foreach (float cy in ys)
                {
                    float sx = cx < C ? 1f : -1f, sy = cy < C ? 1f : -1f;
                    d = Mathf.Min(d, Sd.Segment(x, y, cx, cy, cx + sx * len, cy, w));
                    d = Mathf.Min(d, Sd.Segment(x, y, cx, cy, cx, cy + sy * len, w));
                }
            return d;
        };
        r.Fill(corners, new Color(1f, 0.95f, 0.84f, 0.55f));
        return r;
    }

    // ---------- Capa frontal: ficha hexagonal de madera + dado (dentro de la zona segura) ----------
    static Raster Foreground()
    {
        var r = new Raster(S, S);
        const float cy = 486f, apothem = 236f, corner = 34f, lip = 34f;
        var facePts = Sd.HexPoints(C, cy, apothem, corner);
        var lipPts = Sd.HexPoints(C, cy + lip, apothem, corner);
        Sdf face = (x, y) => Sd.Polygon(x, y, facePts) - corner;
        Sdf lipShape = (x, y) => Sd.Polygon(x, y, lipPts) - corner;

        // Sombra suave
        r.Soft((x, y) => Sd.Ellipse(x, y, C, cy + lip + 210f, 230f, 40f), new Color(0f, 0.1f, 0.25f, 0.45f), 60f);

        // Ficha de madera con canto 3D
        r.Fill((x, y) => Mathf.Min(face(x, y), lipShape(x, y)) - 12f, UIThemeConfig.WoodOutline);
        r.Fill(lipShape, UIThemeConfig.WoodLip);
        r.Fill(face, (x, y) => UISpriteGenerator.Wood(
            Color.Lerp(Hex("B5763F"), Hex("6E3F1C"), (y - (cy - apothem)) / (2f * apothem)), x * 0.25f, y * 0.25f, 0.09f, 9f));
        r.Fill((x, y) => Mathf.Abs(face(x, y) + 16f) - 4.5f, (x, y) => new Color(1f, 0.88f, 0.68f, Mathf.Clamp01(1f - (y - (cy - apothem)) / 230f) * 0.6f));
        r.Fill((x, y) => Mathf.Abs(face(x, y) + 44f) - 5f, UIThemeConfig.WoodOutline, 0.45f);

        // Dado crema ligeramente girado
        const float dcx = C, dcy = cy - 6f, half = 128f, rad = 40f;
        float a = -12f * Mathf.Deg2Rad, ca = Mathf.Cos(a), sa = Mathf.Sin(a);
        Sdf dieAt(float ox, float oy, float grow) => (x, y) =>
        {
            float lx = x - dcx - ox, ly = y - dcy - oy;
            float rx = lx * ca - ly * sa, ry = lx * sa + ly * ca;
            return Sd.RoundRect(rx, ry, 0f, 0f, half, half, rad) - grow;
        };
        var die = dieAt(0f, 0f, 0f);
        r.Fill(dieAt(0f, 22f, 10f), UIThemeConfig.WoodOutline);                  // contorno + canto
        r.Fill(dieAt(0f, 22f, 0f), Hex("C9A86B"));
        r.Fill(dieAt(0f, 0f, 10f), UIThemeConfig.WoodOutline);
        r.Fill(die, (x, y) => Color.Lerp(Hex("FFFAEC"), Hex("F2DDAE"), Mathf.Clamp01((y - (dcy - half)) / (2f * half))));
        r.Fill((x, y) => Sd.Box(x, y, dcx - 92f, dcy - 112f, dcx + 70f, dcy - 78f, 17f), new Color(1f, 1f, 1f, 0.55f));

        Vector2[] pips = { new Vector2(-64, -64), new Vector2(64, -64), Vector2.zero, new Vector2(-64, 64), new Vector2(64, 64) };
        foreach (var p in pips)
        {
            float px = dcx + p.x * ca + p.y * sa, py = dcy - p.x * sa + p.y * ca;
            r.Fill((x, y) => Sd.Circle(x, y, px, py, 27f), Hex("3D200E"));
            r.Fill((x, y) => Sd.Circle(x, y, px - 6f, py - 7f, 8f), new Color(1f, 1f, 1f, 0.25f));
        }

        // Destellos dorados
        Sparkle(r, 680f, 290f, 44f);
        Sparkle(r, 318f, 690f, 30f);
        return r;
    }

    static void Sparkle(Raster r, float cx, float cy, float size)
    {
        var pts = new Vector2[8];
        for (int i = 0; i < 8; i++)
        {
            float ang = i * 45f * Mathf.Deg2Rad;
            float rad = i % 2 == 0 ? size : size * 0.32f;
            pts[i] = new Vector2(cx + Mathf.Cos(ang) * rad, cy + Mathf.Sin(ang) * rad);
        }
        r.Soft((x, y) => Sd.Circle(x, y, cx, cy, size * 0.2f), new Color(1f, 0.95f, 0.7f, 0.6f), size);
        r.Fill((x, y) => Sd.Polygon(x, y, pts) - 3f, UIThemeConfig.WoodOutline);
        r.Fill((x, y) => Sd.Polygon(x, y, pts), UIThemeConfig.Gold);
    }
}
