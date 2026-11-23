using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

// Genera por código todos los sprites de la UI de RA Party (madera + glossy + íconos).
// Dibuja con funciones de distancia (SDF) y supersampling 4x4 en los bordes para un antialiasing suave.
public static class UISpriteGenerator
{
    public const string Folder = "Assets/UI/GeneratedSprites";

    [MenuItem("RA Party/1. Generar Sprites UI")]
    public static void GenerateFromMenu()
    {
        int count = GenerateAll();
        EditorUtility.DisplayDialog("RA Party", "Se generaron " + count + " sprites en " + Folder, "OK");
    }

    public static Sprite Load(string name)
    {
        return AssetDatabase.LoadAssetAtPath<Sprite>(Folder + "/" + name + ".png");
    }

    public static bool AllSpritesExist()
    {
        foreach (var name in new[] { "panel_wood", "btn_green_normal", "btn_hex_wood", "icon_pause", "dot", "duck" })
            if (Load(name) == null) return false;
        return true;
    }

    struct Job { public string name; public Raster raster; public Vector4 border; }

    public static int GenerateAll()
    {
        Directory.CreateDirectory(Folder);
        var jobs = new List<Job>();
        void Add(string name, Raster r, float border = 0f) => jobs.Add(new Job { name = name, raster = r, border = new Vector4(border, border, border, border) });

        // Contenedores y botones de madera
        Add("panel_wood", PanelWood(false), 48);
        Add("panel_wood_dark", PanelWood(true), 48);
        Add("btn_wood_normal", ButtonWood(false), 36);
        Add("btn_wood_pressed", ButtonWood(true), 36);
        Add("btn_hex_wood", HexWood(false));
        Add("btn_hex_wood_pressed", HexWood(true));

        // CTA glossy
        Add("btn_green_normal", ButtonGlossy(UIThemeConfig.GreenTop, UIThemeConfig.GreenBottom, false), 48);
        Add("btn_green_pressed", ButtonGlossy(UIThemeConfig.GreenTop, UIThemeConfig.GreenBottom, true), 48);
        Add("btn_blue_normal", ButtonGlossy(UIThemeConfig.BlueTop, UIThemeConfig.BlueBottom, false), 48);
        Add("btn_blue_pressed", ButtonGlossy(UIThemeConfig.BlueTop, UIThemeConfig.BlueBottom, true), 48);
        Add("btn_amber_normal", ButtonGlossy(UIThemeConfig.AmberTop, UIThemeConfig.AmberBottom, false), 48);
        Add("btn_amber_pressed", ButtonGlossy(UIThemeConfig.AmberTop, UIThemeConfig.AmberBottom, true), 48);

        // HUD y AR
        Add("pill_hud", PillHud(), 32);
        Add("ar_reticle_corner", ReticleCorner());

        // Íconos blancos para tintar
        Add("icon_pause", IconPause());
        Add("icon_settings", IconSettings());
        Add("icon_close", IconClose());
        Add("icon_check", IconCheck());
        Add("icon_restart", IconRestart());
        Add("icon_dice", IconDice());
        Add("icon_sound_on", IconSound(true));
        Add("icon_sound_off", IconSound(false));
        Add("icon_crown", IconCrown());
        Add("icon_users", IconUsers());

        // Auxiliares (indicadores, marcos tintables, slider, minijuegos)
        Add("dot", Dot());
        Add("glow", Glow());
        Add("card_frame", CardFrame(), 32);
        Add("bar_fill", BarFill(), 18);
        Add("duck", Duck());

        foreach (var job in jobs)
        {
            var tex = job.raster.ToTexture();
            File.WriteAllBytes(Path.Combine(Folder, job.name + ".png"), tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
        }
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);

        foreach (var job in jobs) ConfigureImporter(Folder + "/" + job.name + ".png", job.border);
        AssetDatabase.SaveAssets();
        return jobs.Count;
    }

