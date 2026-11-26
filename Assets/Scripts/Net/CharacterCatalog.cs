using UnityEngine;

// Telefon va lobbi uchun qahramonlar ro'yxati. Ma'lumot RunnerGame.Styles dan olinadi (3 ta qahramon).
public class CharacterDef
{
    public string name;
    public string description;
    public string TintHex;   // "#rrggbb" - telefon sahifasidagi karta rangi
    public Color tint;
    public string LookJson;  // telefon sahifasida qahramon rasmini chizish uchun
}

public static class CharacterCatalog
{
    static CharacterDef[] all;

    // Qahramonlarning 3D rasmi (PNG, base64) - telefonlarga yuboriladi. MultiHost to'ldiradi.
    public static string[] ImageMsgs;

    static string H(Color c) { return "\"#" + ColorUtility.ToHtmlStringRGB(c) + "\""; }

    static string MakeLook(int i)
    {
        var l = RunnerGame.StyleLook(i);
        return "{\"sk\":" + H(l.skin) + ",\"hr\":" + H(l.hair) + ",\"tp\":" + H(l.top) + ",\"pn\":" + H(l.pants)
            + ",\"sh\":" + H(l.shoe) + ",\"ht\":" + H(l.hat) + ",\"ac\":" + H(l.accent) + ",\"hd\":" + l.head
            + ",\"gl\":" + (l.glasses ? "true" : "false") + ",\"pt\":" + (l.ponytail ? "true" : "false") + "}";
    }

    static readonly string[] ZoneEmoji = { "\U0001F54C", "\U0001F3F0", "\u26F0\uFE0F", "\U0001F3D9\uFE0F" };

    // Zona va kun vaqti kartalari (oxirgi element - Mix/Auto)
    public static string ZonesJson()
    {
        var sb = new System.Text.StringBuilder("[");
        for (int i = 0; i < 4; i++)
            sb.Append("{\"n\":\"").Append(MultiHost.ZoneLabel(i)).Append("\",\"a\":").Append(H(RunnerGame.ZoneAccent(i)))
              .Append(",\"g\":").Append(H(RunnerGame.ZoneGround(i))).Append(",\"e\":\"").Append(ZoneEmoji[i]).Append("\"},");
        sb.Append("{\"n\":\"Mix\",\"a\":\"#7e57c2\",\"g\":\"#26a69a\",\"e\":\"\U0001F500\"}]");
        return sb.ToString();
    }

    public static string TimesJson()
    {
        var sb = new System.Text.StringBuilder("[");
        for (int i = 0; i < 4; i++)
            sb.Append("{\"n\":\"").Append(MultiHost.TimeNames[i]).Append("\",\"a\":").Append(H(RunnerGame.TimeTop(i)))
              .Append(",\"g\":").Append(H(RunnerGame.TimeHorizon(i))).Append("},");
        sb.Append("{\"n\":\"Auto\",\"a\":\"#7e57c2\",\"g\":\"#ffb74d\"}]");
        return sb.ToString();
    }

    public static int Count { get { return RunnerGame.StyleCount; } }

    public static CharacterDef[] All
    {
        get
        {
            if (all == null || all.Length != RunnerGame.StyleCount)
            {
                all = new CharacterDef[RunnerGame.StyleCount];
                for (int i = 0; i < all.Length; i++)
                {
                    Color c = RunnerGame.StyleTint(i);
                    all[i] = new CharacterDef
                    {
                        name = RunnerGame.StyleName(i),
                        description = RunnerGame.StyleInfo(i),
                        tint = c,
                        TintHex = "#" + ColorUtility.ToHtmlStringRGB(c),
                        LookJson = MakeLook(i),
                    };
                }
            }
            return all;
        }
    }
}
