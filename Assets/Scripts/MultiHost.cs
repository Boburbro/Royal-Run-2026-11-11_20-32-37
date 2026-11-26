using System.Collections.Generic;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;

// Ko'p o'yinchi boshqaruvchisi: lobbi (QR-kod), sanoq, bo'lingan ekran, pauza, natijalar.
// Telefonlar PhoneHub orqali joystik bo'ladi. Har bir o'yinchi uchun RunnerGame ning alohida "dunyo" nusxasi yaratiladi.
public class MultiHost : MonoBehaviour
{
    enum State { Lobby, Countdown, Playing, Results }

    static MultiHost inst;

    RunnerGame solo;
    PhoneHub hub;
    Camera lobbyCam;
    State state = State.Lobby;
    readonly List<RunnerGame> worlds = new List<RunnerGame>();
    readonly List<PhonePlayer> roundPlayers = new List<PhonePlayer>();
    AudioSource music;
    Texture2D qrTex;
    string qrUrl;
    float countT;
    int lastCount = -1;
    float sendT;
    float allDeadAt = -1f;
    bool paused;
    int[] resultOrder = new int[0];

    public static void Open(RunnerGame soloGame)
    {
        if (inst != null) return;
        var go = new GameObject("MultiHost");
        inst = go.AddComponent<MultiHost>();
        inst.solo = soloGame;
        inst.Init();
    }

    void Init()
    {
        if (solo != null) solo.Suspend(true);
        Application.runInBackground = true;
        Screen.orientation = ScreenOrientation.AutoRotation;

        var c = new GameObject("LobbyCamera");
        c.transform.SetParent(transform, false);
        lobbyCam = c.AddComponent<Camera>();
        lobbyCam.clearFlags = CameraClearFlags.SolidColor;
        lobbyCam.backgroundColor = new Color(0.1f, 0.06f, 0.2f);
        lobbyCam.cullingMask = 0;
        lobbyCam.depth = -10f;

        // kompyuterdagi fon musiqasi (tovush effektlari telefonda chiqadi)
        gameObject.AddComponent<AudioListener>();
        AudioListener.volume = PlayerPrefs.GetInt("RoyalRunMute", 0) == 1 ? 0f : 1f;
        music = gameObject.AddComponent<AudioSource>();
        music.clip = RunnerGame.BuildMusicClip();
        music.loop = true;
        music.spatialBlend = 0f;
        music.volume = 0.35f;
        music.Play();

        CaptureHeroImages(); // har safar yangidan: eski keshdagi rasm qolib ketmasin

        hub = gameObject.AddComponent<PhoneHub>();
        hub.Enabled = true;
        hub.AcceptNewPlayers = true;
        hub.Changed += OnHubChanged;
        hub.AgainPressed += OnAgain;
    }

    void OnDestroy()
    {
        if (inst == this) inst = null;
        foreach (var g in grads.Values) if (g != null) Destroy(g);
        for (int i = 0; i < previews.Length; i++) DestroyPreview(i);
        if (qrTex != null) Destroy(qrTex);
    }

    // ---------------- yordamchilar ----------------
    List<PhonePlayer> Players()
    {
        var l = new List<PhonePlayer>();
        foreach (var p in hub.Slots) if (p != null) l.Add(p);
        return l;
    }

    bool AllReady()
    {
        var l = Players();
        if (l.Count == 0) return false;
        foreach (var p in l) if (!p.ready || !p.connected) return false;
        return true;
    }

    bool HasKeyboardPlayer()
    {
        foreach (var p in hub.Slots) if (p != null && p.isKeyboard) return true;
        return false;
    }

    void OnHubChanged()
    {
        if (state == State.Lobby || state == State.Countdown) SendLobbyToAll();
    }

