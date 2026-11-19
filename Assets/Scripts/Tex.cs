using System;
using UnityEngine;

// Kod orqali yasaladigan teksturalar: g'isht, girih kafel, naqshli lenta, yog'och, shag'al,
// tosh yo'lak, gumbaz kafeli, gilam (ikat), chodir chiziqlari.
// Hammasi choksiz (tiling) va bir marta yasalib, keshda saqlanadi.
public static class Tex
{
    static Texture2D brick, girih, band, wood, gravel, paving, domeTiles, ikat, awning, doppi, windows, windowsGlow;

    public static Texture2D Brick => brick != null ? brick : (brick = Make("Brick", 256, BrickPx));
    public static Texture2D Girih => girih != null ? girih : (girih = Make("Girih", 256, GirihPx));
    public static Texture2D Band => band != null ? band : (band = Make("Band", 128, BandPx));
    public static Texture2D Wood => wood != null ? wood : (wood = Make("Wood", 128, WoodPx));
    public static Texture2D Gravel => gravel != null ? gravel : (gravel = Make("Gravel", 256, GravelPx));
    public static Texture2D Paving => paving != null ? paving : (paving = Make("Paving", 256, PavingPx));
    public static Texture2D DomeTiles => domeTiles != null ? domeTiles : (domeTiles = Make("DomeTiles", 128, DomePx));
    public static Texture2D Ikat => ikat != null ? ikat : (ikat = Make("Ikat", 128, IkatPx));
    public static Texture2D Doppi => doppi != null ? doppi : (doppi = Make("Doppi", 128, DoppiPx));
    public static Texture2D Windows => windows != null ? windows : (windows = Make("Windows", 128, (u, v) => WindowsPx(u, v, false)));
    public static Texture2D WindowsGlow => windowsGlow != null ? windowsGlow : (windowsGlow = Make("WindowsGlow", 128, (u, v) => WindowsPx(u, v, true)));
    public static Texture2D Awning => awning != null ? awning : (awning = Make("Awning", 64, AwningPx));

    static Texture2D Make(string name, int n, Func<float, float, Color> px)
    {
        var tex = new Texture2D(n, n, TextureFormat.RGBA32, true)
        {
            name = name, wrapMode = TextureWrapMode.Repeat, filterMode = FilterMode.Trilinear, anisoLevel = 4
        };
        var data = new Color32[n * n];
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
                data[y * n + x] = px((x + 0.5f) / n, (y + 0.5f) / n);
        tex.SetPixels32(data);
        tex.Apply(true, true); // mipmap + GPU xotiraga, CPU nusxasi o'chadi
        return tex;
    }

    // ---------- shovqin (choksiz) ----------
    static float Hash(int x, int y)
    {
        uint h = (uint)(x * 374761393 + y * 668265263);
        h = (h ^ (h >> 13)) * 1274126177u;
        return ((h ^ (h >> 16)) & 0xFFFF) / 65535f;
    }

    static int Wrap(int a, int p) { a %= p; return a < 0 ? a + p : a; }

    // davr (period) bo'yicha takrorlanadigan qiymat shovqini, u,v 0..1
    static float Noise(float u, float v, int period)
    {
        float x = u * period, y = v * period;
        int ix = Mathf.FloorToInt(x), iy = Mathf.FloorToInt(y);
        float fx = x - ix, fy = y - iy;
        fx = fx * fx * (3 - 2 * fx); fy = fy * fy * (3 - 2 * fy);
        float a = Hash(Wrap(ix, period), Wrap(iy, period)), b = Hash(Wrap(ix + 1, period), Wrap(iy, period));
        float c = Hash(Wrap(ix, period), Wrap(iy + 1, period)), d = Hash(Wrap(ix + 1, period), Wrap(iy + 1, period));
        return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
    }

    static float Fbm(float u, float v, int p)
    {
        return Noise(u, v, p) * 0.5f + Noise(u, v, p * 2) * 0.3f + Noise(u, v, p * 4) * 0.2f;
    }

    static Color Gray(float g) { return new Color(g, g, g, 1); }

    // ---------- naqshlar ----------
    // Buxoro g'ishti: 8 qator, har qatorda 4 ta g'isht, yarim siljish. Kulrang (rang bilan bo'yaladi).
    static Color BrickPx(float u, float v)
    {
        const int rows = 8, cols = 4;
        float ry = v * rows;
        int row = Mathf.FloorToInt(ry);
        float rx = u * cols + (row % 2) * 0.5f;
        int col = Mathf.FloorToInt(rx);
        float fy = ry - row, fx = rx - col;
        float mortar = Mathf.Min(Mathf.Min(fy, 1 - fy) * 2.2f, Mathf.Min(fx, 1 - fx) * 4.4f);
        float shade = 0.86f + 0.14f * Hash(Wrap(col, cols), row);
        shade *= 0.93f + 0.07f * Fbm(u, v, 16);
        if (mortar < 0.12f) return Gray(0.7f + 0.06f * Noise(u, v, 64));
        float edge = Mathf.Clamp01((mortar - 0.12f) * 6f);
        return Gray(Mathf.Lerp(0.78f, shade, edge));
    }