    static void ConfigureImporter(string path, Vector4 border)
    {
        var importer = AssetImporter.GetAtPath(path) as TextureImporter;
        if (importer == null) { Debug.LogWarning("No se encontró el importador de " + path); return; }
        importer.textureType = TextureImporterType.Sprite;
        importer.spriteImportMode = SpriteImportMode.Single;
        importer.mipmapEnabled = false;
        importer.alphaIsTransparency = true;
        importer.sRGBTexture = true;
        importer.wrapMode = TextureWrapMode.Clamp;
        importer.filterMode = FilterMode.Bilinear;
        importer.textureCompression = TextureImporterCompression.Uncompressed;
        importer.spritePixelsPerUnit = 100f;
        importer.isReadable = false;

        var settings = new TextureImporterSettings();
        importer.ReadTextureSettings(settings);
        settings.spriteMeshType = SpriteMeshType.FullRect;
        settings.spriteAlignment = (int)SpriteAlignment.Center;
        importer.SetTextureSettings(settings);

        importer.spriteBorder = border; // (izq, abajo, der, arriba)
        importer.SaveAndReimport();
    }

    // =====================================================================
    // Rasterizador
    // =====================================================================
    public delegate float Sdf(float x, float y);
    public delegate Color Paint(float x, float y);

    public class Raster
    {
        public readonly int W, H;
        readonly Color[] px; // alpha directo, y hacia abajo

        public Raster(int w, int h) { W = w; H = h; px = new Color[w * h]; }

        // Rellena una forma con antialiasing (supersampling 4x4 cerca del borde)
        public void Fill(Sdf sdf, Paint paint, float alpha = 1f)
        {
            for (int y = 0; y < H; y++)
            {
                for (int x = 0; x < W; x++)
                {
                    float cx = x + 0.5f, cy = y + 0.5f;
                    float d = sdf(cx, cy);
                    if (d > 1.5f) continue;
                    float cov;
                    if (d < -1.5f) cov = 1f;
                    else
                    {
                        cov = 0f;
                        for (int sy = 0; sy < 4; sy++)
                            for (int sx = 0; sx < 4; sx++)
                                cov += Mathf.Clamp01(0.5f - sdf(x + (sx + 0.5f) * 0.25f, y + (sy + 0.5f) * 0.25f) * 4f);
                        cov /= 16f;
                    }
                    if (cov <= 0f) continue;
                    Color c = paint(cx, cy);
                    c.a *= cov * alpha;
                    Blend(x, y, c);
                }
            }
        }

        public void Fill(Sdf sdf, Color color, float alpha = 1f) { Fill(sdf, (x, y) => color, alpha); }

        // Forma difusa para sombras y brillos
        public void Soft(Sdf sdf, Color color, float softness)
        {
            for (int y = 0; y < H; y++)
            {
                for (int x = 0; x < W; x++)
                {
                    float d = sdf(x + 0.5f, y + 0.5f);
                    if (d >= softness) continue;
                    float t = Mathf.Clamp01(d / softness);
                    float a = 1f - t * t * (3f - 2f * t);
                    Color c = color;
                    c.a *= a;
                    Blend(x, y, c);
                }
            }
        }

        void Blend(int x, int y, Color s)
        {
            int i = y * W + x;
            Color d = px[i];
            float oa = s.a + d.a * (1f - s.a);
            if (oa <= 0.0001f) { px[i] = new Color(0, 0, 0, 0); return; }
            float k = d.a * (1f - s.a);
            px[i] = new Color(
                (s.r * s.a + d.r * k) / oa,
                (s.g * s.a + d.g * k) / oa,
                (s.b * s.a + d.b * k) / oa,
                oa);
        }

        public void MultiplyAlpha(float k)
        {
            for (int i = 0; i < px.Length; i++) px[i].a *= k;
        }

        public Texture2D ToTexture()
        {
            var tex = new Texture2D(W, H, TextureFormat.RGBA32, false);
            var flipped = new Color[W * H];
            for (int y = 0; y < H; y++)
                for (int x = 0; x < W; x++)
                    flipped[(H - 1 - y) * W + x] = px[y * W + x];
            tex.SetPixels(flipped);
            tex.Apply();
            return tex;
        }
    }