    void SendLobbyToAll()
    {
        var l = Players();
        int ready = 0;
        foreach (var p in l) if (p.ready) ready++;
        var taken = new StringBuilder("[");
        for (int i = 0; i < CharacterCatalog.Count; i++)
        {
            if (i > 0) taken.Append(',');
            taken.Append(hub.IsCharacterTaken(i, null) ? "true" : "false");
        }
        taken.Append(']');
        // hamma o'yinchilarning tanlovi: har bir telefonda ko'rinadi
        var others = new StringBuilder("[");
        for (int i = 0; i < l.Count; i++)
        {
            var q = l[i];
            if (i > 0) others.Append(',');
            others.Append("{\"s\":").Append(q.slot).Append(",\"n\":").Append(PhoneHub.Quote(q.name))
                .Append(",\"hi\":").Append(q.character).Append(",\"zi\":").Append(q.voteZone).Append(",\"ti\":").Append(q.voteTime)
                .Append(",\"h\":").Append(PhoneHub.Quote(q.character >= 0 ? CharacterCatalog.All[q.character].name : "-"))
                .Append(",\"z\":").Append(PhoneHub.Quote(VoteName(q.voteZone, true)))
                .Append(",\"t\":").Append(PhoneHub.Quote(VoteName(q.voteTime, false)))
                .Append(",\"r\":").Append(q.ready ? "true" : "false").Append('}');
        }
        others.Append(']');
        foreach (var p in l)
        {
            hub.SendTo(p, "{\"t\":\"lobby\",\"others\":" + others + ",\"char\":" + p.character + ",\"ready\":" + (p.ready ? "true" : "false")
                + ",\"vz\":" + p.voteZone + ",\"vt\":" + p.voteTime + ",\"taken\":" + taken + ",\"readyCount\":" + ready + ",\"players\":" + l.Count + "}");
        }
    }

    void OnAgain(PhonePlayer p)
    {
        if (state != State.Results) return;
        foreach (var q in roundPlayers)
            if (!q.isKeyboard && q.connected && !q.wantsAgain) return;
        Restart();
    }

    // ---------------- holatlar ----------------
    void Update()
    {
        UpdatePreviews(state == State.Lobby || state == State.Countdown);
        var k = Keyboard.current;
        switch (state)
        {
            case State.Lobby:
                if (k != null)
                {
                    if (k.escapeKey.wasPressedThisFrame) { Close(); return; }
                    if (k.tabKey.wasPressedThisFrame) hub.UrlIndex++;
                    foreach (var kp2 in Players())
                    {
                        if (!kp2.isKeyboard) continue;
                        if (k.zKey.wasPressedThisFrame) { kp2.voteZone = (kp2.voteZone + 1) % 5; SendLobbyToAll(); }
                        if (k.tKey.wasPressedThisFrame) { kp2.voteTime = (kp2.voteTime + 1) % 5; SendLobbyToAll(); }
                    }
                    if (k.kKey.wasPressedThisFrame && !HasKeyboardPlayer())
                    {
                        var kp = hub.AddKeyboardPlayer();
                        if (kp != null) kp.ready = true;
                        SendLobbyToAll();
                    }
                }
                if (AllReady()) { state = State.Countdown; countT = 3.99f; lastCount = -1; ResolveVotes(); }
                break;

            case State.Countdown:
                if (k != null && k.escapeKey.wasPressedThisFrame) { Close(); return; }
                if (!AllReady()) { state = State.Lobby; SendLobbyToAll(); break; }
                countT -= Time.unscaledDeltaTime;
                int n = Mathf.FloorToInt(countT);
                if (n != lastCount)
                {
                    lastCount = n;
                    foreach (var p in Players()) hub.SendTo(p, "{\"t\":\"count\",\"n\":" + Mathf.Max(0, n) + ",\"info\":" + PhoneHub.Quote(resText) + "}");
                }
                if (countT <= 0f) StartRound();
                break;

            case State.Playing:
                if (k != null && k.escapeKey.wasPressedThisFrame) SetPaused(!paused);
                if (paused) break;
                sendT -= Time.deltaTime;
                if (sendT <= 0f) { sendT = 0.2f; SendPlayState(); }
                if (allDeadAt < 0f)
                {
                    bool all = worlds.Count > 0;
                    foreach (var w in worlds) if (w != null && !w.IsDead) all = false;
                    if (all) allDeadAt = Time.time + 2f;
                }
                else if (Time.time >= allDeadAt) ShowResults();
                break;

            case State.Results:
                if (k != null)
                {
                    if (k.enterKey.wasPressedThisFrame) Restart();
                    if (k.lKey.wasPressedThisFrame) AddPlayers();
                    if (k.escapeKey.wasPressedThisFrame) Close();
                }
                break;
        }
    }