    // Girih: sakkiz qirrali yulduz, oq chiziqlar, firuza va to'q ko'k (o'z rangida, oq bilan bo'yaladi)
    static Color GirihPx(float u, float v)
    {
        Color bg = new Color(0.07f, 0.18f, 0.5f), tq = new Color(0.1f, 0.62f, 0.74f), wh = new Color(0.96f, 0.96f, 0.93f);
        Color gold = new Color(0.98f, 0.78f, 0.25f), lb = new Color(0.32f, 0.58f, 0.9f);
        float x = u - 0.5f, y = v - 0.5f;
        float ax = Mathf.Abs(x), ay = Mathf.Abs(y);
        float star = Mathf.Min(Mathf.Max(ax, ay), (ax + ay) * 0.7071f);
        const float A = 0.3f;
        Color c;
        if (Mathf.Abs(star - A) < 0.022f) c = wh;
        else if (star < A * 0.42f) c = Mathf.Abs(star - A * 0.42f) < 0.02f ? wh : gold;
        else if (star < A) c = tq;
        else
        {
            // burchaklardagi kichik romb va ularni bog'lovchi chiziqlar
            float cx = 0.5f - ax, cy = 0.5f - ay;
            float dia = cx + cy;
            if (dia < 0.13f) c = dia > 0.1f ? wh : lb;
            else if (ax < 0.012f || ay < 0.012f) c = wh;
            else if (Mathf.Abs(cx - cy) < 0.016f && dia < 0.42f) c = wh;
            else c = bg;
        }
        float n = 0.94f + 0.06f * Noise(u, v, 32);
        return c * n;
    }

    // Naqshli lenta (poyezd va bezaklar uchun): ko'k fonda oq romblar, tepada-pastda oq chiziq
    static Color BandPx(float u, float v)
    {
        Color bg = new Color(0.1f, 0.3f, 0.75f), wh = new Color(0.97f, 0.97f, 0.97f), tq = new Color(0.35f, 0.75f, 0.95f);
        Color navy = new Color(0.05f, 0.12f, 0.35f);
        float x = u - 0.5f, y = v - 0.5f;
        float ay = Mathf.Abs(y);
        if (ay > 0.44f) return wh;
        if (ay > 0.41f) return navy;
        float d = Mathf.Abs(x) + ay;
        if (d < 0.12f) return wh;
        if (d < 0.22f) return tq;
        if (d > 0.3f && d < 0.36f) return wh;
        // yon tomonlardagi yarim romblar (keyingi plitka bilan ulanadi)
        float d2 = (0.5f - Mathf.Abs(x)) + ay;
        if (d2 < 0.1f) return wh;
        return bg;
    }

    // Yog'och: tola chiziqlari (u bo'ylab)
    static Color WoodPx(float u, float v)
    {
        float grain = Mathf.Sin((v * 9f + Fbm(u, v, 4) * 2.2f) * Mathf.PI * 2f) * 0.5f + 0.5f;
        float g = 0.72f + 0.2f * grain + 0.08f * Noise(u, v, 32);
        float plank = Mathf.Abs(Mathf.Repeat(v * 2f, 1f) - 0.5f) > 0.47f ? 0.6f : 1f;
        return Gray(g * plank);
    }

    // Shag'al: mayda toshlar
    static Color GravelPx(float u, float v)
    {
        const int cells = 48;
        float x = u * cells, y = v * cells;
        int ix = Mathf.FloorToInt(x), iy = Mathf.FloorToInt(y);
        float best = 9f, best2 = 9f, id = 0f;
        for (int oy = -1; oy <= 1; oy++)
            for (int ox = -1; ox <= 1; ox++)
            {
                int cx = ix + ox, cy = iy + oy;
                float px = cx + Hash(Wrap(cx, cells), Wrap(cy, cells) + 101);
                float py = cy + Hash(Wrap(cx, cells) + 57, Wrap(cy, cells));
                float d = (px - x) * (px - x) + (py - y) * (py - y);
                if (d < best) { best2 = best; best = d; id = Hash(Wrap(cx, cells), Wrap(cy, cells)); }
                else if (d < best2) best2 = d;
            }
        float edge = Mathf.Clamp01((Mathf.Sqrt(best2) - Mathf.Sqrt(best)) * 3f);
        float g = (0.62f + 0.38f * id) * Mathf.Lerp(0.55f, 1f, edge);
        Color c = new Color(g, g * 0.97f, g * 0.93f, 1);
        return c * (0.9f + 0.1f * Noise(u, v, 16));
    }