    // =====================================================================
    // Funciones de distancia (coordenadas en píxeles, y hacia abajo)
    // =====================================================================
    static class Sd
    {
        public static float Circle(float x, float y, float cx, float cy, float r)
        {
            float dx = x - cx, dy = y - cy;
            return Mathf.Sqrt(dx * dx + dy * dy) - r;
        }

        public static float RoundRect(float x, float y, float cx, float cy, float hw, float hh, float r)
        {
            float qx = Mathf.Abs(x - cx) - hw + r;
            float qy = Mathf.Abs(y - cy) - hh + r;
            float ox = Mathf.Max(qx, 0f), oy = Mathf.Max(qy, 0f);
            return Mathf.Sqrt(ox * ox + oy * oy) + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
        }

        // Caja por bordes (izq, arriba, der, abajo)
        public static float Box(float x, float y, float l, float t, float r, float b, float radius)
        {
            return RoundRect(x, y, (l + r) * 0.5f, (t + b) * 0.5f, (r - l) * 0.5f, (b - t) * 0.5f, radius);
        }

        public static float Segment(float x, float y, float ax, float ay, float bx, float by, float r)
        {
            float pax = x - ax, pay = y - ay, bax = bx - ax, bay = by - ay;
            float h = Mathf.Clamp01((pax * bax + pay * bay) / (bax * bax + bay * bay));
            float dx = pax - bax * h, dy = pay - bay * h;
            return Mathf.Sqrt(dx * dx + dy * dy) - r;
        }

        public static float Polygon(float x, float y, Vector2[] v)
        {
            float dx0 = x - v[0].x, dy0 = y - v[0].y;
            float d = dx0 * dx0 + dy0 * dy0;
            float s = 1f;
            for (int i = 0, j = v.Length - 1; i < v.Length; j = i, i++)
            {
                float ex = v[j].x - v[i].x, ey = v[j].y - v[i].y;
                float wx = x - v[i].x, wy = y - v[i].y;
                float h = Mathf.Clamp01((wx * ex + wy * ey) / (ex * ex + ey * ey));
                float bx = wx - ex * h, by = wy - ey * h;
                d = Mathf.Min(d, bx * bx + by * by);
                bool c1 = y >= v[i].y, c2 = y < v[j].y, c3 = ex * wy > ey * wx;
                if ((c1 && c2 && c3) || (!c1 && !c2 && !c3)) s = -s;
            }
            return s * Mathf.Sqrt(d);
        }

        // Hexágono de lados planos arriba/abajo, con esquinas redondeadas
        public static Vector2[] HexPoints(float cx, float cy, float apothem, float corner)
        {
            float R = (apothem - corner) / Mathf.Cos(30f * Mathf.Deg2Rad);
            var pts = new Vector2[6];
            for (int i = 0; i < 6; i++)
            {
                float a = i * 60f * Mathf.Deg2Rad;
                pts[i] = new Vector2(cx + R * Mathf.Cos(a), cy + R * Mathf.Sin(a));
            }
            return pts;
        }

        // Arco (ángulos en grados, sentido matemático con y hacia arriba)
        public static float Arc(float x, float y, float cx, float cy, float radius, float halfWidth, float startDeg, float spanDeg)
        {
            float vx = x - cx, vy = -(y - cy);
            float len = Mathf.Sqrt(vx * vx + vy * vy);
            float ang = Mathf.Atan2(vy, vx) * Mathf.Rad2Deg;
            float rel = Mathf.Repeat(ang - startDeg, 360f);
            if (rel <= spanDeg) return Mathf.Abs(len - radius) - halfWidth;
            float a0 = startDeg * Mathf.Deg2Rad, a1 = (startDeg + spanDeg) * Mathf.Deg2Rad;
            float d0 = Circle(x, y, cx + radius * Mathf.Cos(a0), cy - radius * Mathf.Sin(a0), 0f);
            float d1 = Circle(x, y, cx + radius * Mathf.Cos(a1), cy - radius * Mathf.Sin(a1), 0f);
            return Mathf.Min(d0, d1) - halfWidth;
        }

