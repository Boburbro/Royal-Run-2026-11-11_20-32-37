using System;
using System.Collections.Generic;
using UnityEngine;

// Kod orqali yumaloq shakllar yasaydigan yordamchi: yumaloqlangan quti, aylanma (lathe) shakllar,
// o'tkir (shom) arka va arka-halqa. Hammasi bir marta yasalib, keshda saqlanadi.
public static class MeshKit
{
    static readonly Dictionary<string, Mesh> cache = new Dictionary<string, Mesh>();

    // ---------- Yumaloq burchakli quti ----------
    public static Mesh RoundedBox(Vector3 size, float r)
    {
        string key = "rb" + size.x.ToString("F2") + "," + size.y.ToString("F2") + "," + size.z.ToString("F2") + "," + r.ToString("F2");
        if (cache.TryGetValue(key, out var cached)) return cached;

        Vector3 H = size * 0.5f;
        r = Mathf.Min(r, Mathf.Min(H.x, Mathf.Min(H.y, H.z)) * 0.99f);
        var v = new List<Vector3>();
        var nrm = new List<Vector3>();
        var t = new List<int>();

        for (int a = 0; a < 3; a++)
            for (int sgn = -1; sgn <= 1; sgn += 2)
            {
                int b = (a + 1) % 3, c = (a + 2) %3;
                float[] cb = Coords(H[b], r), cc = Coords(H[c], r);
                int start = v.Count;
                Vector3 axis = Vector3.zero; axis[a] = sgn;
                for (int i = 0; i < cb.Length; i++)
                    for (int j = 0; j < cc.Length; j++)
                    {
                        Vector3 p = Vector3.zero;
                        p[a] = sgn * H[a]; p[b] = cb[i]; p[c] = cc[j];
                        Vector3 inner = new Vector3(
                            Mathf.Clamp(p.x, -(H.x - r), H.x - r),
                            Mathf.Clamp(p.y, -(H.y - r), H.y - r),
                            Mathf.Clamp(p.z, -(H.z - r), H.z - r));
                        Vector3 d = p - inner;
                        Vector3 n = d.sqrMagnitude < 1e-8f ? axis : d.normalized;
                        v.Add(inner + n * r);
                        nrm.Add(n);
                    }
                int w = cc.Length;
                for (int i = 0; i < cb.Length - 1; i++)
                    for (int j = 0; j < w - 1; j++)
                    {
                        int i0 = start + i * w + j;
                        AddTri(v, t, i0, i0 + 1, i0 + w, nrm[i0]);
                        AddTri(v, t, i0 + 1, i0 + w + 1, i0 + w, nrm[i0]);
                    }
            }

        var m = new Mesh();
        m.SetVertices(v); m.SetNormals(nrm); m.SetTriangles(t, 0);
        m.RecalculateBounds();
        cache[key] = m;
        return m;
    }

    static float[] Coords(float h, float r)
    {
        return new[] { -h, -h + 0.25f * r, -h + 0.6f * r, -h + r, h - r, h - 0.6f * r, h - 0.25f * r, h };
    }

    // uchburchakni berilgan normal tomonga qaratib qo'shadi
    static void AddTri(List<Vector3> v, List<int> t, int a, int b, int c, Vector3 want)
    {
        Vector3 geo = Vector3.Cross(v[b] - v[a], v[c] - v[a]);
        if (Vector3.Dot(geo, want) >= 0) { t.Add(a); t.Add(b); t.Add(c); }
        else { t.Add(a); t.Add(c); t.Add(b); }
    }

    // ---------- Aylanma shakl (gumbaz, minora, chiroq) ----------
    // profile: (radius, balandlik) nuqtalari pastdan yuqoriga
    public static Mesh Lathe(string key, Vector2[] prof, int segs = 20)
    {
        if (cache.TryGetValue(key, out var cached)) return cached;
        var v = new List<Vector3>();
        var t = new List<int>();
        for (int i = 0; i < prof.Length; i++)
            for (int j = 0; j <= segs; j++)
            {
                float ang = j * Mathf.PI * 2f / segs;
                v.Add(new Vector3(prof[i].x * Mathf.Cos(ang), prof[i].y, prof[i].x * Mathf.Sin(ang)));
            }
        int stride = segs + 1;
        for (int i = 0; i < prof.Length - 1; i++)
            for (int j = 0; j < segs; j++)
            {
                int a = i * stride + j, b = a + 1, c = a + stride, d = c + 1;
                t.Add(a); t.Add(c); t.Add(b);
                t.Add(b); t.Add(c); t.Add(d);
            }

        // eng tik segmentda tashqariga qaraydimi? Qaramasa hammasini aylantiramiz
        int best = 0; float bd = -1;
        for (int i = 0; i < prof.Length - 1; i++)
        {
            float dy = Mathf.Abs(prof[i + 1].y - prof[i].y);
            if (prof[i].x > 0.01f && prof[i + 1].x > 0.01f && dy > bd) { bd = dy; best = i; }
        }
        int k = (best * segs) * 6;
        int ia = t[k], ib = t[k + 1], ic = t[k + 2];
        Vector3 geo = Vector3.Cross(v[ib] - v[ia], v[ic] - v[ia]);
        Vector3 cen = (v[ia] + v[ib] + v[ic]) / 3f; cen.y = 0;
        if (Vector3.Dot(geo, cen) < 0)
            for (int q = 0; q < t.Count; q += 3) { int tmp = t[q + 1]; t[q + 1] = t[q + 2]; t[q + 2] = tmp; }

        var m = new Mesh();
        m.SetVertices(v); m.SetTriangles(t, 0);
        m.RecalculateNormals();
        m.RecalculateBounds();
        cache[key] = m;
        return m;
    }

