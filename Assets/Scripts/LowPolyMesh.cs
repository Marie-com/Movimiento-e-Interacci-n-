using System.Collections.Generic;
using UnityEngine;

public class LowPolyMesh
{
    public List<Vector3> vertices = new List<Vector3>();
    public List<int> triangles = new List<int>();
    public List<Color> colors = new List<Color>();

    public static Color Shade(Color c, float f)
    {
        return new Color(Mathf.Clamp01(c.r * f), Mathf.Clamp01(c.g * f), Mathf.Clamp01(c.b * f), 1f);
    }

    public void AddTriangle(Vector3 a, Vector3 b, Vector3 c, Color color)
    {
        int i = vertices.Count;
        vertices.Add(a); vertices.Add(b); vertices.Add(c);
        Color lin = color.linear; // el proyecto usa espacio de color Linear
        colors.Add(lin); colors.Add(lin); colors.Add(lin);
        triangles.Add(i); triangles.Add(i + 1); triangles.Add(i + 2);
    }

    public void AddQuad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color color)
    {
        AddTriangle(a, b, c, color);
        AddTriangle(a, c, d, color);
    }

    public void AddCone(Vector3 center, float radius, float height, int sides, Color color)
    {
        Vector3 apex = center + Vector3.up * height;
        for (int i = 0; i < sides; i++)
        {
            float a0 = i * Mathf.PI * 2f / sides;
            float a1 = (i + 1) * Mathf.PI * 2f / sides;
            Vector3 p0 = center + new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * radius;
            Vector3 p1 = center + new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * radius;
            AddTriangle(p0, apex, p1, color);
        }
    }

    public void AddCylinder(Vector3 baseCenter, float radius, float height, int sides, Color color)
    {
        Vector3 up = Vector3.up * height;
        for (int i = 0; i < sides; i++)
        {
            float a0 = i * Mathf.PI * 2f / sides;
            float a1 = (i + 1) * Mathf.PI * 2f / sides;
            Vector3 p0 = baseCenter + new Vector3(Mathf.Cos(a0), 0f, Mathf.Sin(a0)) * radius;
            Vector3 p1 = baseCenter + new Vector3(Mathf.Cos(a1), 0f, Mathf.Sin(a1)) * radius;
            AddQuad(p0, p0 + up, p1 + up, p1, color);
        }
    }

    public void AddBox(Vector3 center, Vector3 size, Quaternion rot, Color color)
    {
        Vector3 h = size * 0.5f;
        Vector3[] c = new Vector3[8];
        for (int i = 0; i < 8; i++)
        {
            Vector3 p = new Vector3((i & 1) == 0 ? -h.x : h.x, (i & 2) == 0 ? -h.y : h.y, (i & 4) == 0 ? -h.z : h.z);
            c[i] = center + rot * p;
        }
        AddQuad(c[1], c[3], c[7], c[5], color);
        AddQuad(c[0], c[4], c[6], c[2], color);
        AddQuad(c[2], c[6], c[7], c[3], Shade(color, 1.12f));
        AddQuad(c[0], c[1], c[5], c[4], Shade(color, 0.8f));
        AddQuad(c[4], c[5], c[7], c[6], color);
        AddQuad(c[0], c[2], c[3], c[1], color);
    }

    // Esfera facetada (octaedro): copas de árboles, rocas, arbustos
    public void AddBlob(Vector3 c, float r, float scaleY, Color color)
    {
        for (int sx = -1; sx <= 1; sx += 2)
            for (int sy = -1; sy <= 1; sy += 2)
                for (int sz = -1; sz <= 1; sz += 2)
                {
                    Vector3 a = c + new Vector3(sx * r, 0f, 0f);
                    Vector3 b = c + new Vector3(0f, sy * r * scaleY, 0f);
                    Vector3 d = c + new Vector3(0f, 0f, sz * r);
                    Color col = Shade(color, sy > 0 ? 1.12f : 0.8f);
                    if (sx * sy * sz > 0) AddTriangle(a, b, d, col);
                    else AddTriangle(a, d, b, col);
                }
    }
}