        public static float Ellipse(float x, float y, float cx, float cy, float rx, float ry)
        {
            float nx = (x - cx) / rx, ny = (y - cy) / ry;
            return (Mathf.Sqrt(nx * nx + ny * ny) - 1f) * Mathf.Min(rx, ry);
        }

        public static float Ring(float d, float width) { return Mathf.Max(d, -(d + width)); }
    }

    // =====================================================================
    // Utilidades de color
    // =====================================================================
    static Color C(string hex, float a = 1f) { return UIThemeConfig.Hex(hex, a); }
    static Color Mul(Color c, float k) { return new Color(c.r * k, c.g * k, c.b * k, c.a); }
    static Color WithA(Color c, float a) { return new Color(c.r, c.g, c.b, a); }

    // Vetas de madera horizontales (sutiles para que el 9-slice estire bien)
    static Color Wood(Color baseColor, float x, float y, float strength = 0.07f, float seed = 0f)
    {
        float n = Mathf.PerlinNoise(x * 0.011f + seed, y * 0.085f + seed * 1.7f);
        float lines = Mathf.Sin((y + n * 22f) * 0.5f);
        float k = 1f + strength * lines + (n - 0.5f) * strength;
        return Mul(baseColor, k);
    }

    // =====================================================================
    // Paneles y botones de madera
    // =====================================================================
    static Raster PanelWood(bool dark)
    {
        const int S = 256;
        var r = new Raster(S, S);
        const float m = 2f;
        Sdf outer = (x, y) => Sd.Box(x, y, m, m, S - m, S - m, 30f);

        Color outline = dark ? C("1A0D05") : UIThemeConfig.WoodOutline;
        Color frameTop = dark ? C("4A2A15") : UIThemeConfig.WoodBevel;
        Color frameBottom = dark ? C("2A170A") : C("6B3E1F");
        Color inner = dark ? C("2B190C") : UIThemeConfig.WoodFill;
        Color innerShadow = dark ? C("1C1008") : C("55301A");

        // Contorno oscuro de 4px
        r.Fill(outer, outline);
        // Marco con bisel superior iluminado
        r.Fill((x, y) => outer(x, y) + 4f, (x, y) => Wood(Color.Lerp(frameTop, frameBottom, (y - m) / (S - 2 * m)), x, y, 0.08f, 3f));
        r.Fill((x, y) => Mathf.Abs(outer(x, y) + 6.5f) - 1.1f, (x, y) => new Color(1f, 0.86f, 0.66f, Mathf.Clamp01(1f - (y - m) / 110f) * (dark ? 0.25f : 0.5f)));
        // Ranura entre marco y centro
        r.Fill((x, y) => Mathf.Abs(outer(x, y) + 21.5f) - 1.8f, outline, 0.9f);
        // Centro hundido con sombra interior arriba
        r.Fill((x, y) => outer(x, y) + 23f, (x, y) =>
        {
            float t = Mathf.Clamp01((y - (m + 23f)) / 12f);
            return Wood(Color.Lerp(innerShadow, inner, t), x, y, 0.05f, 11f);
        });
        // Brillo inferior del hueco
        r.Fill((x, y) => Mathf.Abs(outer(x, y) + 24.5f) - 0.8f, (x, y) => new Color(1f, 0.9f, 0.75f, Mathf.Clamp01((y - 170f) / 80f) * 0.25f));

        // Muescas en las zonas que no se estiran
        float[] spots = { 40f, S - 40f };
        foreach (float s in spots)
        {
            var notches = new[]
            {
                new[] { new Vector2(s - 4, 5), new Vector2(s + 4, 5), new Vector2(s, 11) },
                new[] { new Vector2(s - 4, S - 5), new Vector2(s, S - 11), new Vector2(s + 4, S - 5) },
                new[] { new Vector2(5, s - 4), new Vector2(11, s), new Vector2(5, s + 4) },
                new[] { new Vector2(S - 5, s - 4), new Vector2(S - 5, s + 4), new Vector2(S - 11, s) },
            };
            foreach (var tri in notches) r.Fill((x, y) => Sd.Polygon(x, y, tri), outline, 0.8f);
        }

        // Remaches de bronce en las esquinas (solo panel claro)
        if (!dark)
        {
            Vector2[] rivets = { new Vector2(15, 15), new Vector2(S - 15, 15), new Vector2(15, S - 15), new Vector2(S - 15, S - 15) };
            foreach (var p in rivets)
            {
                r.Fill((x, y) => Sd.Circle(x, y, p.x, p.y, 5f), outline);
                r.Fill((x, y) => Sd.Circle(x, y, p.x, p.y, 3.5f), (x, y) => Color.Lerp(C("F0CE84"), C("8C6A30"), (y - p.y + 3.5f) / 7f));
                r.Fill((x, y) => Sd.Circle(x, y, p.x - 1.2f, p.y - 1.2f, 1.2f), new Color(1, 1, 1, 0.8f));
            }
        }

        if (dark) r.MultiplyAlpha(0.92f);
        return r;
    }