    public static readonly string[] TimeNames = { "Day", "Sunset", "Evening", "Night" };

    static readonly string[] ZoneLabels = { "Samarkand", "Bukhara", "Mountains", "Tashkent" };
    public static string ZoneLabel(int i) { return ZoneLabels[i % 4]; }

    static string VoteName(int i, bool zone)
    {
        return i >= 4 ? (zone ? "Mix" : "Auto") : (zone ? ZoneLabels[i] : TimeNames[i]);
    }

    // ---- oldindan ko'rish: qahramon va zona/kun vaqti ranglari ----
    // ---- 3D qahramon ko'rinishi: har bir slot uchun kichik sahna + kamera + RenderTexture ----
    class Preview
    {
        public int character = -99;
        public GameObject stage;
        public RunnerGame builder;
        public Camera cam;
        public RenderTexture rt;
    }
    readonly Preview[] previews = new Preview[PhoneHub.MaxPlayers];
    float previewT;

    void DestroyPreview(int i)
    {
        var pv = previews[i];
        if (pv == null) return;
        if (pv.stage != null) Destroy(pv.stage);
        if (pv.builder != null) Destroy(pv.builder.gameObject);
        if (pv.rt != null) { pv.rt.Release(); Destroy(pv.rt); }
        previews[i] = null;
    }

    void BuildPreview(int i, int character)
    {
        DestroyPreview(i);
        if (character < 0) return;
        var pv = new Preview { character = character };
        pv.stage = new GameObject("HeroPreview " + i);
        pv.stage.transform.position = new Vector3(-2000f - i * 50f, 0f, 0f);
        pv.builder = RunnerGame.CreatePreviewBuilder();
        pv.builder.BuildPreview(character, pv.stage.transform);

        var c = new GameObject("PreviewCam");
        c.transform.SetParent(pv.stage.transform, false);
        c.transform.localPosition = new Vector3(0f, 0.95f, 4.2f);
        c.transform.LookAt(pv.stage.transform.position + new Vector3(0f, 0.88f, 0f));
        pv.cam = c.AddComponent<Camera>();
        pv.cam.fieldOfView = 30f;
        pv.cam.nearClipPlane = 0.3f;
        pv.cam.farClipPlane = 20f;
        pv.cam.clearFlags = CameraClearFlags.SolidColor;
        pv.cam.backgroundColor = new Color(0f, 0f, 0f, 0f);
        pv.cam.allowHDR = false;
        var urp = c.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>();
        urp.renderPostProcessing = false;
        pv.rt = new RenderTexture(240, 400, 16, RenderTextureFormat.ARGB32);
        pv.cam.targetTexture = pv.rt;
        previews[i] = pv;
    }

    // Har kadrda: o'yinchi tanlovi o'zgargan bo'lsa modelni yangilaydi, modelni sekin aylantiradi
    void UpdatePreviews(bool visible)
    {
        previewT += Time.unscaledDeltaTime;
        for (int i = 0; i < previews.Length; i++)
        {
            var p = hub.Slots[i];
            int want = (p == null || !visible) ? -1 : p.character;
            int have = previews[i] != null ? previews[i].character : -1;
            if (want != have) { if (want < 0) DestroyPreview(i); else BuildPreview(i, want); }
            if (previews[i] != null) previews[i].builder.PreviewPose(previewT);
        }
    }

    // Har bir qahramonning 3D modelini bir marta rasmga olamiz (PNG): telefonga yuborish engil
    void CaptureHeroImages()
    {
        const int W = 180, H = 300;
        int n = RunnerGame.StyleCount;
        var msgs = new string[n];
        for (int i = 0; i < n; i++)
        {
            var stage = new GameObject("HeroCapture");
            stage.transform.position = new Vector3(-3000f, 0f, 0f);
            var builder = RunnerGame.CreatePreviewBuilder();
            builder.BuildPreview(i, stage.transform);

            var cgo = new GameObject("CaptureCam");
            cgo.transform.SetParent(stage.transform, false);
            cgo.transform.localPosition = new Vector3(0f, 0.95f, 4.2f);
            cgo.transform.LookAt(stage.transform.position + new Vector3(0f, 0.88f, 0f));
            var cam = cgo.AddComponent<Camera>();
            cam.fieldOfView = 30f;
            cam.nearClipPlane = 0.3f;
            cam.farClipPlane = 20f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.16f, 0.1f, 0.3f, 1f);
            cam.allowHDR = false;
            cgo.AddComponent<UnityEngine.Rendering.Universal.UniversalAdditionalCameraData>().renderPostProcessing = false;
            var rt = new RenderTexture(W, H, 16, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            cam.Render();

            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(W, H, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, W, H), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            msgs[i] = "{\"t\":\"img\",\"i\":" + i + ",\"d\":\"" + System.Convert.ToBase64String(tex.EncodeToPNG()) + "\"}";

            cam.targetTexture = null;
            rt.Release();
            Destroy(rt);
            Destroy(tex);
            Destroy(builder.gameObject);
            Destroy(stage);
        }
        CharacterCatalog.ImageMsgs = msgs;
    }