    // ---------- Shom (o'tkir) arka ----------
    static List<Vector2> ArchOutline(float W, float H)
    {
        float R = W * 0.8f;
        float xc = R - W / 2f;
        float th = Mathf.Acos(-xc / R);
        float apex = R * Mathf.Sin(th);
        float spring = Mathf.Max(0.1f, H - apex);
        var left = new List<Vector2> { new Vector2(-W / 2f, 0), new Vector2(-W / 2f, spring) };
        int n = 8;
        for (int k = 1; k <= n; k++)
        {
            float ang = Mathf.Lerp(Mathf.PI, th, k / (float)n);
            left.Add(new Vector2(xc + R * Mathf.Cos(ang), spring + R * Mathf.Sin(ang)));
        }
        var pts = new List<Vector2>(left);
        for (int i = left.Count - 2; i >= 0; i--) pts.Add(new Vector2(-left[i].x, left[i].y));
        return pts;
    }

    // To'ldirilgan arka (ikki tomonlama)
    public static Mesh Arch(float W, float H)
    {
        string key = "ar" + W.ToString("F2") + "," + H.ToString("F2");
        if (cache.TryGetValue(key, out var cached)) return cached;
        var o = ArchOutline(W, H);
        var v = new List<Vector3>();
        foreach (var p in o) v.Add(new Vector3(p.x, p.y, 0));
        v.Add(new Vector3(0, H * 0.4f, 0));
        int cen = v.Count - 1;
        var t = new List<int>();
        for (int i = 0; i < o.Count - 1; i++) AddTri(v, t, cen, i, i + 1, Vector3.forward);
        return TwoSided(v, t, key);
    }

    // Arka halqa (tashqi va ichki arka orasi)
    public static Mesh Ring(float Wo, float Ho, float Wi, float Hi)
    {
        string key = "rg" + Wo.ToString("F2") + "," + Ho.ToString("F2") + "," + Wi.ToString("F2") + "," + Hi.ToString("F2");
        if (cache.TryGetValue(key, out var cached)) return cached;
        var o = ArchOutline(Wo, Ho);
        var inn = ArchOutline(Wi, Hi);
        var v = new List<Vector3>();
        foreach (var p in o) v.Add(new Vector3(p.x, p.y, 0));
        foreach (var p in inn) v.Add(new Vector3(p.x, p.y, 0));
        int n = o.Count;
        var t = new List<int>();
        for (int i = 0; i < n - 1; i++)
        {
            AddTri(v, t, i, i + 1, n + i, Vector3.forward);
            AddTri(v, t, i + 1, n + i + 1, n + i, Vector3.forward);
        }
        return TwoSided(v, t, key);
    }

    static Mesh TwoSided(List<Vector3> v, List<int> frontTris, string key)
    {
        int n = v.Count;
        var vv = new List<Vector3>(v);
        var nn = new List<Vector3>();
        for (int i = 0; i < n; i++) nn.Add(Vector3.forward);
        vv.AddRange(v);
        for (int i = 0; i < n; i++) nn.Add(Vector3.back);
        var t = new List<int>(frontTris);
        for (int i = 0; i < frontTris.Count; i += 3)
        {
            t.Add(frontTris[i] + n); t.Add(frontTris[i + 2] + n); t.Add(frontTris[i + 1] + n);
        }
        var m = new Mesh();
        m.SetVertices(vv); m.SetNormals(nn); m.SetTriangles(t, 0);
        m.RecalculateBounds();
        cache[key] = m;
        return m;
    }

    // ---------- Tayyor profillar ----------
    public static Mesh Dome()
    {
        return Lathe("dome", new[]
        {
            new Vector2(2.4f, 0), new Vector2(2.75f, 0.5f), new Vector2(2.8f, 1.2f), new Vector2(2.4f, 2.1f),
            new Vector2(1.7f, 2.9f), new Vector2(0.9f, 3.4f), new Vector2(0.3f, 3.75f), new Vector2(0.05f, 3.95f)
        }, 24);
    }

    public static Mesh Minaret()
    {
        return Lathe("minaret", new[]
        {
            new Vector2(1.25f, 0), new Vector2(1.1f, 2f), new Vector2(0.9f, 11.8f), new Vector2(1.35f, 12f),
            new Vector2(1.35f, 12.5f), new Vector2(0.7f, 12.6f), new Vector2(0.65f, 14.5f),
            new Vector2(0.9f, 14.6f), new Vector2(0.9f, 15f), new Vector2(0.55f, 15.9f), new Vector2(0.05f, 17f)
        }, 20);
    }

    public static Mesh LampPost()
    {
        return Lathe("lamppost", new[]
        {
            new Vector2(0.25f, 0), new Vector2(0.15f, 0.5f), new Vector2(0.09f, 1f), new Vector2(0.07f, 4f), new Vector2(0.16f, 4.1f)
        }, 12);
    }

    public static Mesh Lantern()
    {
        return Lathe("lantern", new[]
        {
            new Vector2(0.12f, 0), new Vector2(0.3f, 0.25f), new Vector2(0.3f, 0.7f), new Vector2(0.14f, 0.95f), new Vector2(0.03f, 1.1f)
        }, 12);
    }

    public static Mesh Vase()
    {
        return Lathe("vase", new[]
        {
            new Vector2(0.3f, 0), new Vector2(0.45f, 0.4f), new Vector2(0.3f, 0.9f), new Vector2(0.4f, 1.2f), new Vector2(0.2f, 1.2f)
        }, 12);
    }
}