    static Raster ButtonWood(bool pressed)
    {
        const int W = 256, H = 128;
        var r = new Raster(W, H);
        float lip = pressed ? 4f : 8f;
        float top = pressed ? 7f : 3f;
        float bottom = H - 3f - lip;
        Sdf face = (x, y) => Sd.Box(x, y, 3f, top, W - 3f, bottom, 22f);
        Sdf lipShape = (x, y) => Sd.Box(x, y, 3f, top + lip, W - 3f, bottom + lip, 22f);
        float k = pressed ? 0.9f : 1f;

        r.Fill((x, y) => Mathf.Min(face(x, y), lipShape(x, y)) - 2.5f, UIThemeConfig.WoodOutline);
        r.Fill(lipShape, UIThemeConfig.WoodLip);
        r.Fill(face, (x, y) => Wood(Mul(Color.Lerp(C("A86A3C"), UIThemeConfig.WoodFill, (y - top) / (bottom - top)), k), x, y, 0.07f, 5f));
        r.Fill((x, y) => Mathf.Abs(face(x, y) + 3.5f) - 1.1f, (x, y) => new Color(1f, 0.88f, 0.68f, Mathf.Clamp01(1f - (y - top) / 50f) * 0.55f * k));
        r.Fill((x, y) => Mathf.Abs(face(x, y) + 9f) - 1.2f, UIThemeConfig.WoodOutline, 0.35f);
        return r;
    }

    static Raster HexWood(bool pressed)
    {
        const int S = 128;
        var r = new Raster(S, S);
        float lip = pressed ? 4f : 8f;
        float cy = pressed ? 65f : 61f;
        const float apothem = 50f, corner = 8f;
        var facePts = Sd.HexPoints(64f, cy, apothem, corner);
        var lipPts = Sd.HexPoints(64f, cy + lip, apothem, corner);
        Sdf face = (x, y) => Sd.Polygon(x, y, facePts) - corner;
        Sdf lipShape = (x, y) => Sd.Polygon(x, y, lipPts) - corner;
        float k = pressed ? 0.9f : 1f;

        r.Fill((x, y) => Mathf.Min(face(x, y), lipShape(x, y)) - 2.5f, UIThemeConfig.WoodOutline);
        r.Fill(lipShape, UIThemeConfig.WoodLip);
        r.Fill(face, (x, y) => Wood(Mul(Color.Lerp(C("B07240"), C("74441F"), (y - (cy - apothem)) / (2f * apothem)), k), x, y, 0.08f, 9f));
        r.Fill((x, y) => Mathf.Abs(face(x, y) + 4f) - 1.2f, (x, y) => new Color(1f, 0.88f, 0.68f, Mathf.Clamp01(1f - (y - (cy - apothem)) / 55f) * 0.6f * k));
        r.Fill((x, y) => Mathf.Abs(face(x, y) + 11f) - 1.4f, UIThemeConfig.WoodOutline, 0.45f);
        return r;
    }