    // Tosh yo'lak: 4x4 plitka, qatorlar yarim siljigan, orasida ariqcha
    static Color PavingPx(float u, float v)
    {
        const int n = 4;
        float ry = v * n;
        int row = Mathf.FloorToInt(ry);
        float rx = u * n + (row % 2) * 0.5f;
        int col = Mathf.FloorToInt(rx);
        float fy = ry - row, fx = rx - col;
        float e = Mathf.Min(Mathf.Min(fx, 1 - fx), Mathf.Min(fy, 1 - fy));
        float shade = 0.84f + 0.16f * Hash(Wrap(col, n), row + 13);
        shade *= 0.92f + 0.08f * Fbm(u, v, 8);
        if (e < 0.035f) return Gray(0.55f);
        if (e < 0.07f) return Gray(shade * 1.06f); // qirra yorug'roq
        return Gray(shade);
    }

    // Gumbaz kafeli: romb panjara (kulrang, firuza bilan bo'yaladi)
    static Color DomePx(float u, float v)
    {
        float a = Mathf.Repeat(u * 2f + v * 2f, 1f), b = Mathf.Repeat(u * 2f - v * 2f, 1f);
        float line = Mathf.Min(Mathf.Min(a, 1 - a), Mathf.Min(b, 1 - b));
        float g = line < 0.06f ? 0.62f : 0.9f + 0.1f * Mathf.Clamp01(line * 3f);
        return Gray(g * (0.95f + 0.05f * Noise(u, v, 16)));
    }

    // Ikat gilam naqshi: cho'zinchoq romblar va chiziqlar (kulrang, rang bilan bo'yaladi)
    static Color IkatPx(float u, float v)
    {
        float x = Mathf.Abs(u - 0.5f), y = Mathf.Abs(Mathf.Repeat(v * 2f, 1f) - 0.5f);
        float d = x * 1.6f + y;
        float blur = 0.03f * (Noise(u, v, 32) - 0.5f);
        if (u < 0.06f || u > 0.94f) return Gray(1f);
        if (d + blur < 0.2f) return Gray(1f);
        if (d + blur < 0.3f) return Gray(0.45f);
        if (d + blur < 0.38f) return new Color(1f, 0.95f, 0.7f) * 1.2f;
        return Gray(0.8f);
    }

    // Chodir: qizil-oq chiziqlar (o'z rangida)
    static Color AwningPx(float u, float v)
    {
        return Mathf.Repeat(u * 2f, 1f) < 0.5f ? new Color(0.85f, 0.2f, 0.18f) : new Color(0.98f, 0.95f, 0.88f);
    }

    // Do'ppi naqshi: to'q ko'k fonda oq romb ramka, ichida to'q sariq romb, burchaklarda oq uchburchaklar
    static Color DoppiPx(float u, float v)
    {
        Color bg = new Color(0.07f, 0.09f, 0.22f), wh = new Color(0.97f, 0.96f, 0.92f);
        Color orange = new Color(0.95f, 0.5f, 0.14f), red = new Color(0.75f, 0.15f, 0.12f);
        float x = Mathf.Abs(u - 0.5f), y = Mathf.Abs(v - 0.5f);
        float d = x + y;
        if (d < 0.07f) return red;
        if (d < 0.15f) return orange;
        if (d < 0.2f) return bg;
        if (d < 0.28f) return wh;
        if (d > 0.32f && d < 0.35f) return wh;           // ingichka tashqi chiziq
        float c = (0.5f - x) + (0.5f - y);
        if (c < 0.1f) return orange;                     // burchak (qo'shni plitka bilan romb bo'ladi)
        return bg * (0.95f + 0.05f * Noise(u, v, 16));
    }

    // Zamonaviy bino oynalari: 4x4 katak, ramka, ko'k shisha; ba'zi derazalarda chiroq yoniq.
    // glow = true: faqat yoniq derazalar (emission xaritasi uchun), qolgani qora.
    static Color WindowsPx(float u, float v, bool glow)
    {
        const int n = 4;
        int cx = Mathf.FloorToInt(u * n), cy = Mathf.FloorToInt(v * n);
        float fx = u * n - cx, fy = v * n - cy;
        bool frame = fx < 0.1f || fx > 0.9f || fy < 0.16f || fy > 0.94f;
        bool lit = Hash(cx + 7, cy + 31) > 0.55f;
        if (glow) return frame || !lit ? Color.black : new Color(1f, 0.82f, 0.5f);
        if (frame) return Gray(0.82f);
        float grad = 0.85f + 0.15f * fy;
        return lit ? new Color(0.95f, 0.85f, 0.6f) * grad : new Color(0.25f, 0.42f, 0.62f) * grad;
    }
}