    static Texture2D circleTex;
    readonly Dictionary<string, Texture2D> grads = new Dictionary<string, Texture2D>();

    static Texture2D Circle()
    {
        if (circleTex != null) return circleTex;
        const int n = 64;
        circleTex = new Texture2D(n, n, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < n; y++)
            for (int x = 0; x < n; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(n / 2f, n / 2f));
                circleTex.SetPixel(x, y, new Color(1, 1, 1, Mathf.Clamp01(n / 2f - d)));
            }
        circleTex.Apply();
        return circleTex;
    }

    static void Disc(Rect r, Color c)
    {
        var old = GUI.color;
        GUI.color = c;
        GUI.DrawTexture(r, Circle());
        GUI.color = old;
    }

    Texture2D Grad(Color a, Color b, bool vertical)
    {
        string key = ColorUtility.ToHtmlStringRGB(a) + ColorUtility.ToHtmlStringRGB(b) + (vertical ? "v" : "h");
        Texture2D t;
        if (grads.TryGetValue(key, out t) && t != null) return t;
        const int n = 32;
        t = vertical ? new Texture2D(1, n, TextureFormat.RGBA32, false) : new Texture2D(n, 1, TextureFormat.RGBA32, false);
        t.wrapMode = TextureWrapMode.Clamp;
        for (int i = 0; i < n; i++)
        {
            Color c = vertical ? Color.Lerp(b, a, i / (float)(n - 1)) : Color.Lerp(a, b, i / (float)(n - 1));
            if (vertical) t.SetPixel(0, i, c); else t.SetPixel(i, 0, c);
        }
        t.Apply();
        grads[key] = t;
        return t;
    }

    void Tile(Rect r, string text, Color a, Color b, bool vertical)
    {
        GUI.DrawTexture(r, Grad(a, b, vertical));
        var sh = Label(26, new Color(0, 0, 0, 0.7f), TextAnchor.MiddleCenter);
        GUI.Label(new Rect(r.x + 2, r.y + 2, r.width, r.height), text, sh);
        GUI.Label(r, text, Label(26, Color.white, TextAnchor.MiddleCenter));
    }

    void ZoneTile(Rect r, int v)
    {
        if (v >= 4) Tile(r, "Mix", new Color(0.49f, 0.34f, 0.76f), new Color(0.15f, 0.65f, 0.6f), false);
        else Tile(r, ZoneLabels[v], RunnerGame.ZoneAccent(v), RunnerGame.ZoneGround(v), false);
    }

    void TimeTile(Rect r, int v)
    {
        if (v >= 4) Tile(r, "Auto", new Color(0.49f, 0.34f, 0.76f), new Color(1f, 0.72f, 0.3f), true);
        else Tile(r, TimeNames[v], RunnerGame.TimeTop(v), RunnerGame.TimeHorizon(v), true);
    }

    // Qahramonning soddalashtirilgan rasmi (kostyum, soch/do'ppi, ko'zoynak)
    static void DrawAvatar(Rect b, int character)
    {
        if (character < 0 || character >= RunnerGame.StyleCount)
        {
            Disc(new Rect(b.x + b.width / 2 - 30, b.y + 40, 60, 60), new Color(1, 1, 1, 0.15f));
            GUI.Label(b, "?", new GUIStyle(GUI.skin.label) { fontSize = 70, fontStyle = FontStyle.Bold, alignment = TextAnchor.MiddleCenter, normal = { textColor = new Color(1, 1, 1, 0.4f) } });
            return;
        }
        var l = RunnerGame.StyleLook(character);
        float cx = b.x + b.width / 2f, y0 = b.y;
        Fill(new Rect(cx - 28, y0 + 134, 26, 46), l.pants);                  // oyoqlar
        Fill(new Rect(cx + 2, y0 + 134, 26, 46), l.pants);
        Fill(new Rect(cx - 31, y0 + 176, 31, 14), l.shoe);                   // krossovka
        Fill(new Rect(cx, y0 + 176, 31, 14), l.shoe);
        Fill(new Rect(cx - 44, y0 + 74, 14, 52), l.top);                     // qo'llar
        Fill(new Rect(cx + 30, y0 + 74, 14, 52), l.top);
        Disc(new Rect(cx - 45, y0 + 122, 16, 16), l.skin);
        Disc(new Rect(cx + 29, y0 + 122, 16, 16), l.skin);
        Fill(new Rect(cx - 30, y0 + 70, 60, 68), l.top);                     // tana
        Fill(new Rect(cx - 30, y0 + 124, 60, 8), l.accent);                  // etak chizig'i
        if (l.ponytail) Disc(new Rect(cx - 48, y0 + 26, 28, 28), l.hair);
        Color cap = l.head == 2 ? l.hair : l.hat;
        Disc(new Rect(cx - 33, y0, 66, 60), cap);                            // soch / do'ppi
        Disc(new Rect(cx - 28, y0 + 16, 56, 54), l.skin);                    // yuz
        if (l.head == 2) Fill(new Rect(cx - 29, y0 + 22, 58, 8), l.hat);     // lenta
        if (l.head == 1) Fill(new Rect(cx + 6, y0 + 24, 40, 8), l.hat * 0.8f); // beysbolka soyaboni
        if (l.glasses)
        {
            Fill(new Rect(cx - 21, y0 + 38, 17, 11), new Color(0.05f, 0.05f, 0.08f, 0.85f));
            Fill(new Rect(cx + 4, y0 + 38, 17, 11), new Color(0.05f, 0.05f, 0.08f, 0.85f));
        }
    }

    int resZone = -1, resTime = -1;
    string resText = "";

    // Sanoq boshlanganda ovozlar bir marta hisoblanadi: natija telefonda ham, kompyuterda ham ko'rsatiladi va raundda ishlatiladi
    void ResolveVotes()
    {
        string a, b;
        var ps = Players();
        resZone = Tally(ps, true, out a);
        resTime = Tally(ps, false, out b);
        resText = "Zone: " + VoteName(resZone < 0 ? 4 : resZone, true) + "  ·  Time: " + VoteName(resTime < 0 ? 4 : resTime, false);
    }

    // Ko'pchilik ovozi (teng bo'lsa tasodifiy). 4 = Mix/Auto = -1 (har 400 metrda almashadi)
    static int Tally(List<PhonePlayer> ps, bool zone, out string text)
    {
        var votes = new int[5];
        foreach (var p in ps) votes[Mathf.Clamp(zone ? p.voteZone : p.voteTime, 0, 4)]++;
        int best = 0;
        foreach (int v in votes) best = Mathf.Max(best, v);
        var tied = new List<int>();
        for (int i = 0; i < 5; i++) if (votes[i] == best) tied.Add(i);
        int win = tied[Random.Range(0, tied.Count)];
        var sb = new StringBuilder();
        for (int i = 0; i < 5; i++)
        {
            if (votes[i] == 0) continue;
            string n = i == 4 ? (zone ? "Mix" : "Auto") : (zone ? RunnerGame.ZoneName(i) : TimeNames[i]);
            if (sb.Length > 0) sb.Append("  ");
            sb.Append(n).Append(' ').Append(votes[i]);
        }
        text = sb.ToString();
        return win == 4 ? -1 : win;
    }

    void StartRound()
    {
        RunnerGame.ForcedZone = resZone;
        RunnerGame.ForcedTime = resTime;
        hub.AcceptNewPlayers = false;
        roundPlayers.Clear();
        roundPlayers.AddRange(Players());
        ClearWorlds();
        lobbyCam.enabled = false;
        int n = roundPlayers.Count;
        for (int i = 0; i < n; i++)
        {
            var g = new GameObject("RunnerWorld " + i).AddComponent<RunnerGame>();
            g.slot = i;
            g.worldCount = n;
            g.phone = roundPlayers[i];
            g.OnDied = OnWorldDied;
            g.OnSfx = SendSfx;
            worlds.Add(g);
        }
        allDeadAt = -1f;
        paused = false;
        sendT = 0f;
        state = State.Playing;
    }

    void SendSfx(RunnerGame g, string name, float pitch)
    {
        hub.SendTo(g.phone, "{\"t\":\"sfx\",\"n\":\"" + name + "\",\"p\":" + pitch.ToString("F2", System.Globalization.CultureInfo.InvariantCulture) + "}");
    }

    void OnWorldDied(RunnerGame g)
    {
        hub.SendTo(g.phone, "{\"t\":\"hit\"}");
        SendOne(g, true);
    }

    void SendPlayState()
    {
        foreach (var w in worlds) if (w != null) SendOne(w, w.IsDead);
    }

    void SendOne(RunnerGame w, bool over)
    {
        if (w.phone == null) return;
        hub.SendTo(w.phone, "{\"t\":\"play\",\"stage\":" + w.Stage + ",\"prog\":"
            + w.StageProgress.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)
            + ",\"score\":" + w.ScoreValue + ",\"over\":" + (over ? "true" : "false") + "}");
    }

    void SetPaused(bool on)
    {
        paused = on;
        Time.timeScale = on ? 0f : 1f;
        hub.SendToAll("{\"t\":\"pause\",\"on\":" + (on ? "true" : "false") + "}");
    }

    void ShowResults()
    {
        state = State.Results;
        var idx = new List<int>();
        for (int i = 0; i < worlds.Count; i++) idx.Add(i);
        idx.Sort((a, b) => worlds[b].ScoreValue.CompareTo(worlds[a].ScoreValue));
        resultOrder = idx.ToArray();

        var list = new StringBuilder("[");
        for (int r = 0; r < resultOrder.Length; r++)
        {
            var w = worlds[resultOrder[r]];
            if (r > 0) list.Append(',');
            list.Append("{\"slot\":").Append(w.phone.slot).Append(",\"name\":").Append(PhoneHub.Quote(w.PlayerName))
                .Append(",\"score\":").Append(w.ScoreValue).Append('}');
        }
        list.Append(']');
        for (int r = 0; r < resultOrder.Length; r++)
        {
            var w = worlds[resultOrder[r]];
            w.phone.wantsAgain = false;
            hub.SendTo(w.phone, "{\"t\":\"results\",\"rank\":" + (r + 1) + ",\"list\":" + list + "}");
        }
    }

    void ClearWorlds()
    {
        Time.timeScale = 1f;
        paused = false;
        foreach (var w in worlds) if (w != null) Destroy(w.gameObject);
        worlds.Clear();
    }

    // Shu tarkib bilan yangi raund (3-2-1 dan keyin)
    void Restart()
    {
        ClearWorlds();
        foreach (var p in Players())
            if (!p.connected && !p.isKeyboard) hub.RemovePlayer(p);
        foreach (var p in Players()) p.wantsAgain = false;
        lobbyCam.enabled = true;
        hub.AcceptNewPlayers = true;
        if (Players().Count == 0) { state = State.Lobby; return; }
        state = State.Countdown;
        countT = 3.99f;
        lastCount = -1;
        ResolveVotes();
    }

    // Lobbiga qaytish: o'yinchilar joyida, "tayyor" bayroqlari tiklanadi
    void AddPlayers()
    {
        ClearWorlds();
        lobbyCam.enabled = true;
        hub.AcceptNewPlayers = true;
        foreach (var p in Players()) { p.ready = p.isKeyboard; p.wantsAgain = false; }
        state = State.Lobby;
        SendLobbyToAll();
    }

    void Close()
    {
        RunnerGame.ForcedZone = RunnerGame.ForcedTime = -1;
        ClearWorlds();
        if (solo != null) solo.Suspend(false);
        Destroy(gameObject);
    }

    // ---------------- IMGUI: lobbi, sanoq, pauza, natijalar ----------------
    Texture2D Qr(string url)
    {
        if (url == qrUrl && qrTex != null) return qrTex;
        qrUrl = url;
        bool[,] m = RoyalRun.Net.QrCode.Encode(url);
        if (qrTex != null) Destroy(qrTex);
        qrTex = null;
        if (m == null) return null;
        int n = m.GetLength(0), quiet = 4, size = n + quiet * 2;
        var tex = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        var dark = new Color(0.08f, 0.04f, 0.16f);
        for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                int mx = x - quiet, my = size - 1 - y - quiet;
                bool d = mx >= 0 && my >= 0 && mx < n && my < n && m[my, mx];
                tex.SetPixel(x, y, d ? dark : Color.white);
            }
        tex.Apply();
        qrTex = tex;
        return tex;
    }

    GUIStyle Label(int size, Color col, TextAnchor a = TextAnchor.MiddleLeft)
    {
        var s = new GUIStyle(GUI.skin.label) { fontSize = size, fontStyle = FontStyle.Bold, alignment = a, wordWrap = true };
        s.normal.textColor = col;
        return s;
    }

    static void Fill(Rect r, Color c)
    {
        var old = GUI.color;
        GUI.color = c;
        GUI.DrawTexture(r, Texture2D.whiteTexture);
        GUI.color = old;
    }

    bool Btn(Rect r, string text)
    {
        Fill(r, new Color(1f, 0.78f, 0.18f));
        GUI.Label(r, text, Label(30, new Color(0.1f, 0.06f, 0.2f), TextAnchor.MiddleCenter));
        var e = Event.current;
        return e.type == EventType.MouseDown && r.Contains(e.mousePosition) && Consume();
    }

    static bool Consume() { Event.current.Use(); return true; }

    void OnGUI()
    {
        float sc = Screen.height / 1080f;
        GUI.matrix = Matrix4x4.Scale(new Vector3(sc, sc, 1f));
        float W = Screen.width / sc, H = 1080f;
        var gold = new Color(1f, 0.82f, 0.28f);

        if (state == State.Lobby || state == State.Countdown)
        {
            GUI.Label(new Rect(0, 30, W, 110), "MULTIPLAYER", Label(96, gold, TextAnchor.MiddleCenter));
            string url = hub.ServerOk ? hub.CurrentUrl : "";
            GUI.Label(new Rect(60, 170, W * 0.5f, 330),
                "1. Connect phones to the same Wi-Fi\n2. Scan the QR code\n3. Enter a name, pick a hero, tap READY\n\n"
                + (hub.ServerOk ? url : "Server could not start!"),
                Label(40, Color.white));
            var qt = hub.ServerOk ? Qr(url) : null;
            if (qt != null)
            {
                Fill(new Rect(W - 560, 160, 440, 440), Color.white);
                GUI.DrawTexture(new Rect(W - 550, 170, 420, 420), qt);
            }

            float cw = (W - 160) / 3f;
            var all = Players();
            for (int i = 0; i < PhoneHub.MaxPlayers; i++)
            {
                var r = new Rect(60 + i * (cw + 20), 640, cw, 250);
                Fill(r, new Color(0f, 0f, 0f, 0.4f));
                Fill(new Rect(r.x, r.y, 14, r.height), PhoneHub.SlotColors[i]);
                var p = hub.Slots[i];
                if (p == null)
                {
                    GUI.Label(new Rect(r.x + 30, r.y, r.width - 40, r.height), "Waiting for player " + (i + 1) + "...", Label(34, new Color(1, 1, 1, 0.45f)));
                    continue;
                }
                string hero = p.character >= 0 ? CharacterCatalog.All[p.character].name : "choosing a hero...";
                var pvw = previews[i];
                if (pvw != null && pvw.rt != null) GUI.DrawTexture(new Rect(r.x + 20, r.y + 10, 138, 230), pvw.rt, ScaleMode.ScaleToFit, true);
                else DrawAvatar(new Rect(r.x + 36, r.y + 24, 110, 200), p.character);
                float tx = r.x + 170, tw = r.width - 185;
                GUI.Label(new Rect(tx, r.y + 6, tw, 56), p.name + (p.isKeyboard ? " (keyboard)" : ""), Label(42, PhoneHub.SlotColors[i]));
                GUI.Label(new Rect(tx, r.y + 58, tw, 44), hero, Label(32, Color.white));
                float hw = (tw - 10) / 2f;
                ZoneTile(new Rect(tx, r.y + 108, hw, 56), p.voteZone);
                TimeTile(new Rect(tx + hw + 10, r.y + 108, hw, 56), p.voteTime);
                GUI.Label(new Rect(tx, r.y + 178, tw, 60), !p.connected ? "disconnected" : p.ready ? "READY" : "not ready",
                    Label(40, p.ready ? new Color(0.4f, 0.9f, 0.45f) : new Color(1f, 0.8f, 0.3f)));
            }

            {
                string tz, tt;
                var ps = Players();
                if (ps.Count > 0)
                {
                    Tally(ps, true, out tz); Tally(ps, false, out tt);
                    GUI.Label(new Rect(60, 900, W - 120, 50), "ZONE votes: " + (tz.Length > 0 ? tz : "-") + "      TIME votes: " + (tt.Length > 0 ? tt : "-"), Label(32, gold, TextAnchor.MiddleCenter));
                }
            }
            GUI.Label(new Rect(0, 950, W, 60), "Esc: back    Tab: next address    K: add keyboard player    Z / T: keyboard player votes zone / time", Label(30, new Color(1, 1, 1, 0.6f), TextAnchor.MiddleCenter));
            if (state == State.Countdown)
            {
                Fill(new Rect(0, 0, W, H), new Color(0, 0, 0, 0.55f));
                GUI.Label(new Rect(0, 0, W, H), lastCount > 0 ? lastCount.ToString() : "GO!", Label(300, gold, TextAnchor.MiddleCenter));
                GUI.Label(new Rect(0, H - 260, W, 70), resText, Label(52, Color.white, TextAnchor.MiddleCenter));
            }
        }
        else if (state == State.Playing && paused)
        {
            Fill(new Rect(0, 0, W, H), new Color(0, 0, 0, 0.7f));
            GUI.Label(new Rect(0, 250, W, 140), "PAUSED", Label(110, gold, TextAnchor.MiddleCenter));
            if (Btn(new Rect(W / 2 - 250, 450, 500, 90), "RESUME (Esc)")) SetPaused(false);
            if (Btn(new Rect(W / 2 - 250, 570, 500, 90), "RESTART")) { SetPaused(false); Restart(); }
            if (Btn(new Rect(W / 2 - 250, 690, 500, 90), "MAIN MENU")) Close();
        }
        else if (state == State.Results)
        {
            Fill(new Rect(0, 0, W, H), new Color(0.1f, 0.06f, 0.2f, 0.94f));
            GUI.Label(new Rect(0, 60, W, 120), "RESULTS", Label(96, gold, TextAnchor.MiddleCenter));
            Color[] medal = { gold, new Color(0.8f, 0.82f, 0.88f), new Color(0.8f, 0.55f, 0.3f) };
            for (int r = 0; r < resultOrder.Length; r++)
            {
                var w = worlds[resultOrder[r]];
                var row = new Rect(W / 2 - 480, 220 + r * 130, 960, 110);
                Fill(row, new Color(1, 1, 1, 0.08f));
                Fill(new Rect(row.x, row.y, 14, row.height), PhoneHub.SlotColors[w.phone.slot]);
                GUI.Label(new Rect(row.x + 30, row.y, 90, row.height), (r + 1) + ".", Label(60, medal[Mathf.Min(r, 2)]));
                GUI.Label(new Rect(row.x + 130, row.y, 450, row.height), w.PlayerName + "  ·  " + CharacterCatalog.All[Mathf.Clamp(w.phone.character, 0, CharacterCatalog.Count - 1)].name, Label(38, Color.white));
                GUI.Label(new Rect(row.x + 560, row.y, 380, row.height), w.ScoreValue.ToString(), Label(60, gold, TextAnchor.MiddleRight));
            }
            float by = 220 + resultOrder.Length * 130 + 40;
            if (Btn(new Rect(W / 2 - 480, by, 300, 90), "RESTART (Enter)")) Restart();
            if (Btn(new Rect(W / 2 - 150, by, 300, 90), "ADD PLAYERS (L)")) AddPlayers();
            if (Btn(new Rect(W / 2 + 180, by, 300, 90), "MENU (Esc)")) Close();
        }
    }
}