    // Cápsula glossy de alto contraste con borde crema, labio 3D y franja de brillo
    static Raster ButtonGlossy(Color topColor, Color bottomColor, bool pressed)
    {
        const int W = 320, H = 128;
        var r = new Raster(W, H);
        const float radius = 38f, l = 4f, rr = W - 4f;
        float lip = pressed ? 3f : 7f;
        float t = pressed ? 7f : 3f;
        float b = H - 3f - lip;
        Sdf face = (x, y) => Sd.Box(x, y, l, t, rr, b, radius);
        Sdf lipShape = (x, y) => Sd.Box(x, y, l, t + lip, rr, b + lip, radius);
        float k = pressed ? 0.88f : 1f;
        Color outline = Mul(bottomColor, 0.32f);
        outline.a = 1f;

        r.Fill((x, y) => Mathf.Min(face(x, y), lipShape(x, y)) - 2.5f, outline);
        r.Fill(lipShape, Mul(bottomColor, 0.62f));
        r.Fill(face, Mul(C("FFF8EC"), pressed ? 0.94f : 1f));                       // borde blanco/crema
        r.Fill((x, y) => face(x, y) + 5f, (x, y) =>
        {
            float v = Mathf.Clamp01((y - (t + 5f)) / (b - t - 10f));
            return Mul(Color.Lerp(topColor, bottomColor, v), k);
        });
        // Reflejo inferior suave
        r.Fill((x, y) => Sd.Box(x, y, l + 26f, b - 20f, rr - 26f, b - 8f, 6f), new Color(1f, 1f, 1f, 0.14f * k));
        // Franja de brillo especular (dentro del borde superior del 9-slice)
        float gTop = t + 8f, gBottom = t + 41f;
        r.Fill((x, y) => Sd.Box(x, y, l + 15f, gTop, rr - 15f, gBottom, 16f),
            (x, y) => new Color(1f, 1f, 1f, Mathf.Lerp(0.62f, 0.1f, (y - gTop) / (gBottom - gTop)) * (pressed ? 0.7f : 1f)));
        // Destello
        r.Soft((x, y) => Sd.Circle(x, y, l + 32f, t + 17f, 2f), new Color(1f, 1f, 1f, 0.45f), 8f);
        r.Fill((x, y) => Sd.Circle(x, y, l + 32f, t + 17f, 4f), new Color(1f, 1f, 1f, 0.95f));
        return r;
    }

    static Raster PillHud()
    {
        const int W = 256, H = 96;
        var r = new Raster(W, H);
        Sdf outer = (x, y) => Sd.Box(x, y, 2f, 2f, W - 2f, H - 2f, 30f);
        r.Fill((x, y) => Sd.Ring(outer(x, y), 1.8f), C("1A0D05"));
        r.Fill((x, y) => Sd.Ring(outer(x, y) + 1.8f, 3.4f), (x, y) => Color.Lerp(C("F2CF83"), C("9C6B2A"), (y - 2f) / (H - 4f)));
        r.Fill((x, y) => outer(x, y) + 5.2f, (x, y) => Color.Lerp(C("2A190D", 0.9f), C("140B05", 0.9f), (y - 5f) / (H - 10f)));
        r.Fill((x, y) => Mathf.Abs(outer(x, y) + 7.5f) - 0.8f, (x, y) => new Color(1f, 0.95f, 0.85f, Mathf.Clamp01(1f - (y - 6f) / 26f) * 0.22f));
        return r;
    }

