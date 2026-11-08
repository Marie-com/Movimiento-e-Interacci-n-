using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

[RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
public class IslandBuilder : MonoBehaviour
{
    [Header("Tamaño y forma")]
    public float radius = 0.15f;          // radio de la isla en metros
    public int seed = 7;                  // otra semilla = otra isla
    [Range(40, 110)] public int resolution = 72;
    public float mountainHeight = 0.45f;  // relativo al radio
    public float snowLine = 0.30f;        // relativo al radio

    [Header("Angulos: 0 bosque, 90 nieve, 180 desierto, 270 sabana")]
    public float riverAngle = 225f;
    public float pathStartAngle = 315f;
    public float pathRadius = 0.52f;

    [Header("Decoracion")]
    public int decorationAttempts = 1500;
    public int boats = 3;

    [Header("Materiales")]
    public Material matInicio;
    public Material matPuntos;
    public Material matReto;
    public Material matTrampa;
    public Material matLinea;

    public float tileDiameter { get { return 0.06f * radius; } }

    [System.NonSerialized] public List<Transform> tiles = new List<Transform>();

    const float BridgeY = 0.06f;
    static readonly float[] HillAmp = { 0.05f, 0.12f, 0.03f, 0.04f }; // bosque, nieve, desierto, sabana
    float offX, offZ;
    bool built;

    void Awake() { EnsureBuilt(); }

    void EnsureBuilt() { if (!built) Build(); }

    public Transform GetTile(int i)
    {
        EnsureBuilt();
        return tiles[i];
    }

    [ContextMenu("Vista previa en el editor")]
    public void Build()
    {
        built = true;
        offX = 100f + seed * 17.31f;
        offZ = 100f + seed * 9.77f;
        bool edit = !Application.isPlaying;

        for (int i = transform.childCount - 1; i >= 0; i--)
        {
            GameObject child = transform.GetChild(i).gameObject;
            if (Application.isPlaying) Destroy(child); else DestroyImmediate(child);
        }
        tiles.Clear();

        var rnd = new System.Random(seed);
        var lp = new LowPolyMesh();

        var dense = new List<Vector2>();
        const int samples = 480;
        for (int i = 0; i < samples; i++) dense.Add(PathPoint(i / (float)samples));

        AddTerrain(lp, rnd);
        AddWater(lp, rnd);
        AddBridges(lp, dense);
        Decorate(lp, dense, rnd);
        AddBoats(lp, rnd);

        var mesh = new Mesh();
        mesh.name = "IslaLowPoly";
        mesh.indexFormat = IndexFormat.UInt32;
        mesh.SetVertices(lp.vertices);
        mesh.SetTriangles(lp.triangles, 0);
        mesh.SetColors(lp.colors);
        mesh.RecalculateNormals();
        mesh.RecalculateBounds();
        if (edit) mesh.hideFlags = HideFlags.DontSave;
        GetComponent<MeshFilter>().sharedMesh = mesh;

        BuildPath(dense, edit);
    }

    // ---------- Ruido, bioma y altura (coordenadas normalizadas: la isla mide 1 de radio) ----------

    float Perlin(float x, float z, float freq)
    {
        return Mathf.PerlinNoise(x * freq + offX, z * freq + offZ);
    }

    static float Smooth(float a, float b, float x)
    {
        float t = Mathf.Clamp01((x - a) / (b - a));
        return t * t * (3f - 2f * t);
    }

    static Color Vary(Color c, System.Random rnd, float amount)
    {
        float k = 1f + ((float)rnd.NextDouble() * 2f - 1f) * amount;
        return new Color(Mathf.Clamp01(c.r * k), Mathf.Clamp01(c.g * k), Mathf.Clamp01(c.b * k), 1f);
    }

    float HillAmplitude(float ang)
    {
        float sum = 0f;
        for (int k = 0; k < 4; k++)
        {
            float c = Mathf.Cos(ang - k * Mathf.PI * 0.5f);
            if (c > 0f) sum += c * c * HillAmp[k];
        }
        return sum;
    }

    // 0 bosque, 1 nieve, 2 desierto, 3 sabana
    int BiomeAt(float nx, float nz)
    {
        float ang = Mathf.Atan2(nz, nx) + (Perlin(nx, nz, 1.7f) - 0.5f) * 0.7f;
        int best = 0;
        float bestC = -2f;
        for (int k = 0; k < 4; k++)
        {
            float c = Mathf.Cos(ang - k * Mathf.PI * 0.5f);
            if (c > bestC) { bestC = c; best = k; }
        }
        return best;
    }

    // 1 en el cauce del río o el lago, 0 fuera
    float RiverMask(float nx, float nz)
    {
        float ra = riverAngle * Mathf.Deg2Rad;
        Vector2 dir = new Vector2(Mathf.Cos(ra), Mathf.Sin(ra));
        float u = nx * dir.x + nz * dir.y;
        float v = -nx * dir.y + nz * dir.x;
        const float u0 = 0.30f;
        float dl = Mathf.Sqrt((u - u0) * (u - u0) + v * v);
        float lake = 1f - Smooth(0.07f, 0.13f, dl);
        if (u < u0) return lake;
        float center = 0.08f * Mathf.Sin((u - u0) * 7f);
        float width = Mathf.Lerp(0.045f, 0.09f, Mathf.Clamp01((u - u0) / 0.8f));
        float river = 1f - Smooth(width * 0.7f, width * 1.3f, Mathf.Abs(v - center));
        return Mathf.Max(lake, river);
    }

    float HeightN(float nx, float nz)
    {
        float r = Mathf.Sqrt(nx * nx + nz * nz);
        float ang = Mathf.Atan2(nz, nx);

        float coast = 0.86f + 0.12f * (Perlin(Mathf.Cos(ang), Mathf.Sin(ang), 1.6f) - 0.5f) * 2f;
        float land = 1f - Smooth(coast - 0.14f, coast + 0.03f, r);

        float h = 0.05f + Perlin(nx, nz, 2.3f) * HillAmplitude(ang) + (Perlin(nx, nz, 7f) - 0.5f) * 0.02f;

        float dz = nz - 0.20f;
        float m = Mathf.Exp(-(nx * nx + dz * dz) / (2f * 0.26f * 0.26f));
        h += mountainHeight * m * (0.7f + 0.6f * Perlin(nx, nz, 3.2f));

        h = Mathf.Lerp(-0.08f, h, land);
        return Mathf.Lerp(h, -0.035f, RiverMask(nx, nz));
    }

    // Altura donde se apoyan las casillas y la línea (sobre el puente si cruza el río)
    float PathHeight(float nx, float nz)
    {
        const float e = 0.015f;
        float h = Mathf.Max(HeightN(nx, nz), HeightN(nx + e, nz), HeightN(nx - e, nz), HeightN(nx, nz + e), HeightN(nx, nz - e));
        if (RiverMask(nx, nz) > 0.01f) h = Mathf.Max(h, BridgeY);
        return h;
    }

    // ---------- Terreno ----------

    void AddTerrain(LowPolyMesh lp, System.Random rnd)
    {
        int n = resolution;
        float ext = 1.12f;
        float cell = 2f * ext / n;
        var P = new Vector3[n + 1, n + 1];
        for (int i = 0; i <= n; i++)
            for (int j = 0; j <= n; j++)
            {
                float nx = -ext + i * cell;
                float nz = -ext + j * cell;
                P[i, j] = new Vector3(nx * radius, HeightN(nx, nz) * radius, nz * radius);
            }

        for (int i = 0; i < n; i++)
            for (int j = 0; j < n; j++)
            {
                float cx = -ext + (i + 0.5f) * cell;
                float cz = -ext + (j + 0.5f) * cell;
                if (cx * cx + cz * cz > ext * ext) continue;
                Vector3 a = P[i, j], b = P[i + 1, j], c = P[i + 1, j + 1], d = P[i, j + 1];
                AddTerrainTriangle(lp, a, d, c, rnd);
                AddTerrainTriangle(lp, a, c, b, rnd);
            }
    }

    void AddTerrainTriangle(LowPolyMesh lp, Vector3 a, Vector3 b, Vector3 c, System.Random rnd)
    {
        Vector3 normal = Vector3.Cross(b - a, c - a).normalized;
        Vector3 center = (a + b + c) / 3f;
        lp.AddTriangle(a, b, c, TerrainColor(center / radius, normal, rnd));
    }

    Color TerrainColor(Vector3 p, Vector3 normal, System.Random rnd)
    {
        float h = p.y;
        Color c;
        if (h < 0f) c = new Color(0.55f, 0.75f, 0.80f);
        else if (h < 0.025f) c = new Color(0.95f, 0.88f, 0.62f);
        else if (h > snowLine) c = new Color(0.96f, 0.98f, 1f);
        else
        {
            int b = BiomeAt(p.x, p.z);
            switch (b)
            {
                case 0: c = new Color(0.36f, 0.76f, 0.26f); break;
                case 1: c = new Color(0.92f, 0.96f, 1.00f); break;
                case 2: c = new Color(0.93f, 0.72f, 0.42f); break;
                default: c = new Color(0.76f, 0.74f, 0.32f); break;
            }
            if (1f - normal.y > 0.28f)
                c = (b == 2) ? new Color(0.72f, 0.38f, 0.25f) : new Color(0.52f, 0.52f, 0.57f);
        }
        return Vary(c, rnd, 0.07f);
    }

    // ---------- Agua ----------

    Vector3 WaterPoint(int ring, int sector, int rings, int sectors, float rad)
    {
        int sec = sector % sectors;
        float a = sec * Mathf.PI * 2f / sectors;
        float rr = rad * ring / rings;
        float jitter = (ring == 0 || ring == rings) ? 0f : (Perlin(ring * 1.7f, sec * 1.3f, 1f) - 0.5f) * 0.012f * radius;
        return new Vector3(Mathf.Cos(a) * rr, jitter, Mathf.Sin(a) * rr);
    }

    void AddWater(LowPolyMesh lp, System.Random rnd)
    {
        float rad = 1.18f * radius;
        int rings = 7, sectors = 48;
        lp.AddCylinder(new Vector3(0f, -0.10f * radius, 0f), rad, 0.10f * radius, sectors, new Color(0.08f, 0.42f, 0.72f));
        Color water = new Color(0.12f, 0.72f, 1f);
        for (int r = 0; r < rings; r++)
            for (int s = 0; s < sectors; s++)
            {
                Vector3 p00 = WaterPoint(r, s, rings, sectors, rad);
                Vector3 p01 = WaterPoint(r, s + 1, rings, sectors, rad);
                Vector3 p10 = WaterPoint(r + 1, s, rings, sectors, rad);
                Vector3 p11 = WaterPoint(r + 1, s + 1, rings, sectors, rad);
                lp.AddTriangle(p00, p01, p11, Vary(water, rnd, 0.08f));
                lp.AddTriangle(p00, p11, p10, Vary(water, rnd, 0.08f));
            }
    }

    // ---------- Camino cerrado, casillas y línea negra ----------

    Vector2 PathPoint(float t01)
    {
        float th = pathStartAngle * Mathf.Deg2Rad + t01 * Mathf.PI * 2f;
        float rp = pathRadius
                 + 0.05f * Mathf.Sin(2f * th + 1.3f)
                 + 0.04f * Mathf.Sin(3f * th + 0.4f)
                 + 0.02f * Mathf.Sin(5f * th);
        return new Vector2(Mathf.Cos(th) * rp, Mathf.Sin(th) * rp);
    }

    bool NearPath(float nx, float nz, List<Vector2> dense, float minDist)
    {
        float m2 = minDist * minDist;
        for (int i = 0; i < dense.Count; i += 2)
        {
            float dx = dense[i].x - nx, dz = dense[i].y - nz;
            if (dx * dx + dz * dz < m2) return true;
        }
        return false;
    }

    Material MaterialFor(TileType type)
    {
        switch (type)
        {
            case TileType.Inicio: return matInicio;
            case TileType.Puntos: return matPuntos;
            case TileType.Reto: return matReto;
            default: return matTrampa;
        }
    }

    void BuildPath(List<Vector2> dense, bool edit)
    {
        int n = dense.Count;
        float[] cum = new float[n + 1];
        for (int i = 0; i < n; i++)
            cum[i + 1] = cum[i] + Vector2.Distance(dense[i], dense[(i + 1) % n]);
        float total = cum[n];

        // Línea negra que une todas las casillas
        var pts = new Vector3[n];
        for (int i = 0; i < n; i++)
            pts[i] = new Vector3(dense[i].x * radius, (PathHeight(dense[i].x, dense[i].y) + 0.012f) * radius, dense[i].y * radius);
        var lineGo = new GameObject("LineaCamino");
        lineGo.transform.SetParent(transform, false);
        var lr = lineGo.AddComponent<LineRenderer>();
        lr.useWorldSpace = false;
        lr.loop = true;
        lr.positionCount = n;
        lr.SetPositions(pts);
        lr.startWidth = 0.022f * radius;
        lr.endWidth = 0.022f * radius;
        lr.numCornerVertices = 3;
        lr.shadowCastingMode = ShadowCastingMode.Off;
        lr.receiveShadows = false;
        lr.sharedMaterial = matLinea;
        if (edit) lineGo.hideFlags = HideFlags.DontSave;

        // Casillas repartidas a distancias iguales a lo largo del circuito
        int count = BoardLayout.TileCount;
        int seg = 0;
        for (int t = 0; t < count; t++)
        {
            float target = total * t / count;
            while (seg < n - 1 && cum[seg + 1] < target) seg++;
            float f = Mathf.InverseLerp(cum[seg], cum[seg + 1], target);
            Vector2 p = Vector2.Lerp(dense[seg], dense[(seg + 1) % n], f);

            var tile = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            tile.name = "Casilla_" + t;
            Collider col = tile.GetComponent<Collider>();
            if (Application.isPlaying) Destroy(col); else DestroyImmediate(col);
            tile.transform.SetParent(transform, false);
            tile.transform.localPosition = new Vector3(p.x * radius, (PathHeight(p.x, p.y) + 0.014f) * radius, p.y * radius);
            tile.transform.localScale = new Vector3(tileDiameter, 0.004f * radius, tileDiameter);
            tile.GetComponent<Renderer>().sharedMaterial = MaterialFor(BoardLayout.TypeAt(t));
            if (edit) tile.hideFlags = HideFlags.DontSave;
            tiles.Add(tile.transform);
        }
    }

    // ---------- Puente ----------

    void AddBridges(LowPolyMesh lp, List<Vector2> dense)
    {
        int n = dense.Count;
        int start = 0;
        while (start < n && RiverMask(dense[start].x, dense[start].y) > 0.05f) start++;
        if (start >= n) return;

        int k = 0;
        while (k < n)
        {
            Vector2 p = dense[(start + k) % n];
            if (RiverMask(p.x, p.y) > 0.05f)
            {
                int first = k;
                while (k < n && RiverMask(dense[(start + k) % n].x, dense[(start + k) % n].y) > 0.05f) k++;
                int last = k - 1;
                Vector2 A = dense[(start + Mathf.Max(0, first - 5)) % n];
                Vector2 B = dense[(start + Mathf.Min(n - 1, last + 5)) % n];
                BuildBridge(lp, A, B);
            }
            else k++;
        }
    }

    void BuildBridge(LowPolyMesh lp, Vector2 A, Vector2 B)
    {
        Vector3 a = new Vector3(A.x * radius, BridgeY * radius, A.y * radius);
        Vector3 b = new Vector3(B.x * radius, BridgeY * radius, B.y * radius);
        Vector3 dir = b - a;
        float len = dir.magnitude;
        if (len < 0.0001f) return;

        Quaternion rot = Quaternion.LookRotation(dir / len, Vector3.up);
        Vector3 mid = (a + b) * 0.5f;
        Vector3 side = rot * Vector3.right;
        float w = 0.075f * radius;
        Color wood = new Color(0.60f, 0.38f, 0.18f);
        Color dark = new Color(0.40f, 0.24f, 0.11f);

        lp.AddBox(mid, new Vector3(w, 0.012f * radius, len), rot, wood);
        Vector3 rail = Vector3.up * (0.02f * radius);
        lp.AddBox(mid + side * (w * 0.5f) + rail, new Vector3(0.008f * radius, 0.008f * radius, len), rot, dark);
        lp.AddBox(mid - side * (w * 0.5f) + rail, new Vector3(0.008f * radius, 0.008f * radius, len), rot, dark);

        int posts = Mathf.Max(2, Mathf.RoundToInt(len / (0.06f * radius)));
        Vector3 postUp = Vector3.up * (0.014f * radius);
        for (int i = 0; i <= posts; i++)
        {
            Vector3 pos = a + dir * (i / (float)posts);
            lp.AddBox(pos + side * (w * 0.5f) + postUp, new Vector3(0.01f * radius, 0.03f * radius, 0.01f * radius), rot, dark);
            lp.AddBox(pos - side * (w * 0.5f) + postUp, new Vector3(0.01f * radius, 0.03f * radius, 0.01f * radius), rot, dark);
        }
    }

    // ---------- Decoración ----------

    void Decorate(LowPolyMesh lp, List<Vector2> dense, System.Random rnd)
    {
        float[] density = { 0.95f, 0.40f, 0.35f, 0.35f };
        for (int k = 0; k < decorationAttempts; k++)
        {
            float a = (float)rnd.NextDouble() * Mathf.PI * 2f;
            float rr = Mathf.Sqrt((float)rnd.NextDouble()) * 0.92f;
            float nx = Mathf.Cos(a) * rr;
            float nz = Mathf.Sin(a) * rr;
            float h = HeightN(nx, nz);
            if (h < 0.03f || h > 0.26f) continue;
            if (RiverMask(nx, nz) > 0.02f) continue;
            if (NearPath(nx, nz, dense, 0.07f)) continue;
            float slope = Mathf.Abs(HeightN(nx + 0.02f, nz) - h) + Mathf.Abs(HeightN(nx, nz + 0.02f) - h);
            if (slope > 0.06f) continue;
            int biome = BiomeAt(nx, nz);
            if (rnd.NextDouble() > density[biome]) continue;

            Vector3 pos = new Vector3(nx * radius, (h - 0.01f) * radius, nz * radius);
            float s = 0.8f + (float)rnd.NextDouble() * 0.5f;
            float roll = (float)rnd.NextDouble();
            switch (biome)
            {
                case 0:
                    if (roll < 0.55f) AddPine(lp, pos, s, false, rnd);
                    else if (roll < 0.90f) AddRoundTree(lp, pos, s, rnd);
                    else AddRock(lp, pos, s, rnd);
                    break;
                case 1:
                    if (roll < 0.80f) AddPine(lp, pos, s, true, rnd);
                    else AddRock(lp, pos, s, rnd);
                    break;
                case 2:
                    if (roll < 0.70f) AddCactus(lp, pos, s, rnd);
                    else AddRock(lp, pos, s, rnd);
                    break;
                default:
                    if (roll < 0.60f) AddAcacia(lp, pos, s, rnd);
                    else if (roll < 0.80f) AddBush(lp, pos, s, rnd);
                    else AddRock(lp, pos, s, rnd);
                    break;
            }
        }
    }

    void AddPine(LowPolyMesh lp, Vector3 p, float s, bool snowy, System.Random rnd)
    {
        float u = radius * s;
        Color trunk = Vary(new Color(0.45f, 0.28f, 0.14f), rnd, 0.1f);
        Color leaf = Vary(new Color(0.14f, 0.55f, 0.22f), rnd, 0.12f);
        Color top = snowy ? Vary(new Color(0.88f, 0.95f, 0.97f), rnd, 0.04f) : leaf;
        lp.AddCylinder(p, 0.012f * u, 0.05f * u, 6, trunk);
        lp.AddCone(p + Vector3.up * (0.03f * u), 0.07f * u, 0.09f * u, 7, leaf);
        lp.AddCone(p + Vector3.up * (0.07f * u), 0.052f * u, 0.08f * u, 7, top);
        lp.AddCone(p + Vector3.up * (0.11f * u), 0.034f * u, 0.07f * u, 7, top);
    }

    void AddRoundTree(LowPolyMesh lp, Vector3 p, float s, System.Random rnd)
    {
        float u = radius * s;
        Color trunk = Vary(new Color(0.45f, 0.28f, 0.14f), rnd, 0.1f);
        Color canopy = rnd.NextDouble() < 0.25
            ? Vary(new Color(0.95f, 0.62f, 0.15f), rnd, 0.08f)
            : Vary(new Color(0.30f, 0.72f, 0.24f), rnd, 0.10f);
        lp.AddCylinder(p, 0.012f * u, 0.06f * u, 6, trunk);
        lp.AddBlob(p + Vector3.up * (0.09f * u), 0.06f * u, 0.9f, canopy);
    }

    void AddAcacia(LowPolyMesh lp, Vector3 p, float s, System.Random rnd)
    {
        float u = radius * s;
        Color trunk = Vary(new Color(0.42f, 0.30f, 0.16f), rnd, 0.1f);
        Color leaf = Vary(new Color(0.50f, 0.62f, 0.20f), rnd, 0.10f);
        lp.AddCylinder(p, 0.010f * u, 0.08f * u, 6, trunk);
        lp.AddCone(p + Vector3.up * (0.07f * u), 0.10f * u, 0.035f * u, 8, leaf);
        lp.AddCone(p + Vector3.up * (0.075f * u), 0.06f * u, 0.04f * u, 8, LowPolyMesh.Shade(leaf, 1.1f));
    }

    void AddCactus(LowPolyMesh lp, Vector3 p, float s, System.Random rnd)
    {
        float u = radius * s;
        Color g = Vary(new Color(0.20f, 0.58f, 0.28f), rnd, 0.10f);
        Quaternion rot = Quaternion.Euler(0f, (float)rnd.NextDouble() * 360f, 0f);
        Vector3 arm = new Vector3(0.03f * u, 0.01f * u, 0.01f * u);
        Vector3 tip = new Vector3(0.01f * u, 0.03f * u, 0.01f * u);
        lp.AddCylinder(p, 0.014f * u, 0.09f * u, 6, g);
        lp.AddCone(p + Vector3.up * (0.09f * u), 0.014f * u, 0.02f * u, 6, g);
        lp.AddBox(p + rot * new Vector3(0.026f * u, 0.045f * u, 0f), arm, rot, g);
        lp.AddBox(p + rot * new Vector3(0.038f * u, 0.060f * u, 0f), tip, rot, g);
        lp.AddBox(p + rot * new Vector3(-0.026f * u, 0.060f * u, 0f), arm, rot, g);
        lp.AddBox(p + rot * new Vector3(-0.038f * u, 0.075f * u, 0f), tip, rot, g);
    }

    void AddBush(LowPolyMesh lp, Vector3 p, float s, System.Random rnd)
    {
        float u = radius * s;
        lp.AddBlob(p + Vector3.up * (0.02f * u), 0.035f * u, 0.7f, Vary(new Color(0.40f, 0.60f, 0.22f), rnd, 0.1f));
    }

    void AddRock(LowPolyMesh lp, Vector3 p, float s, System.Random rnd)
    {
        float u = radius * s;
        lp.AddBlob(p + Vector3.up * (0.015f * u), 0.035f * u, 0.65f, Vary(new Color(0.58f, 0.54f, 0.50f), rnd, 0.1f));
    }

    // ---------- Barcos ----------

    void AddBoats(LowPolyMesh lp, System.Random rnd)
    {
        float ra = riverAngle * Mathf.Deg2Rad;
        Vector2 lake = new Vector2(Mathf.Cos(ra), Mathf.Sin(ra)) * 0.30f;
        AddBoat(lp, new Vector3(lake.x * radius, 0f, lake.y * radius), (float)rnd.NextDouble() * 360f);

        int placed = 0, tries = 0;
        while (placed < boats && tries < 300)
        {
            tries++;
            float a = (float)rnd.NextDouble() * Mathf.PI * 2f;
            float rr = 1.0f + (float)rnd.NextDouble() * 0.10f;
            float nx = Mathf.Cos(a) * rr;
            float nz = Mathf.Sin(a) * rr;
            if (HeightN(nx, nz) > -0.04f) continue;
            AddBoat(lp, new Vector3(nx * radius, 0f, nz * radius), (float)rnd.NextDouble() * 360f);
            placed++;
        }
    }

    void AddBoat(LowPolyMesh lp, Vector3 pos, float yaw)
    {
        float u = radius;
        Quaternion rot = Quaternion.Euler(0f, yaw, 0f);
        Color hull = new Color(0.45f, 0.26f, 0.12f);
        Color wood = new Color(0.60f, 0.38f, 0.18f);
        Color sail = new Color(0.97f, 0.97f, 0.95f);
        Vector3 c = pos + Vector3.up * (0.008f * u);

        lp.AddBox(c, new Vector3(0.03f * u, 0.014f * u, 0.08f * u), rot, hull);
        lp.AddBox(c + rot * new Vector3(0f, 0.012f * u, -0.03f * u), new Vector3(0.026f * u, 0.012f * u, 0.016f * u), rot, wood);
        lp.AddCylinder(c + rot * new Vector3(0f, 0.007f * u, 0.005f * u), 0.003f * u, 0.09f * u, 5, wood);

        Vector3 s1 = c + rot * new Vector3(0f, 0.02f * u, 0.008f * u);
        Vector3 s2 = c + rot * new Vector3(0f, 0.095f * u, 0.008f * u);
        Vector3 s3 = c + rot * new Vector3(0f, 0.02f * u, 0.040f * u);
        lp.AddTriangle(s1, s2, s3, sail);
        lp.AddTriangle(s1, s3, s2, sail);
    }
}