    static Raster ReticleCorner()
    {
        const int S = 128;
        var r = new Raster(S, S);
        Sdf shape = (x, y) => Mathf.Min(Sd.Segment(x, y, 26, 26, 110, 26, 9f), Sd.Segment(x, y, 26, 26, 26, 110, 9f));
        r.Soft((x, y) => shape(x - 2f, y - 3f), new Color(0, 0, 0, 0.45f), 7f);
        r.Fill((x, y) => shape(x, y) - 3f, UIThemeConfig.GoldDark);
        r.Fill(shape, (x, y) => Color.Lerp(UIThemeConfig.Cream, C("F4DFAE"), Mathf.Clamp01((x + y - 52f) / 140f)));
        return r;
    }

    // =====================================================================
    // Íconos (128x128, blancos)
    // =====================================================================
    static Raster Icon(Sdf shape)
    {
        var r = new Raster(128, 128);
        r.Fill(shape, Color.white);
        return r;
    }

    static Raster IconPause()
    {
        return Icon((x, y) => Mathf.Min(Sd.RoundRect(x, y, 46, 64, 11, 36, 6), Sd.RoundRect(x, y, 82, 64, 11, 36, 6)));
    }

    static Raster IconClose()
    {
        return Icon((x, y) => Mathf.Min(Sd.Segment(x, y, 36, 36, 92, 92, 10), Sd.Segment(x, y, 92, 36, 36, 92, 10)));
    }

    static Raster IconCheck()
    {
        return Icon((x, y) => Mathf.Min(Sd.Segment(x, y, 28, 66, 54, 92, 11), Sd.Segment(x, y, 54, 92, 100, 38, 11)));
    }

    static Raster IconSettings()
    {
        return Icon((x, y) =>
        {
            float d = Sd.Circle(x, y, 64, 64, 33);
            for (int i = 0; i < 8; i++)
            {
                float a = i * 45f * Mathf.Deg2Rad;
                float c = Mathf.Cos(a), s = Mathf.Sin(a);
                float lx = (x - 64) * c + (y - 64) * s;
                float ly = -(x - 64) * s + (y - 64) * c;
                d = Mathf.Min(d, Sd.RoundRect(lx, ly, 40, 0, 10, 9, 3));
            }
            return Mathf.Max(d, -Sd.Circle(x, y, 64, 64, 14));
        });
    }

    static Raster IconRestart()
    {
        const float cx = 64, cy = 66, R = 36;
        const float start = 110f, span = 280f;
        float a = start * Mathf.Deg2Rad;
        // Punta de flecha al inicio del arco, apuntando en sentido horario
        Vector2 p = new Vector2(cx + R * Mathf.Cos(a), cy - R * Mathf.Sin(a));
        Vector2 tangent = new Vector2(Mathf.Sin(a), Mathf.Cos(a));     // horario, en coordenadas y-abajo
        Vector2 normal = new Vector2(Mathf.Cos(a), -Mathf.Sin(a));
        var head = new[] { p + tangent * 22f, p + normal * 21f - tangent * 6f, p - normal * 21f - tangent * 6f };
        return Icon((x, y) => Mathf.Min(Sd.Arc(x, y, cx, cy, R, 9f, start, span), Sd.Polygon(x, y, head) - 2f));
    }

    static Raster IconDice()
    {
        return Icon((x, y) =>
        {
            float d = Sd.RoundRect(x, y, 64, 64, 46, 46, 16);
            Vector2[] pips = { new Vector2(42, 42), new Vector2(86, 42), new Vector2(64, 64), new Vector2(42, 86), new Vector2(86, 86) };
            foreach (var pip in pips) d = Mathf.Max(d, -Sd.Circle(x, y, pip.x, pip.y, 9));
            return d;
        });
    }

    static Raster IconSound(bool on)
    {
        var body = new[] { new Vector2(22, 50), new Vector2(42, 50), new Vector2(66, 28), new Vector2(66, 100), new Vector2(42, 78), new Vector2(22, 78) };
        return Icon((x, y) =>
        {
            float d = Sd.Polygon(x, y, body) - 3f;
            if (on)
            {
                d = Mathf.Min(d, Sd.Arc(x, y, 66, 64, 20, 5f, -42f, 84f));
                d = Mathf.Min(d, Sd.Arc(x, y, 66, 64, 37, 5f, -48f, 96f));
            }
            else
            {
                d = Mathf.Min(d, Sd.Segment(x, y, 80, 48, 108, 80, 6.5f));
                d = Mathf.Min(d, Sd.Segment(x, y, 108, 48, 80, 80, 6.5f));
            }
            return d;
        });
    }

    static Raster IconCrown()
    {
        var crown = new[]
        {
            new Vector2(20, 42), new Vector2(42, 66), new Vector2(64, 32), new Vector2(86, 66),
            new Vector2(108, 42), new Vector2(100, 90), new Vector2(28, 90)
        };
        return Icon((x, y) =>
        {
            float d = Sd.Polygon(x, y, crown) - 2.5f;
            d = Mathf.Min(d, Sd.Circle(x, y, 20, 40, 8));
            d = Mathf.Min(d, Sd.Circle(x, y, 64, 29, 8));
            d = Mathf.Min(d, Sd.Circle(x, y, 108, 40, 8));
            d = Mathf.Min(d, Sd.RoundRect(x, y, 64, 102, 38, 7, 3.5f));
            return d;
        });
    }

    static Raster IconUsers()
    {
        return Icon((x, y) =>
        {
            float front = Mathf.Min(Sd.Circle(x, y, 52, 44, 17), Mathf.Max(Sd.RoundRect(x, y, 52, 98, 32, 26, 22), y - 106f));
            float back = Mathf.Min(Sd.Circle(x, y, 86, 38, 14), Mathf.Max(Sd.RoundRect(x, y, 90, 86, 25, 22, 18), y - 100f));
            back = Mathf.Max(back, -(front - 6f));
            return Mathf.Min(front, back);
        });
    }

    // =====================================================================
    // Auxiliares
    // =====================================================================
    static Raster Dot()
    {
        var r = new Raster(64, 64);
        r.Fill((x, y) => Sd.Circle(x, y, 32, 32, 30), Color.white);
        return r;
    }

    static Raster Glow()
    {
        var r = new Raster(128, 128);
        r.Soft((x, y) => Sd.Circle(x, y, 64, 64, 8f), Color.white, 54f);
        return r;
    }

    static Raster CardFrame()
    {
        var r = new Raster(128, 128);
        r.Fill((x, y) => Sd.Ring(Sd.Box(x, y, 3, 3, 125, 125, 22), 6f), Color.white);
        return r;
    }

    static Raster BarFill()
    {
        var r = new Raster(64, 40);
        r.Fill((x, y) => Sd.Box(x, y, 2, 2, 62, 38, 16), (x, y) => Color.Lerp(Color.white, new Color(0.8f, 0.8f, 0.8f), (y - 2f) / 36f));
        r.Fill((x, y) => Sd.Box(x, y, 8, 6, 56, 16, 5), new Color(1, 1, 1, 0.45f));
        return r;
    }

    // Silueta de pato mirando a la derecha (blanca para tintar)
    static Raster Duck()
    {
        var beak = new[] { new Vector2(102, 27), new Vector2(124, 34), new Vector2(102, 41) };
        var tail = new[] { new Vector2(10, 38), new Vector2(34, 52), new Vector2(22, 64) };
        var r = new Raster(128, 96);
        r.Fill((x, y) =>
        {
            float d = Sd.Ellipse(x, y, 58, 60, 42, 25);
            d = Mathf.Min(d, Sd.Circle(x, y, 92, 32, 16));
            d = Mathf.Min(d, Sd.Segment(x, y, 80, 52, 90, 36, 11));
            d = Mathf.Min(d, Sd.Polygon(x, y, beak) - 2f);
            d = Mathf.Min(d, Sd.Polygon(x, y, tail) - 2f);
            d = Mathf.Max(d, -Sd.Circle(x, y, 96, 28, 3.6f));                      // ojo
            d = Mathf.Max(d, -(Mathf.Abs(Sd.Ellipse(x, y, 52, 58, 22, 12)) - 1.6f)); // ala
            return d;
        }, Color.white);
        return r;
    }
}
