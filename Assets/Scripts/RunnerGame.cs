using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

// Royal Run: 3 qatorli cheksiz yugurish o'yini.
// Bu skript hamma narsani (yo'l, o'yinchi, to'siqlar, tangalar, kamera, UI) kod orqali o'zi yaratadi.
// Sahnaga hech narsa qo'shish shart emas: Play bosilganda o'yin boshlanadi.
//
// Boshqaruv: A/D yoki <-/-> = qator almashtirish, Space/W/Up = sakrash, S/Down = sirg'anish.
// Telefon/sichqoncha: surish (swipe) chapga/o'ngga/yuqoriga/pastga.
public class RunnerGame : MonoBehaviour
{
    const float LaneW = 2.5f;
    const float SegLen = 24f;
    const int SegCount = 7;
    const float StartSpeed = 12f, MaxSpeed = 42f, Accel = 0.25f;
    const float StageLen = 400f; // har 400 metrda yangi bosqich
    const float Gravity = 28f, JumpSpeed = 10f, SlideTime = 0.7f;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    static void Boot()
    {
        // sahna qayta yuklanganda (Restart) o'yin yana yaratilishi uchun
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;
        Spawn();
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode) { Spawn(); }

    static void Spawn()
    {
        if (FindAnyObjectByType<RunnerGame>() == null)
            new GameObject("RunnerGame").AddComponent<RunnerGame>();
    }

    class Obs { public Vector3 c, h; }
    class Coin { public Transform t; public Vector3 c; public bool taken; }
    class Seg
    {
        public GameObject root;
        public float z;
        public List<Obs> obs = new List<Obs>();
        public List<Coin> coins = new List<Coin>();
        public List<Mesh> meshes = new List<Mesh>();
    }

    enum ObsType { Low, Tall, Overhead }

    // holat
    bool dead;
    float deadTime;
    int lane = 1;
    float px, py, vy, z, speed = StartSpeed, slideT;
    bool sliding;
    int coinCount;
    float best;
    float nextSegZ;

    // reklama
    static int runsSinceAd;        // oxirgi to'liq ekran reklamadan beri necha o'yin
    static float lastAdTime = -999f;
    const int AdEveryRuns = 3;     // har 3 o'yindan keyin
    const float AdMinGap = 90f;    // reklamalar orasida kamida 90 soniya

    // bosqichlar
    int stage = 1;
    float bannerT;
    Text bannerTxt;
    Light sun;
    static readonly Color[] SkyColors =
    {
        new Color(0.5f, 0.75f, 0.97f),   // kunduz
        new Color(0.98f, 0.68f, 0.48f),  // quyosh botishi
        new Color(0.5f, 0.38f, 0.65f),   // shom
        new Color(0.1f, 0.13f, 0.28f),   // tun
    };
    static readonly Color[] SunColors =
    {
        new Color(1f, 0.96f, 0.88f), new Color(1f, 0.7f, 0.45f), new Color(0.7f, 0.6f, 0.9f), new Color(0.5f, 0.6f, 0.9f),
    };

    // ovoz
    AudioSource sfx, coinSrc, stepSrc, music;
    AudioClip cCoin, cJump, cSlide, cHit, cLane, cStep, cLevel, cClick, cRevive;
    bool muted;
    float prevSn, coinTimer;
    int coinCombo;
    GameObject muteBtn;
    Text muteLbl;

    // bosh menyu va personaj tanlash
    static bool skipMenu;          // "Qayta boshlash" menyusiz to'g'ridan-to'g'ri o'yinga o'tadi
    bool menu = true;
    float menuT;
    int styleIndex;
    GameObject menuPanel;
    Text nameTxt, menuBestTxt;

    class Style
    {
        public string name;
        public Color top, pants, shoe, hair, skin, accent, bag, hat;
        public int head;            // 0 = do'ppi, 1 = beysbolka, 2 = lenta
        public bool ponytail, glasses, pattern;
    }

    static readonly Style[] Styles =
    {
        new Style { name = "Alisher", top = new Color(0.93f, 0.9f, 0.84f), pants = new Color(0.1f, 0.12f, 0.25f), shoe = new Color(0.97f, 0.97f, 0.97f),
            hair = new Color(0.16f, 0.1f, 0.07f), skin = new Color(0.96f, 0.77f, 0.62f), accent = new Color(0.12f, 0.36f, 0.78f),
            bag = new Color(0.12f, 0.15f, 0.3f), hat = new Color(0.06f, 0.06f, 0.08f), head = 0 },
        new Style { name = "Madina", top = new Color(0.72f, 0.14f, 0.18f), pants = new Color(0.3f, 0.5f, 0.8f), shoe = new Color(0.85f, 0.85f, 0.9f),
            hair = new Color(0.25f, 0.14f, 0.08f), skin = new Color(0.93f, 0.74f, 0.6f), accent = new Color(0.97f, 0.97f, 0.97f),
            bag = new Color(0.5f, 0.12f, 0.15f), hat = new Color(0.9f, 0.25f, 0.25f), head = 2, ponytail = true, pattern = true },
        new Style { name = "Sardor", top = new Color(0.35f, 0.55f, 0.25f), pants = new Color(0.1f, 0.1f, 0.14f), shoe = new Color(0.97f, 0.97f, 0.97f),
            hair = new Color(0.1f, 0.07f, 0.05f), skin = new Color(0.9f, 0.68f, 0.52f), accent = new Color(0.97f, 0.97f, 0.97f),
            bag = new Color(0.15f, 0.15f, 0.18f), hat = new Color(0.3f, 0.5f, 0.22f), head = 1, glasses = true },
    };
    int segIndex;

    // obyektlar
    Transform player, body, upper, legL, legR, kneeL, kneeR, armL, armR, elbowL, elbowR;
    float runT;
    Camera cam;
    readonly List<Seg> segs = new List<Seg>();
    readonly Dictionary<Color, Material> mats = new Dictionary<Color, Material>();
    Text scoreTxt, coinTxt, hintTxt, dlgTxt, adTxt, dlgNote;
    GameObject dialog, adPanel, contBtn;
    Renderer[] bodyRenderers;
    Material[] origMats;
    bool revived;      // bir o'yinda bir marta davom etish mumkin
    float adT;         // reklama taymeri (soniya)
    float invT;        // o'lmas vaqt (davom etgandan keyin)

    // swipe
    Vector2 swipeStart;
    bool swiping;

    void Start()
    {
        Application.targetFrameRate = 60;
        QualitySettings.vSyncCount = 0;
        Screen.sleepTimeout = SleepTimeout.NeverSleep;
        Screen.orientation = ScreenOrientation.Portrait;
        best = PlayerPrefs.GetFloat("RoyalRunBest", 0);

        cam = Camera.main;
        if (cam == null)
        {
            var c = new GameObject("Main Camera");
            c.tag = "MainCamera";
            cam = c.AddComponent<Camera>();
            c.AddComponent<AudioListener>();
        }
        cam.clearFlags = CameraClearFlags.SolidColor;
        cam.backgroundColor = new Color(0.5f, 0.75f, 0.97f);
        cam.farClipPlane = 250f;
        cam.fieldOfView = 60f;

        if (FindAnyObjectByType<Light>() == null)
        {
            var l = new GameObject("Sun").AddComponent<Light>();
            l.type = LightType.Directional;
            l.transform.rotation = Quaternion.Euler(50, -30, 0);
        }

        // o'yinchi
        player = new GameObject("Player").transform;
        body = new GameObject("Model").transform;
        body.SetParent(player, false);
        styleIndex = Mathf.Clamp(PlayerPrefs.GetInt("RoyalRunStyle", 0), 0, Styles.Length - 1);
        BuildCharacter(body);
        bodyRenderers = body.GetComponentsInChildren<Renderer>();

        sun = FindAnyObjectByType<Light>();
        BuildAudio();
        AdService.I.Init();
        BuildUI();
        nextSegZ = -SegLen; // o'yinchi orqasida ham yo'l bo'lsin (menyu uchun)
        for (int i = 0; i <= SegCount; i++) SpawnSeg();

        menu = !skipMenu;
        skipMenu = false;
        if (menu) { SetHud(false); MenuCamera(true); }
        else { menuPanel.SetActive(false); SetHud(true); UpdateCamera(true); AdService.I.ShowBanner(true); }
    }

    void Update()
    {
        float dt = Time.deltaTime;

        AdService.I.Tick(Time.unscaledDeltaTime);

        if (menu) { MenuUpdate(dt); return; }

        if (dead)
        {
            deadTime += dt;
            if (adT > 0f)
            {
                adT -= dt;
                adTxt.text = "REKLAMA\n(sinov rejimi)\n\n" + Mathf.Max(1, Mathf.CeilToInt(adT));
                if (adT <= 0f) { adPanel.SetActive(false); Revive(); }
            }
            else if (Keyboard.current != null) // kompyuterda sinash uchun: R = qayta, C = davom
            {
                if (Keyboard.current.rKey.wasPressedThisFrame) Restart();
                if (Keyboard.current.cKey.wasPressedThisFrame && !revived) StartAd();
            }
            return;
        }

        HandleInput();

        // tezlik bosqichga qarab oshadi
        float targetSpeed = Mathf.Min(MaxSpeed, StartSpeed + (stage - 1) * 4f + ((z % StageLen) / StageLen) * 2f);
        speed = Mathf.MoveTowards(speed, targetSpeed, 4f * dt);
        z += speed * dt;
        scoreF += speed * dt * stage;
        cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, 60f + (speed - StartSpeed) * 0.4f, 2f * dt);

        int newStage = 1 + (int)(z / StageLen);
        if (newStage > stage) OnStage(newStage);
        UpdateStageVisuals(dt);

        px = Mathf.MoveTowards(px, (lane - 1) * LaneW, 22f * dt);

        if (py > 0f || vy > 0f)
        {
            vy -= Gravity * dt;
            py += vy * dt;
            if (py <= 0f) { py = 0f; vy = 0f; }
        }
        if (sliding)
        {
            slideT -= dt;
            if (slideT <= 0f) sliding = false;
        }

        float h = sliding ? 0.9f : 1.8f;
        player.position = new Vector3(px, py, z);
        body.localScale = new Vector3(0.9f, 0.9f * h / 1.8f, 0.9f);

        // yugurish animatsiyasi
        float dx = (lane - 1) * LaneW - px;
        float thL, thR, knL, knR, shL, shR, elb = 70f, bob = 0f, lean = 12f;
        if (py >= 0.05f) // havoda
        {
            thL = -50f; knL = 70f; thR = 20f; knR = 40f; shL = -130f; shR = -130f; elb = 25f; lean = 5f;
        }
        else if (sliding)
        {
            thL = -30f; knL = 20f; thR = -10f; knR = 80f; shL = -60f; shR = -60f; elb = 40f; lean = -35f;
        }
        else
        {
            runT += dt * speed * 0.55f;
            float sn = Mathf.Sin(runT), cs = Mathf.Cos(runT);
            if (sn * prevSn < 0f) stepSrc.PlayOneShot(cStep, 0.5f);
            prevSn = sn;
            thL = sn * 50f; thR = -sn * 50f;
            knL = Mathf.Max(0f, -cs) * 70f; knR = Mathf.Max(0f, cs) * 70f;
            shL = -sn * 55f; shR = sn * 55f;
            bob = Mathf.Abs(cs) * 0.07f;
        }
        body.localPosition = new Vector3(0, bob, 0);
        upper.localRotation = Quaternion.Euler(lean, 0, -dx * 6f);
        legL.localRotation = Quaternion.Euler(thL, 0, 0);
        legR.localRotation = Quaternion.Euler(thR, 0, 0);
        kneeL.localRotation = Quaternion.Euler(knL, 0, 0);
        kneeR.localRotation = Quaternion.Euler(knR, 0, 0);
        armL.localRotation = Quaternion.Euler(shL, 0, 0);
        armR.localRotation = Quaternion.Euler(shR, 0, 0);
        elbowL.localRotation = Quaternion.Euler(-elb, 0, 0);
        elbowR.localRotation = Quaternion.Euler(-elb, 0, 0);

        // eski bo'laklarni olib tashlab, yangisini oldinga qo'shamiz
        while (segs[0].z + SegLen < z - 12f)
        {
            foreach (var mm in segs[0].meshes) Destroy(mm);
            Destroy(segs[0].root);
            segs.RemoveAt(0);
            SpawnSeg();
        }

        // to'qnashuvlar
        Vector3 pc = new Vector3(px, py + h / 2f, z);
        Vector3 ph = new Vector3(0.4f, h / 2f, 0.3f);
        if (invT > 0f)
        {
            invT -= dt;
            bool vis = invT <= 0f || ((int)(invT * 12f)) % 2 == 0;
            foreach (var r in bodyRenderers) r.enabled = vis;
        }
        foreach (var s in segs)
        {
            if (s.z > z + 40f || s.z + SegLen < z - 5f) continue;
            if (invT <= 0f)
            foreach (var o in s.obs)
                if (Overlap(pc, ph, o.c, o.h)) { Die(); return; }
            foreach (var c in s.coins)
            {
                if (c.taken) continue;
                c.t.Rotate(0, 240f * dt, 0);
                if (Overlap(pc, ph, c.c, new Vector3(0.35f, 0.35f, 0.35f)))
                {
                    c.taken = true;
                    c.t.gameObject.SetActive(false);
                    coinCount++;
                    scoreF += 10f * stage;
                    coinCombo = coinTimer > 0f ? coinCombo + 1 : 0;
                    coinTimer = 0.8f;
                    coinSrc.pitch = Mathf.Min(1.7f, 1f + coinCombo * 0.06f);
                    coinSrc.PlayOneShot(cCoin, 0.7f);
                }
            }
        }

        if (hintTxt != null) { var hc = hintTxt.color; hc.a = Mathf.Clamp01(1f - (z - 30f) / 30f); hintTxt.color = hc; }
        if (coinTimer > 0f) coinTimer -= dt;
        scoreTxt.text = CurrentScore().ToString("00000");
        coinTxt.text = coinCount.ToString();
        multTxt.text = "x" + stage;
        stageTxt.text = "BOSQICH " + stage;
        UpdateCamera(false);
    }

    float scoreF; // ochko: masofa * bosqich ko'paytmasi + tangalar
    int CurrentScore() { return Mathf.FloorToInt(scoreF); }

    static bool Overlap(Vector3 ac, Vector3 ah, Vector3 bc, Vector3 bh)
    {
        return Mathf.Abs(ac.x - bc.x) < ah.x + bh.x &&
               Mathf.Abs(ac.y - bc.y) < ah.y + bh.y &&
               Mathf.Abs(ac.z - bc.z) < ah.z + bh.z;
    }

    void Die()
    {
        dead = true;
        deadTime = 0f;
        int sc = CurrentScore();
        if (sc > best) { best = sc; PlayerPrefs.SetFloat("RoyalRunBest", best); PlayerPrefs.Save(); }

        origMats = new Material[bodyRenderers.Length];
        var red = Mat(new Color(0.9f, 0.3f, 0.3f));
        for (int i = 0; i < bodyRenderers.Length; i++) { origMats[i] = bodyRenderers[i].sharedMaterial; bodyRenderers[i].sharedMaterial = red; bodyRenderers[i].enabled = true; }

        runsSinceAd++;
        AdService.I.ShowBanner(false);
        dlgNote.text = "";
        Sfx(cHit, 1f);
        music.volume = 0.12f;
        dlgTxt.text = "O'YIN TUGADI\n\nOchko: " + sc + "\nRekord: " + (int)best + "\nTanga: " + coinCount;
        contBtn.SetActive(!revived);
        dialog.SetActive(true);
    }

    void Restart()
    {
        skipMenu = true;
        LeaveWithAd();
    }

    void GoMenu()
    {
        skipMenu = false;
        LeaveWithAd();
    }

    // Har 3 o'yindan keyin (kamida 90 soniya o'tgan bo'lsa) to'liq ekran reklama, so'ng sahna qayta yuklanadi
    void LeaveWithAd()
    {
        bool due = runsSinceAd >= AdEveryRuns && Time.realtimeSinceStartup - lastAdTime > AdMinGap;
        if (due && AdService.I.InterstitialReady)
        {
            runsSinceAd = 0;
            lastAdTime = Time.realtimeSinceStartup;
            dialog.SetActive(false);
            AdService.I.ShowInterstitial(ReloadScene);
        }
        else ReloadScene();
    }

    void ReloadScene()
    {
        SceneManager.LoadScene(SceneManager.GetActiveScene().buildIndex);
    }

    // Mukofotli reklama. HOZIR: 5 soniyalik sinov ekrani.
    // Haqiqiy reklama (Unity LevelPlay / AdMob) ulanganda faqat shu funksiyani almashtirasiz:
    // reklama oxirigacha ko'rilsa Revive() ni chaqiring.
    void StartAd()
    {
        if (revived) return;

        if (!AdService.I.Supported) // reklama qo'llab-quvvatlanmasa: 5 soniyalik sinov ekrani
        {
            dialog.SetActive(false);
            adPanel.SetActive(true);
            adT = 5f;
            return;
        }
        if (!AdService.I.RewardedReady)
        {
            dlgNote.text = "Reklama yuklanmadi. " + AdService.I.LastError;
            return;
        }
        dlgNote.text = "";
        dialog.SetActive(false);
        lastAdTime = Time.realtimeSinceStartup;
        AdService.I.ShowRewarded(Revive, () =>
        {
            dialog.SetActive(true);
            dlgNote.text = "Reklama oxirigacha ko'rilmadi";
        });
    }

    // Shu joyning o'zidan davom etish
    void Revive()
    {
        revived = true;
        dead = false;
        invT = 2.5f;
        AdService.I.ShowBanner(true);
        music.volume = 0.35f;
        Sfx(cRevive, 0.9f);
        for (int i = 0; i < bodyRenderers.Length; i++) bodyRenderers[i].sharedMaterial = origMats[i];
        foreach (var s in segs) s.obs.RemoveAll(o => o.c.z > z - 6f && o.c.z < z + 30f); // oldidagi to'siqlar o'tkazib yuboriladi
        py = 0f; vy = 0f; sliding = false;
        speed = Mathf.Max(StartSpeed, speed * 0.75f);
        dialog.SetActive(false);
    }

    // ---------------- Bosqichlar ----------------
    void OnStage(int n)
    {
        stage = n;
        Sfx(cLevel, 0.9f);
        bannerT = 2.5f;
        bannerTxt.text = "BOSQICH " + n;
        music.pitch = Mathf.Min(1.25f, 1f + (stage - 1) * 0.04f); // musiqa ham tezlashadi
    }

    void UpdateStageVisuals(float dt)
    {
        if (bannerT > 0f)
        {
            bannerT -= dt;
            var c = bannerTxt.color;
            c.a = Mathf.Clamp01(bannerT / 0.8f);
            bannerTxt.color = c;
        }
        int idx = (stage - 1) % SkyColors.Length; // kunduz -> shom -> tun -> yana kunduz
        cam.backgroundColor = Color.Lerp(cam.backgroundColor, SkyColors[idx], 0.6f * dt);
        if (sun != null) sun.color = Color.Lerp(sun.color, SunColors[idx], 0.6f * dt);
    }

    // ---------------- Ovoz (hammasi kod bilan yaratiladi, fayl kerak emas) ----------------
    void BuildAudio()
    {
        muted = PlayerPrefs.GetInt("RoyalRunMute", 0) == 1;
        AudioListener.volume = muted ? 0f : 1f;

        sfx = gameObject.AddComponent<AudioSource>();
        coinSrc = gameObject.AddComponent<AudioSource>();
        stepSrc = gameObject.AddComponent<AudioSource>();
        music = gameObject.AddComponent<AudioSource>();
        music.loop = true;
        music.volume = 0.35f;
        music.clip = MakeMusic();
        music.Play();

        const float TAU = Mathf.PI * 2f;

        cCoin = Synth("coin", 0.3f, t =>
        {
            float f = t < 0.07f ? 988f : 1319f;
            return (Mathf.Sin(TAU * f * t) + 0.3f * Mathf.Sin(2f * TAU * f * t)) * 0.45f * Mathf.Exp(-t * 10f);
        });
        cJump = Synth("jump", 0.3f, t => Mathf.Sin(TAU * (250f * t + 700f * t * t)) * 0.4f * (1f - t / 0.3f));
        cClick = Synth("click", 0.06f, t => Mathf.Sin(TAU * 800f * t) * 0.4f * Mathf.Exp(-t * 50f));
        cStep = Synth("step", 0.1f, t => Mathf.Sin(TAU * 80f * t) * 0.8f * Mathf.Exp(-t * 35f));

        var rnd = new System.Random(7);
        float lp1 = 0f;
        cSlide = Synth("slide", 0.45f, t =>
        {
            float n = (float)(rnd.NextDouble() * 2 - 1);
            lp1 += (n - lp1) * 0.25f;
            return lp1 * 0.9f * Mathf.Sin(Mathf.PI * t / 0.45f);
        });
        float lp2 = 0f;
        cLane = Synth("lane", 0.12f, t =>
        {
            float n = (float)(rnd.NextDouble() * 2 - 1);
            lp2 += (n - lp2) * 0.4f;
            return lp2 * 0.6f * Mathf.Sin(Mathf.PI * t / 0.12f);
        });
        float lp3 = 0f;
        cHit = Synth("hit", 0.6f, t =>
        {
            float n = (float)(rnd.NextDouble() * 2 - 1);
            lp3 += (n - lp3) * 0.15f;
            return (Mathf.Sin(TAU * (110f - 100f * t) * t) * 0.7f + lp3 * 0.6f) * Mathf.Exp(-t * 7f);
        });
        cLevel = Arp(new[] { 523f, 659f, 784f, 1047f }, 0.12f);
        cRevive = Arp(new[] { 392f, 523f, 659f, 784f }, 0.1f);
    }

    static AudioClip Synth(string name, float dur, System.Func<float, float> f)
    {
        int rate = 22050;
        int n = (int)(dur * rate);
        var d = new float[n];
        for (int i = 0; i < n; i++) d[i] = Mathf.Clamp(f(i / (float)rate), -1f, 1f);
        var c = AudioClip.Create(name, n, 1, rate, false);
        c.SetData(d, 0);
        return c;
    }

    static AudioClip Arp(float[] notes, float step)
    {
        return Synth("arp", step * notes.Length + 0.35f, t =>
        {
            int i = Mathf.Min(notes.Length - 1, (int)(t / step));
            float lt = t - i * step;
            float f = notes[i];
            return (Mathf.Sin(Mathf.PI * 2f * f * t) + 0.3f * Mathf.Sin(Mathf.PI * 4f * f * t)) * 0.3f * Mathf.Exp(-lt * 5f);
        });
    }

    // Fon musiqasi: Hijaz maqomida (D) 8 taktlik takrorlanuvchi motiv, bas va baraban bilan
    static AudioClip MakeMusic()
    {
        const int rate = 22050;
        const float bpm = 128f;
        float beat = 60f / bpm, eighth = beat / 2f;
        int n = (int)(32 * eighth * rate);
        var buf = new float[n];

        int[] scale = { 0, 1, 4, 5, 7, 8, 10 };
        int[] mel = { 7, -1, 4, 5, 4, 2, 1, 2, 4, -1, 5, 7, 8, 7, 5, 4,
                      7, -1, 4, 5, 4, 2, 1, 2, 4, 5, 4, 2, 1, 2, 0, -1 };
        int[] bass = { 0, 0, 7, 0, 0, 0, 7, 0, 1, 1, 7, 7, 0, 0, 7, 0 };
        var rnd = new System.Random(3);

        for (int e = 0; e < 32; e++)
        {
            if (mel[e] < 0) continue;
            int semis = scale[mel[e] % 7] + 12 * (mel[e] / 7);
            AddNote(buf, rate, e * eighth, eighth * 1.8f, 293.66f * Mathf.Pow(2f, semis / 12f), 0.15f, true);
        }
        for (int b = 0; b < 16; b++)
        {
            AddNote(buf, rate, b * beat, beat * 0.9f, 73.42f * Mathf.Pow(2f, bass[b] / 12f), 0.24f, false);
            AddKick(buf, rate, b * beat);
            AddHat(buf, rate, b * beat + eighth, rnd);
        }
        for (int i = 0; i < n; i++) buf[i] = Mathf.Clamp(buf[i], -1f, 1f);
        var c = AudioClip.Create("music", n, 1, rate, false);
        c.SetData(buf, 0);
        return c;
    }

    static void AddNote(float[] buf, int rate, float start, float len, float freq, float amp, bool bright)
    {
        int s0 = (int)(start * rate), cnt = (int)(len * rate);
        for (int i = 0; i < cnt; i++)
        {
            float t = i / (float)rate;
            float env = Mathf.Exp(-t * (bright ? 4f : 3f)) * Mathf.Min(1f, t * 200f);
            float ph = Mathf.PI * 2f * freq * t;
            float v = Mathf.Sin(ph);
            if (bright) v += 0.5f * Mathf.Sin(2f * ph) + 0.25f * Mathf.Sin(3f * ph);
            buf[(s0 + i) % buf.Length] += v * amp * env;
        }
    }

    static void AddKick(float[] buf, int rate, float start)
    {
        int s0 = (int)(start * rate), cnt = (int)(0.15f * rate);
        for (int i = 0; i < cnt; i++)
        {
            float t = i / (float)rate;
            float ph = Mathf.PI * 2f * (50f * t + 120f * (1f - Mathf.Exp(-40f * t)) / 40f);
            buf[(s0 + i) % buf.Length] += Mathf.Sin(ph) * Mathf.Exp(-t * 14f) * 0.35f;
        }
    }

    static void AddHat(float[] buf, int rate, float start, System.Random rnd)
    {
        int s0 = (int)(start * rate), cnt = (int)(0.05f * rate);
        for (int i = 0; i < cnt; i++)
        {
            float t = i / (float)rate;
            buf[(s0 + i) % buf.Length] += (float)(rnd.NextDouble() * 2 - 1) * Mathf.Exp(-t * 70f) * 0.07f;
        }
    }

    void Sfx(AudioClip c, float vol)
    {
        if (c != null && sfx != null) sfx.PlayOneShot(c, vol);
    }

    void ToggleMute()
    {
        muted = !muted;
        PlayerPrefs.SetInt("RoyalRunMute", muted ? 1 : 0);
        RefreshMute();
    }

    void RefreshMute()
    {
        AudioListener.volume = muted ? 0f : 1f;
        if (muteLbl != null) muteLbl.text = muted ? "Jim" : "Ovoz";
        if (muteBtn != null) muteBtn.GetComponent<Image>().color = muted ? new Color(0.6f, 0.2f, 0.2f) : new Color(0.15f, 0.55f, 0.25f);
    }

    // ---------------- HUD uslubi: yumaloq to'q kapsulalar, yulduz va tanga belgisi ----------------
    Sprite pillSprite, starSprite, coinSprite;
    GameObject hudRoot;
    Text multTxt, stageTxt;

    void BuildHud(Transform ui)
    {
        pillSprite = MakePillSprite();
        starSprite = MakeStarSprite();
        coinSprite = MakeCoinSprite();

        hudRoot = new GameObject("Hud", typeof(RectTransform));
        hudRoot.transform.SetParent(ui, false);
        var rt = (RectTransform)hudRoot.transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        var hud = hudRoot.transform;

        // ochko kapsulasi: [xN] [yulduz] 01090
        var sp = MakePill(hud, new Vector2(40, -40), new Vector2(560, 120));
        multTxt = MakeText(sp, "Mult", new Vector2(0, 0.5f), new Vector2(30, 0), TextAnchor.MiddleLeft, 44);
        multTxt.color = new Color(1f, 0.85f, 0.25f);
        MakeIcon(sp, starSprite, new Vector2(0, 0.5f), new Vector2(150, 0), 84);
        scoreTxt = MakeText(sp, "Score", new Vector2(1, 0.5f), new Vector2(-30, 0), TextAnchor.MiddleRight, 66);
        scoreTxt.rectTransform.sizeDelta = new Vector2(330, 100);
        scoreTxt.text = "00000";

        // tanga kapsulasi: 12 [tanga] (ochko kapsulasi ostida, o'ng tomonda)
        var cp = MakePill(hud, new Vector2(300, -180), new Vector2(300, 100));
        coinTxt = MakeText(cp, "Coins", new Vector2(0, 0.5f), new Vector2(30, 0), TextAnchor.MiddleLeft, 56);
        coinTxt.rectTransform.sizeDelta = new Vector2(180, 90);
        MakeIcon(cp, coinSprite, new Vector2(1, 0.5f), new Vector2(-55, 0), 70);

        // bosqich kapsulasi
        var gp = MakePill(hud, new Vector2(40, -180), new Vector2(240, 100));
        stageTxt = MakeText(gp, "Stage", new Vector2(0.5f, 0.5f), Vector2.zero, TextAnchor.MiddleCenter, 36);
        stageTxt.rectTransform.sizeDelta = new Vector2(240, 90);
        stageTxt.color = new Color(0.7f, 0.85f, 1f);
    }

    Transform MakePill(Transform parent, Vector2 pos, Vector2 size)
    {
        var go = new GameObject("Pill", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0, 1);
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        var img = go.GetComponent<Image>();
        img.sprite = pillSprite;
        img.type = Image.Type.Sliced;
        img.color = new Color(0.1f, 0.14f, 0.27f, 0.88f);
        img.raycastTarget = false;
        return go.transform;
    }

    void MakeIcon(Transform parent, Sprite sprite, Vector2 anchor, Vector2 pos, float size)
    {
        var go = new GameObject("Icon", typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(size, size);
        var img = go.GetComponent<Image>();
        img.sprite = sprite;
        img.raycastTarget = false;
    }

    // Yumaloq to'rtburchak (9-slice)
    static Sprite MakePillSprite()
    {
        const int N = 64; const float R = 26f;
        var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                float cx = Mathf.Clamp(x + 0.5f, R, N - R), cy = Mathf.Clamp(y + 0.5f, R, N - R);
                float d = Mathf.Sqrt((x + 0.5f - cx) * (x + 0.5f - cx) + (y + 0.5f - cy) * (y + 0.5f - cy));
                float a = Mathf.Clamp01(R - d + 0.5f);
                tex.SetPixel(x, y, new Color(1, 1, 1, a));
            }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect, new Vector4(R, R, R, R));
    }

    static bool InStar(Vector2 p, float outer, float inner)
    {
        // 5 qirrali yulduz: burchakka qarab radius
        float ang = Mathf.Atan2(p.x, p.y);              // tepa = 0
        float seg = Mathf.PI * 2f / 5f;
        float a = Mathf.Repeat(ang + seg / 2f, seg) - seg / 2f; // -seg/2..seg/2, 0 = uchi
        float t = Mathf.Abs(a) / (seg / 2f);            // 0 uchda, 1 chuqurchada
        // uchdan chuqurchaga to'g'ri chiziq (qutb koordinatasida taxminiy)
        Vector2 tip = new Vector2(0, outer);
        float ca = seg / 2f;
        Vector2 valley = new Vector2(Mathf.Sin(ca) * inner, Mathf.Cos(ca) * inner);
        Vector2 q = new Vector2(Mathf.Sin(Mathf.Abs(a)) * p.magnitude, Mathf.Cos(Mathf.Abs(a)) * p.magnitude);
        Vector2 e = valley - tip, w = q - tip;
        float side = e.x * w.y - e.y * w.x;             // qirraning qaysi tomonida
        return side <= 0f && p.magnitude <= outer;
    }

    static Sprite MakeStarSprite()
    {
        const int N = 128;
        var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                Color acc = Color.clear; int hits = 0;
                for (int sy = 0; sy < 3; sy++)
                    for (int sx = 0; sx < 3; sx++)
                    {
                        Vector2 p = new Vector2((x + (sx + 0.5f) / 3f) / N - 0.5f, (y + (sy + 0.5f) / 3f) / N - 0.5f) * 2f; // -1..1
                        p.y -= 0.04f;
                        Color c;
                        if (InStar(p, 0.95f * 0.95f, 0.46f)) c = Color.Lerp(new Color(1f, 0.72f, 0.1f), new Color(1f, 0.93f, 0.4f), (p.y + 1f) / 2f);
                        else if (InStar(p, 1.0f, 0.55f)) c = new Color(0.8f, 0.45f, 0.05f);
                        else continue;
                        acc += c; hits++;
                    }
                tex.SetPixel(x, y, hits == 0 ? Color.clear : new Color(acc.r / hits, acc.g / hits, acc.b / hits, hits / 9f));
            }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100f);
    }

    static Sprite MakeCoinSprite()
    {
        const int N = 128;
        var tex = new Texture2D(N, N, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp };
        for (int y = 0; y < N; y++)
            for (int x = 0; x < N; x++)
            {
                Color acc = Color.clear; int hits = 0;
                for (int sy = 0; sy < 3; sy++)
                    for (int sx = 0; sx < 3; sx++)
                    {
                        Vector2 p = new Vector2((x + (sx + 0.5f) / 3f) / N - 0.5f, (y + (sy + 0.5f) / 3f) / N - 0.5f) * 2f;
                        float r = p.magnitude;
                        Color c;
                        if (r > 0.96f) continue;
                        if (r > 0.82f) c = new Color(0.75f, 0.42f, 0.04f);
                        else if (InStar(p, 0.5f, 0.24f)) c = new Color(1f, 0.95f, 0.6f);
                        else if (r > 0.64f && r < 0.7f) c = new Color(1f, 0.9f, 0.45f);
                        else c = Color.Lerp(new Color(1f, 0.7f, 0.1f), new Color(1f, 0.85f, 0.25f), (p.y + 1f) / 2f);
                        acc += c; hits++;
                    }
                tex.SetPixel(x, y, hits == 0 ? Color.clear : new Color(acc.r / hits, acc.g / hits, acc.b / hits, hits / 9f));
            }
        tex.Apply();
        return Sprite.Create(tex, new Rect(0, 0, N, N), new Vector2(0.5f, 0.5f), 100f);
    }

    // ---------------- Bosh menyu ----------------
    void SetHud(bool on)
    {
        hudRoot.SetActive(on);
        hintTxt.gameObject.SetActive(on);
    }

    void MenuCamera(bool snap)
    {
        Vector3 target = new Vector3(0.9f, 1.35f, 3.3f);
        cam.transform.position = snap ? target : Vector3.Lerp(cam.transform.position, target, 8f * Time.deltaTime);
        cam.transform.LookAt(new Vector3(0, 1.0f, 0));
    }

    void MenuUpdate(float dt)
    {
        menuT += dt;
        body.localRotation = Quaternion.Euler(0, 150f + menuT * 30f, 0);
        body.localPosition = new Vector3(0, Mathf.Sin(menuT * 2f) * 0.015f, 0);
        body.localScale = new Vector3(0.9f, 0.9f, 0.9f);
        upper.localRotation = Quaternion.identity;
        legL.localRotation = legR.localRotation = kneeL.localRotation = kneeR.localRotation = Quaternion.identity;
        armL.localRotation = Quaternion.Euler(-6f, 0, 0);
        armR.localRotation = Quaternion.Euler(6f, 0, 0);
        elbowL.localRotation = elbowR.localRotation = Quaternion.Euler(-12f, 0, 0);
        MenuCamera(false);

        var k = Keyboard.current;
        if (k != null)
        {
            if (k.leftArrowKey.wasPressedThisFrame || k.aKey.wasPressedThisFrame) NextStyle(-1);
            if (k.rightArrowKey.wasPressedThisFrame || k.dKey.wasPressedThisFrame) NextStyle(1);
            if (k.spaceKey.wasPressedThisFrame || k.enterKey.wasPressedThisFrame) StartGame();
        }
    }

    void NextStyle(int dir)
    {
        SetStyle((styleIndex + dir + Styles.Length) % Styles.Length);
    }

    void SetStyle(int i)
    {
        styleIndex = i;
        PlayerPrefs.SetInt("RoyalRunStyle", i);
        foreach (Transform c in body) { c.gameObject.SetActive(false); Destroy(c.gameObject); }
        BuildCharacter(body);
        bodyRenderers = body.GetComponentsInChildren<Renderer>();
        nameTxt.text = Styles[i].name;
    }

    void StartGame()
    {
        menu = false;
        menuPanel.SetActive(false);
        SetHud(true);
        AdService.I.ShowBanner(true);
        body.localRotation = Quaternion.identity;
        UpdateCamera(true);
    }

    // ---------------- Boshqaruv ----------------
    void HandleInput()
    {
        var k = Keyboard.current;
        if (k != null)
        {
            if (k.aKey.wasPressedThisFrame || k.leftArrowKey.wasPressedThisFrame) ChangeLane(-1);
            if (k.dKey.wasPressedThisFrame || k.rightArrowKey.wasPressedThisFrame) ChangeLane(1);
            if (k.spaceKey.wasPressedThisFrame || k.wKey.wasPressedThisFrame || k.upArrowKey.wasPressedThisFrame) Jump();
            if (k.sKey.wasPressedThisFrame || k.downArrowKey.wasPressedThisFrame) Slide();
        }

        var p = Pointer.current;
        if (p != null)
        {
            if (p.press.wasPressedThisFrame)
            {
                swipeStart = p.position.ReadValue();
                swiping = true;
            }
            else if (swiping && p.press.isPressed)
            {
                Vector2 d = p.position.ReadValue() - swipeStart;
                if (d.magnitude > Mathf.Max(40f, Screen.height * 0.035f))
                {
                    swiping = false;
                    if (Mathf.Abs(d.x) > Mathf.Abs(d.y)) ChangeLane(d.x > 0 ? 1 : -1);
                    else if (d.y > 0) Jump();
                    else Slide();
                }
            }
            else if (!p.press.isPressed) swiping = false;
        }
    }

    void ChangeLane(int d)
    {
        int nl = Mathf.Clamp(lane + d, 0, 2);
        if (nl == lane) return;
        lane = nl;
        Sfx(cLane, 0.7f);
    }

    void Jump()
    {
        if (py > 0.01f) return;
        vy = JumpSpeed;
        py = 0.01f;
        sliding = false;
        Sfx(cJump, 0.8f);
    }

    void Slide()
    {
        if (py > 0.01f) vy = -18f; // havoda bo'lsa tezroq yerga tushadi
        sliding = true;
        slideT = SlideTime;
        Sfx(cSlide, 0.8f);
    }

    bool AnyPress()
    {
        var k = Keyboard.current;
        if (k != null && k.anyKey.wasPressedThisFrame) return true;
        var p = Pointer.current;
        return p != null && p.press.wasPressedThisFrame;
    }

    // ---------------- Kamera ----------------
    void UpdateCamera(bool snap)
    {
        // Vertikal (telefon) ekranda yo'l sig'ishi uchun kamera orqaroqqa va balandroqqa o'tadi
        float tanHalf = Mathf.Tan(30f * Mathf.Deg2Rad);
        float back = Mathf.Max(8f, 4.8f / (tanHalf * Mathf.Max(0.3f, cam.aspect)));
        float height = 4.6f + (back - 8f) * 0.5f;
        Vector3 target = new Vector3(px * 0.6f, height, z - back);
        cam.transform.position = snap ? target : Vector3.Lerp(cam.transform.position, target, 10f * Time.deltaTime);
        cam.transform.LookAt(new Vector3(px * 0.3f, 1.2f, z + 7f));
    }

    // Segmentdagi barcha oddiy obyektlarni material bo'yicha bitta meshga birlashtiradi
    void Combine(Seg s)
    {
        var groups = new Dictionary<Material, List<CombineInstance>>();
        var old = new List<GameObject>();
        Matrix4x4 inv = s.root.transform.worldToLocalMatrix;
        foreach (var mf in s.root.GetComponentsInChildren<MeshFilter>())
        {
            if (mf.transform.parent != null && mf.transform.parent.name == "Coin") continue; // tangalar aylanadi
            var mr = mf.GetComponent<MeshRenderer>();
            if (mr == null) continue;
            if (!groups.TryGetValue(mr.sharedMaterial, out var list)) groups[mr.sharedMaterial] = list = new List<CombineInstance>();
            list.Add(new CombineInstance { mesh = mf.sharedMesh, transform = inv * mf.transform.localToWorldMatrix });
            old.Add(mf.gameObject);
        }
        foreach (var kv in groups)
        {
            var mesh = new Mesh { indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.CombineMeshes(kv.Value.ToArray(), true, true);
            s.meshes.Add(mesh);
            var go = new GameObject("Batch");
            go.transform.SetParent(s.root.transform, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = kv.Key;
        }
        foreach (var g in old) Destroy(g);
    }

    // ---------------- Yo'l generatsiyasi ----------------
    static Color C(float r, float g, float b) { return new Color(r, g, b); }
    static readonly Color Sand = new Color(0.86f, 0.73f, 0.54f);
    static readonly Color TileBlue = new Color(0.12f, 0.36f, 0.78f);
    static readonly Color DeepBlue = new Color(0.08f, 0.2f, 0.5f);
    static readonly Color Teal = new Color(0.1f, 0.62f, 0.72f);
    static readonly Color Red = new Color(0.85f, 0.15f, 0.15f);
    static readonly Color White = new Color(0.97f, 0.97f, 0.97f);
    static readonly Color Navy = new Color(0.1f, 0.14f, 0.3f);

    void SpawnSeg()
    {
        var s = new Seg { z = nextSegZ, root = new GameObject("Segment") };
        var tr = s.root.transform;
        float mid = s.z + SegLen / 2f;

        // tuproq poydevor
        Prim(PrimitiveType.Cube, new Vector3(0, -0.25f, mid), new Vector3(9f, 0.5f, SegLen), C(0.45f, 0.36f, 0.28f), tr);

        // har bir qator uchun relslar va shpallar
        for (int l = 0; l < 3; l++)
        {
            float x = (l - 1) * LaneW;
            foreach (float sx in new[] { -0.7f, 0.7f })
                RB(new Vector3(x + sx, 0.1f, mid), new Vector3(0.12f, 0.14f, SegLen), 0.04f, C(0.62f, 0.64f, 0.7f), tr);
            for (float dz = 0.6f; dz < SegLen; dz += 1.5f)
                RB(new Vector3(x, 0.05f, s.z + dz), new Vector3(2f, 0.1f, 0.35f), 0.04f, C(0.48f, 0.31f, 0.17f), tr);
        }

        // yon tomonlar: yo'lak, chiroq, daraxt, binolar
        foreach (int side in new[] { -1, 1 })
        {
            RB(new Vector3(side * 7f, 0.05f, mid), new Vector3(5f, 0.2f, SegLen), 0.08f, C(0.8f, 0.7f, 0.55f), tr);
            Lamp(tr, side * 4.9f, mid, side);
            Tree(tr, side * Random.Range(6.5f, 8.5f), s.z + Random.Range(2f, SegLen - 2f));

            switch (Random.Range(0, 5))
            {
                case 0: Madrasa(tr, side * 13f, mid, side); break;
                case 1: DomeHall(tr, side * 13f, mid, side); break;
                case 2: Stall(tr, side * 8.6f, mid, side); House(tr, side * 13f, mid, side); break;
                default: House(tr, side * 12.5f, mid, side); break;
            }
        }
        if (segIndex % 7 == 2) Tower(tr, (Random.value < 0.5f ? -1 : 1) * 18f, s.z + 4f, 1.2f);
        if (segIndex % 8 == 3) Gantry(tr, mid);

        if (segIndex >= 3) // boshida bo'sh yo'l
        {
            Row(s, s.z + 6f);
            Row(s, s.z + 18f);
        }

        Combine(s); // yuzlab obyektni bitta meshga birlashtiramiz (telefon uchun tez)
        segIndex++;
        nextSegZ += SegLen;
        segs.Add(s);
    }

    // Har bir qatorda kamida bitta "xavfsiz" yo'l qoldiriladi (poyezd faqat 2 qatorgacha).
    void Row(Seg s, float zz)
    {
        int safe = Random.Range(0, 3);
        for (int l = 0; l < 3; l++)
        {
            float x = (l - 1) * LaneW;
            if (l == safe)
            {
                int r = Random.Range(0, 4);
                if (r == 0) AddObs(s, ObsType.Low, x, zz);
                else if (r == 1) AddObs(s, ObsType.Overhead, x, zz);
                else AddCoins(s, x, zz);
            }
            else
            {
                int stg = 1 + (int)(zz / StageLen);
                int tallN = Mathf.Min(3, 2 + (stg - 1) / 2); // bosqich oshgan sari poyezdlar ko'payadi
                int r = Random.Range(0, 5);
                if (r < tallN) AddObs(s, ObsType.Tall, x, zz);
                else if (r == 2) AddObs(s, ObsType.Low, x, zz);
                else if (r == 3) AddObs(s, ObsType.Overhead, x, zz);
            }
        }
    }

    void AddObs(Seg s, ObsType t, float x, float zz)
    {
        var tr = s.root.transform;
        switch (t)
        {
            case ObsType.Low: // qizil-oq to'siq: ustidan sakrab o'tish kerak
                Stripes(tr, x, 0.55f, zz, 2f, 0.5f);
                RB(new Vector3(x - 0.85f, 0.28f, zz), new Vector3(0.14f, 0.56f, 0.14f), 0.05f, C(0.4f, 0.3f, 0.2f), tr);
                RB(new Vector3(x + 0.85f, 0.28f, zz), new Vector3(0.14f, 0.56f, 0.14f), 0.05f, C(0.4f, 0.3f, 0.2f), tr);
                s.obs.Add(new Obs { c = new Vector3(x, 0.4f, zz), h = new Vector3(1f, 0.4f, 0.3f) });
                break;

            case ObsType.Tall: // poyezd vagoni: chetlab o'tish kerak
                RB(new Vector3(x, 1.4f, zz), new Vector3(2f, 2.8f, 8f), 0.4f, C(0.88f, 0.9f, 0.95f), tr);
                RB(new Vector3(x, 0.75f, zz), new Vector3(2.05f, 0.9f, 8.05f), 0.25f, TileBlue, tr);
                RB(new Vector3(x, 1.28f, zz), new Vector3(2.07f, 0.1f, 8.07f), 0.04f, White, tr);
                RB(new Vector3(x, 2.05f, zz), new Vector3(2.06f, 0.7f, 6.8f), 0.3f, C(0.14f, 0.2f, 0.3f), tr);
                RB(new Vector3(x, 2.88f, zz), new Vector3(1.6f, 0.2f, 7.6f), 0.09f, C(0.55f, 0.58f, 0.65f), tr);
                RB(new Vector3(x, 0.75f, zz - 4.0f), new Vector3(1.9f, 0.9f, 0.1f), 0.04f, TileBlue, tr);
                RB(new Vector3(x, 2.0f, zz - 4.0f), new Vector3(1.6f, 0.9f, 0.1f), 0.12f, C(0.12f, 0.18f, 0.28f), tr);
                Prim(PrimitiveType.Sphere, new Vector3(x - 0.6f, 1.0f, zz - 4.05f), Vector3.one * 0.28f, C(1f, 0.95f, 0.6f), tr);
                Prim(PrimitiveType.Sphere, new Vector3(x + 0.6f, 1.0f, zz - 4.05f), Vector3.one * 0.28f, C(1f, 0.95f, 0.6f), tr);
                s.obs.Add(new Obs { c = new Vector3(x, 1.4f, zz), h = new Vector3(1f, 1.4f, 4f) });
                break;

            case ObsType.Overhead: // yuqoridagi to'sin: tagidan sirg'anib o'tish kerak
                Stripes(tr, x, 1.7f, zz, 2f, 0.6f);
                RB(new Vector3(x - 0.95f, 0.7f, zz), new Vector3(0.16f, 1.4f, 0.3f), 0.06f, C(0.4f, 0.4f, 0.45f), tr);
                RB(new Vector3(x + 0.95f, 0.7f, zz), new Vector3(0.16f, 1.4f, 0.3f), 0.06f, C(0.4f, 0.4f, 0.45f), tr);
                s.obs.Add(new Obs { c = new Vector3(x, 1.7f, zz), h = new Vector3(1f, 0.3f, 0.25f) });
                break;
        }
    }

    void AddCoins(Seg s, float x, float zz)
    {
        for (int i = -2; i <= 2; i++)
        {
            Vector3 pos = new Vector3(x, 1f, zz + i * 2f);
            var holder = new GameObject("Coin").transform;
            holder.SetParent(s.root.transform);
            holder.position = pos;
            var disc = Prim(PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.65f, 0.05f, 0.65f), C(1f, 0.78f, 0.1f), holder);
            disc.transform.localRotation = Quaternion.Euler(90, 0, 0);
            var star = Prim(PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.36f, 0.07f, 0.36f), C(1f, 0.95f, 0.55f), holder);
            star.transform.localRotation = Quaternion.Euler(90, 0, 0);
            s.coins.Add(new Coin { t = holder, c = pos });
        }
    }

    // qizil-oq chiziqli to'sin taxtasi
    void Stripes(Transform tr, float x, float y, float zz, float w, float h)
    {
        int n = 5;
        for (int i = 0; i < n; i++)
            RB(new Vector3(x - w / 2f + w / n * (i + 0.5f), y, zz), new Vector3(w / n, h, 0.16f), 0.05f, i % 2 == 0 ? Red : White, tr);
    }

    // ---------------- Atrof-muhit (O'zbekiston uslubi) ----------------
    void Lamp(Transform tr, float x, float z, int side)
    {
        Obj(MeshKit.LampPost(), new Vector3(x, 0, z), Vector3.one, C(0.15f, 0.15f, 0.18f), tr);
        Obj(MeshKit.Lantern(), new Vector3(x, 4.1f, z), Vector3.one, C(1f, 0.92f, 0.6f), tr);
        // O'zbekiston bayrog'i: ko'k, oq, yashil
        float fz = z + 0.8f;
        RB(new Vector3(x, 3.6f, fz), new Vector3(0.05f, 0.3f, 1.4f), 0.02f, C(0.1f, 0.6f, 0.9f), tr);
        RB(new Vector3(x, 3.3f, fz), new Vector3(0.05f, 0.3f, 1.4f), 0.02f, White, tr);
        RB(new Vector3(x, 3.0f, fz), new Vector3(0.05f, 0.3f, 1.4f), 0.02f, C(0.1f, 0.65f, 0.25f), tr);
    }

    void Tree(Transform tr, float x, float z)
    {
        Prim(PrimitiveType.Cylinder, new Vector3(x, 1.1f, z), new Vector3(0.35f, 1.1f, 0.35f), C(0.4f, 0.27f, 0.15f), tr);
        float gv = Random.Range(0.45f, 0.6f);
        Prim(PrimitiveType.Sphere, new Vector3(x, 3.2f, z), Vector3.one * 2.8f, C(0.15f, gv, 0.2f), tr);
        Prim(PrimitiveType.Sphere, new Vector3(x + 0.8f, 3.6f, z + 0.4f), Vector3.one * 2f, C(0.18f, gv + 0.05f, 0.22f), tr);
        Prim(PrimitiveType.Sphere, new Vector3(x - 0.7f, 3.5f, z - 0.5f), Vector3.one * 2.2f, C(0.12f, gv - 0.03f, 0.18f), tr);
        Prim(PrimitiveType.Sphere, new Vector3(x, 4.6f, z), Vector3.one * 1.8f, C(0.2f, gv + 0.08f, 0.24f), tr);
    }

    // Registon uslubidagi peshtoq: o'tkir arkalar, ko'k kafel, ikki yonida minoralar
    void Madrasa(Transform tr, float x, float z, int side)
    {
        Vector3 dir = new Vector3(-side, 0, 0);
        float fx = x - side * 2.02f; // yo'lga qaragan yuz
        RB(new Vector3(x, 5f, z), new Vector3(4f, 10f, 14f), 0.35f, Sand, tr);
        RB(new Vector3(x, 10.2f, z), new Vector3(4.2f, 0.5f, 14.2f), 0.2f, TileBlue, tr);
        ArchObj(tr, new Vector3(fx, 0, z), 8f, 9.2f, TileBlue, dir);
        ArchObj(tr, new Vector3(fx - side * 0.04f, 0, z), 7.2f, 8.4f, White, dir);
        ArchObj(tr, new Vector3(fx - side * 0.08f, 0, z), 6.8f, 8.0f, Teal, dir);
        ArchObj(tr, new Vector3(fx - side * 0.12f, 0, z), 4.4f, 6.0f, Navy, dir);
        ArchObj(tr, new Vector3(fx - side * 0.16f, 0, z), 3.6f, 5.2f, C(0.05f, 0.07f, 0.16f), dir);
        for (int i = -2; i <= 2; i++)
        {
            var d = RB(new Vector3(fx - side * 0.1f, 9.4f, z + i * 1.5f), new Vector3(0.1f, 0.4f, 0.4f), 0.03f, White, tr);
            d.transform.localRotation = Quaternion.Euler(45, 0, 0);
        }
        Tower(tr, x - side * 1.5f, z - 7.6f, 0.6f);
        Tower(tr, x - side * 1.5f, z + 7.6f, 0.6f);
    }

    // Gumbazli bino: bo'rtma ko'k gumbaz
    void DomeHall(Transform tr, float x, float z, int side)
    {
        Vector3 dir = new Vector3(-side, 0, 0);
        RB(new Vector3(x, 2.5f, z), new Vector3(8f, 5f, 8f), 0.4f, Sand, tr);
        Prim(PrimitiveType.Cylinder, new Vector3(x, 6.3f, z), new Vector3(5f, 1.3f, 5f), C(0.8f, 0.68f, 0.5f), tr);
        Prim(PrimitiveType.Cylinder, new Vector3(x, 6.5f, z), new Vector3(5.2f, 0.18f, 5.2f), TileBlue, tr);
        Obj(MeshKit.Dome(), new Vector3(x, 7.6f, z), Vector3.one * 1.1f, Teal, tr);
        Prim(PrimitiveType.Cylinder, new Vector3(x, 12.2f, z), new Vector3(0.12f, 0.6f, 0.12f), C(0.9f, 0.75f, 0.2f), tr);
        Prim(PrimitiveType.Sphere, new Vector3(x, 12.9f, z), Vector3.one * 0.3f, C(0.9f, 0.75f, 0.2f), tr);
        float fx = x - side * 4.02f;
        ArchObj(tr, new Vector3(fx, 0, z), 3.2f, 3.8f, TileBlue, dir);
        ArchObj(tr, new Vector3(fx - side * 0.05f, 0, z), 2.4f, 3.2f, Navy, dir);
    }

    // Minora: aylanma shakl, ko'k kamarlar va ko'k gumbaz
    void Tower(Transform tr, float x, float z, float scale)
    {
        Obj(MeshKit.Minaret(), new Vector3(x, 0, z), Vector3.one * scale, C(0.86f, 0.68f, 0.48f), tr);
        Prim(PrimitiveType.Cylinder, new Vector3(x, 4f * scale, z), new Vector3(2.5f, 0.2f, 2.5f) * scale, TileBlue, tr);
        Prim(PrimitiveType.Cylinder, new Vector3(x, 8f * scale, z), new Vector3(2.1f, 0.2f, 2.1f) * scale, TileBlue, tr);
        Prim(PrimitiveType.Cylinder, new Vector3(x, 14.6f * scale, z), new Vector3(1.9f, 0.3f, 1.9f) * scale, Teal, tr);
    }

    // Bozor peshtaxtasi: rangli gilamlar va chodir
    void Stall(Transform tr, float x, float z, int side)
    {
        float back = x + side * 1.1f;
        RB(new Vector3(x, 0.55f, z), new Vector3(2.2f, 0.9f, 3f), 0.12f, C(0.5f, 0.33f, 0.18f), tr);
        Color[] carpets = { Red, TileBlue, C(0.9f, 0.55f, 0.15f) };
        for (int i = 0; i < 3; i++)
            RB(new Vector3(back, 1.7f, z - 1f + i * 1f), new Vector3(0.1f, 1.5f, 0.9f), 0.04f, carpets[i], tr);
        var awning = RB(new Vector3(x, 2.7f, z), new Vector3(2.8f, 0.14f, 3.4f), 0.06f, C(0.9f, 0.45f, 0.15f), tr);
        awning.transform.localRotation = Quaternion.Euler(0, 0, side * 10f);
        Prim(PrimitiveType.Cylinder, new Vector3(x - side * 1.2f, 1.3f, z - 1.5f), new Vector3(0.12f, 1.3f, 0.12f), C(0.4f, 0.3f, 0.2f), tr);
        Prim(PrimitiveType.Cylinder, new Vector3(x - side * 1.2f, 1.3f, z + 1.5f), new Vector3(0.12f, 1.3f, 0.12f), C(0.4f, 0.3f, 0.2f), tr);
        Obj(MeshKit.Vase(), new Vector3(x, 1f, z), Vector3.one * 0.5f, C(0.1f, 0.5f, 0.8f), tr);
    }

    void House(Transform tr, float x, float z, int side)
    {
        Vector3 dir = new Vector3(-side, 0, 0);
        float hh = Random.Range(4f, 9f);
        Color c = Color.HSVToRGB(Random.Range(0.07f, 0.12f), 0.3f, Random.Range(0.8f, 0.95f));
        RB(new Vector3(x, hh / 2f, z), new Vector3(5f, hh, 11f), 0.25f, c, tr);
        RB(new Vector3(x - side * 0.02f, hh - 0.8f, z), new Vector3(5.1f, 0.4f, 11.1f), 0.15f, TileBlue, tr);
        float fx = x - side * 2.52f;
        for (int i = -1; i <= 1; i++)
        {
            ArchObj(tr, new Vector3(fx, 1.5f, z + i * 3f), 1.1f, 2f, Navy, dir);
            if (hh > 6f) ArchObj(tr, new Vector3(fx, 4.4f, z + i * 3f), 1.1f, 1.8f, Navy, dir);
        }
    }

    // Yo'l ustidagi bezakli ko'k arka-darvoza
    void Gantry(Transform tr, float z)
    {
        Vector3 dir = Vector3.forward;
        RingObj(tr, new Vector3(0, 0, z), 12.6f, 11.5f, 10.2f, 9.6f, TileBlue, dir);
        RingObj(tr, new Vector3(0, 0, z - 0.05f), 10.4f, 9.8f, 10.0f, 9.4f, White, dir);
        RingObj(tr, new Vector3(0, 0, z + 0.05f), 12.8f, 11.7f, 12.5f, 11.4f, Teal, dir);
        for (int i = -3; i <= 3; i++)
        {
            float ay = i == 0 ? 11f : 10.6f - Mathf.Abs(i) * 0.9f;
            var d = RB(new Vector3(i * 1.4f, ay - 0.1f - Mathf.Abs(i) * 0.05f, z - 0.1f), new Vector3(0.45f, 0.45f, 0.06f), 0.03f, White, tr);
            d.transform.localRotation = Quaternion.Euler(0, 0, 45);
        }
    }

    // ---------------- O'yinchi modeli ----------------
    Transform Pivot(Transform parent, Vector3 pos)
    {
        var t = new GameObject("Pivot").transform;
        t.SetParent(parent, false);
        t.localPosition = pos;
        return t;
    }

    // Do'ppi kiygan, ryukzakli bola (+Z ga qarab yuguradi). Katta bosh, yumaloq shakllar, bo'g'imlari bor.
    void BuildCharacter(Transform m)
    {
        Style st = Styles[styleIndex];
        Color skin = st.skin, skinDark = st.skin * 0.88f;
        Color hood = st.top, pants = st.pants, dark = C(0.12f, 0.12f, 0.16f);
        Color hair = st.hair, cap = st.hat;

        // yuqori tana belda aylanadi (oldinga engashadi)
        upper = Pivot(m, new Vector3(0, 0.9f, 0));
        Vector3 U(float x, float y, float z) { return new Vector3(x, y - 0.9f, z); }

        // tana: kofta, etak, zip, ko'k naqsh
        RB(U(0, 1.12f, 0), new Vector3(0.74f, 0.62f, 0.42f), 0.18f, hood, upper);
        RB(U(0, 0.84f, 0), new Vector3(0.78f, 0.12f, 0.46f), 0.05f, dark, upper);
        RB(U(0, 1.12f, 0.215f), new Vector3(0.04f, 0.58f, 0.03f), 0.01f, dark, upper);
        var chest = RB(U(0.17f, 1.2f, 0.22f), new Vector3(0.13f, 0.13f, 0.03f), 0.02f, st.accent, upper);
        chest.transform.localRotation = Quaternion.Euler(0, 0, 45);
        RB(U(0, 1.46f, -0.14f), new Vector3(0.52f, 0.2f, 0.24f), 0.09f, hood, upper); // kapyushon

        // ryukzak va tasmalar
        RB(U(0, 1.1f, -0.34f), new Vector3(0.5f, 0.6f, 0.26f), 0.11f, st.bag, upper);
        RB(U(0, 1.2f, -0.5f), new Vector3(0.38f, 0.2f, 0.1f), 0.05f, C(0.2f, 0.25f, 0.45f), upper);
        var emb = RB(U(0, 1.05f, -0.48f), new Vector3(0.16f, 0.16f, 0.03f), 0.02f, White, upper);
        emb.transform.localRotation = Quaternion.Euler(0, 0, 45);
        RB(U(-0.2f, 1.14f, 0), new Vector3(0.08f, 0.5f, 0.46f), 0.03f, C(0.2f, 0.22f, 0.3f), upper);
        RB(U(0.2f, 1.14f, 0), new Vector3(0.08f, 0.5f, 0.46f), 0.03f, C(0.2f, 0.22f, 0.3f), upper);

        // bosh: katta, quloq, soch, yuz
        Prim(PrimitiveType.Cylinder, U(0, 1.44f, 0), new Vector3(0.2f, 0.06f, 0.2f), skin, upper);
        Prim(PrimitiveType.Sphere, U(0, 1.64f, 0), new Vector3(0.6f, 0.6f, 0.6f), skin, upper);
        Prim(PrimitiveType.Sphere, U(-0.3f, 1.62f, 0), Vector3.one * 0.12f, skin, upper);
        Prim(PrimitiveType.Sphere, U(0.3f, 1.62f, 0), Vector3.one * 0.12f, skin, upper);
        Prim(PrimitiveType.Sphere, U(0, 1.66f, -0.07f), new Vector3(0.63f, 0.56f, 0.55f), hair, upper);
        // yuz (oldindan va yon tomondan ko'rinadi)
        foreach (float ex in new[] { -0.1f, 0.1f })
        {
            Prim(PrimitiveType.Sphere, U(ex, 1.63f, 0.265f), new Vector3(0.075f, 0.09f, 0.04f), White, upper);
            Prim(PrimitiveType.Sphere, U(ex, 1.63f, 0.285f), new Vector3(0.045f, 0.055f, 0.03f), C(0.05f, 0.03f, 0.02f), upper);
            RB(U(ex, 1.72f, 0.265f), new Vector3(0.11f, 0.025f, 0.03f), 0.01f, hair, upper);
        }
        Prim(PrimitiveType.Sphere, U(0, 1.58f, 0.29f), new Vector3(0.07f, 0.06f, 0.06f), skinDark, upper);
        RB(U(0, 1.5f, 0.28f), new Vector3(0.1f, 0.025f, 0.03f), 0.01f, C(0.55f, 0.2f, 0.2f), upper);

        // bosh kiyimi
        if (st.head == 0) // do'ppi: qora, oq naqshli
        {
            RB(U(0, 1.86f, 0), new Vector3(0.64f, 0.12f, 0.64f), 0.06f, cap, upper);
            RB(U(0, 1.94f, 0), new Vector3(0.52f, 0.14f, 0.52f), 0.07f, cap, upper);
            RB(U(0, 1.81f, 0), new Vector3(0.66f, 0.03f, 0.66f), 0.01f, White, upper);
            for (int side = 0; side < 4; side++)
            {
                float ang = side * 90f * Mathf.Deg2Rad;
                Vector3 off = new Vector3(Mathf.Sin(ang), 0, Mathf.Cos(ang));
                var dm = RB(U(off.x * 0.325f, 1.87f, off.z * 0.325f), new Vector3(0.14f, 0.14f, 0.03f), 0.02f, White, upper);
                dm.transform.localRotation = Quaternion.LookRotation(off) * Quaternion.Euler(0, 0, 45);
            }
        }
        else if (st.head == 1) // beysbolka
        {
            RB(U(0, 1.92f, 0), new Vector3(0.64f, 0.22f, 0.64f), 0.1f, cap, upper);
            RB(U(0, 1.85f, 0.38f), new Vector3(0.5f, 0.05f, 0.32f), 0.02f, cap, upper);
            RB(U(0, 1.9f, 0.33f), new Vector3(0.12f, 0.1f, 0.03f), 0.02f, White, upper);
        }
        else // sochga bog'langan lenta va tepada soch
        {
            Prim(PrimitiveType.Sphere, U(0, 1.72f, -0.02f), new Vector3(0.62f, 0.5f, 0.6f), hair, upper);
            RB(U(0, 1.83f, 0), new Vector3(0.62f, 0.07f, 0.62f), 0.03f, cap, upper);
            var bow1 = RB(U(0.22f, 1.88f, 0.1f), new Vector3(0.16f, 0.1f, 0.05f), 0.03f, cap, upper);
            bow1.transform.localRotation = Quaternion.Euler(0, 0, 25);
            var bow2 = RB(U(0.3f, 1.84f, 0.1f), new Vector3(0.16f, 0.1f, 0.05f), 0.03f, cap, upper);
            bow2.transform.localRotation = Quaternion.Euler(0, 0, -25);
        }
        if (st.ponytail)
        {
            Prim(PrimitiveType.Sphere, U(0, 1.72f, -0.36f), Vector3.one * 0.16f, cap, upper);
            Prim(PrimitiveType.Sphere, U(0, 1.6f, -0.45f), new Vector3(0.24f, 0.32f, 0.22f), hair, upper);
            Prim(PrimitiveType.Sphere, U(0, 1.38f, -0.48f), new Vector3(0.2f, 0.34f, 0.2f), hair, upper);
            Prim(PrimitiveType.Sphere, U(0, 1.16f, -0.5f), new Vector3(0.15f, 0.28f, 0.15f), hair, upper);
        }
        if (st.glasses)
        {
            foreach (float ex in new[] { -0.1f, 0.1f })
                RB(U(ex, 1.64f, 0.3f), new Vector3(0.17f, 0.1f, 0.04f), 0.03f, C(0.03f, 0.03f, 0.05f), upper);
            RB(U(0, 1.66f, 0.3f), new Vector3(0.06f, 0.02f, 0.03f), 0.01f, C(0.03f, 0.03f, 0.05f), upper);
        }
        if (st.pattern) // kofta bezagi (ikat uslubida oq-ko'k rombchalar)
            for (int i = 0; i < 3; i++)
            {
                var pd = RB(U(-0.17f, 0.98f + i * 0.2f, 0.22f), new Vector3(0.1f, 0.1f, 0.03f), 0.02f, i % 2 == 0 ? White : TileBlue, upper);
                pd.transform.localRotation = Quaternion.Euler(0, 0, 45);
                var pb = RB(U(0, 0.98f + i * 0.2f, -0.22f), new Vector3(0.14f, 0.14f, 0.03f), 0.02f, i % 2 == 0 ? White : TileBlue, upper);
                pb.transform.localRotation = Quaternion.Euler(0, 0, 45);
            }

        // qo'llar: yelka -> tirsak -> panja
        armL = Pivot(upper, U(-0.48f, 1.3f, 0));
        armR = Pivot(upper, U(0.48f, 1.3f, 0));
        elbowL = BuildArm(armL, hood, dark, skin);
        elbowR = BuildArm(armR, hood, dark, skin);

        // oyoqlar: son -> tizza -> boldir -> krossovka (belga bog'lanadi)
        legL = Pivot(m, new Vector3(-0.17f, 0.78f, 0));
        legR = Pivot(m, new Vector3(0.17f, 0.78f, 0));
        kneeL = BuildLeg(legL, pants);
        kneeR = BuildLeg(legR, pants);
    }

    Transform BuildArm(Transform shoulder, Color sleeve, Color cuff, Color skin)
    {
        Prim(PrimitiveType.Sphere, Vector3.zero, Vector3.one * 0.26f, sleeve, shoulder);
        Prim(PrimitiveType.Capsule, new Vector3(0, -0.19f, 0), new Vector3(0.22f, 0.21f, 0.22f), sleeve, shoulder);
        var elbow = Pivot(shoulder, new Vector3(0, -0.36f, 0));
        Prim(PrimitiveType.Capsule, new Vector3(0, -0.16f, 0), new Vector3(0.2f, 0.19f, 0.2f), sleeve, elbow);
        RB(new Vector3(0, -0.31f, 0), new Vector3(0.21f, 0.09f, 0.21f), 0.04f, cuff, elbow);
        Prim(PrimitiveType.Sphere, new Vector3(0, -0.4f, 0), Vector3.one * 0.19f, skin, elbow);
        return elbow;
    }

    Transform BuildLeg(Transform hip, Color pants)
    {
        Prim(PrimitiveType.Capsule, new Vector3(0, -0.19f, 0), new Vector3(0.29f, 0.2f, 0.29f), pants, hip);
        var knee = Pivot(hip, new Vector3(0, -0.37f, 0));
        Prim(PrimitiveType.Capsule, new Vector3(0, -0.17f, 0), new Vector3(0.27f, 0.19f, 0.27f), pants, knee);
        RB(new Vector3(0, -0.31f, 0), new Vector3(0.28f, 0.08f, 0.28f), 0.04f, C(0.2f, 0.25f, 0.45f), knee); // manjet
        RB(new Vector3(0, -0.35f, 0.07f), new Vector3(0.3f, 0.16f, 0.46f), 0.08f, Styles[styleIndex].shoe, knee);               // krossovka
        RB(new Vector3(0, -0.42f, 0.07f), new Vector3(0.31f, 0.04f, 0.47f), 0.02f, C(0.55f, 0.58f, 0.65f), knee); // tagcharm
        RB(new Vector3(0, -0.33f, 0.2f), new Vector3(0.31f, 0.05f, 0.2f), 0.02f, C(0.12f, 0.36f, 0.78f), knee);   // ko'k bezak
        return knee;
    }

    // ---------------- Shakl yaratish yordamchilari ----------------
    GameObject Obj(Mesh mesh, Vector3 pos, Vector3 scale, Color col, Transform parent)
    {
        var go = new GameObject("Mesh");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = Mat(col);
        return go;
    }

    GameObject RB(Vector3 pos, Vector3 size, float r, Color col, Transform parent)
    {
        // o'lchamni 0.05 ga yaxlitlab, bir xil meshlar qayta ishlatiladi
        size = new Vector3(Mathf.Round(size.x * 20f) / 20f, Mathf.Round(size.y * 20f) / 20f, Mathf.Round(size.z * 20f) / 20f);
        return Obj(MeshKit.RoundedBox(size, r), pos, Vector3.one, col, parent);
    }

    GameObject ArchObj(Transform parent, Vector3 pos, float w, float h, Color col, Vector3 dir)
    {
        var go = Obj(MeshKit.Arch(w, h), pos, Vector3.one, col, parent);
        go.transform.localRotation = Quaternion.LookRotation(dir);
        return go;
    }

    GameObject RingObj(Transform parent, Vector3 pos, float wo, float ho, float wi, float hi, Color col, Vector3 dir)
    {
        var go = Obj(MeshKit.Ring(wo, ho, wi, hi), pos, Vector3.one, col, parent);
        go.transform.localRotation = Quaternion.LookRotation(dir);
        return go;
    }

    // ---------------- Yordamchi funksiyalar ----------------
    // Fizika moduli build'da kesib tashlanmasligi uchun havola (CreatePrimitive collider qo'shadi)
    static readonly System.Type[] KeepPhysics = { typeof(BoxCollider), typeof(SphereCollider), typeof(CapsuleCollider), typeof(MeshCollider) };
    static readonly Dictionary<PrimitiveType, Mesh> primMeshes = new Dictionary<PrimitiveType, Mesh>();

    static Mesh PrimMesh(PrimitiveType type)
    {
        if (primMeshes.TryGetValue(type, out var m) && m != null) return m;
        var tmp = GameObject.CreatePrimitive(type);
        m = tmp.GetComponent<MeshFilter>().sharedMesh;
        Destroy(tmp);
        primMeshes[type] = m;
        return m;
    }

    // Tayyor shakl (kub, sfera, ...): colliderlarsiz, faqat mesh + material. To'qnashuvni kodda tekshiramiz.
    GameObject Prim(PrimitiveType type, Vector3 pos, Vector3 scale, Color col, Transform parent)
    {
        return Obj(PrimMesh(type), pos, scale, col, parent);
    }

    Material Mat(Color c)
    {
        if (mats.TryGetValue(c, out var m)) return m;
        var sh = Shader.Find("Universal Render Pipeline/Lit");
        if (sh == null) sh = Shader.Find("Standard");
        m = new Material(sh) { color = c };
        m.SetColor("_BaseColor", c);
        mats[c] = m;
        return m;
    }

    // ---------------- UI ----------------
    void BuildUI()
    {
        var cv = new GameObject("UI").AddComponent<Canvas>();
        cv.renderMode = RenderMode.ScreenSpaceOverlay;
        cv.gameObject.AddComponent<GraphicRaycaster>(); // tugmalar bosilishi uchun shart
        var sc = cv.gameObject.AddComponent<CanvasScaler>();
        sc.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        sc.referenceResolution = new Vector2(1080, 1920);
        sc.matchWidthOrHeight = 0.5f;

        // telefon "chakka" (notch) va chetlaridan qochish uchun xavfsiz maydon
        var safe = new GameObject("SafeArea", typeof(RectTransform)).GetComponent<RectTransform>();
        safe.SetParent(cv.transform, false);
        Rect sa = Screen.safeArea;
        safe.anchorMin = new Vector2(sa.xMin / Screen.width, sa.yMin / Screen.height);
        safe.anchorMax = new Vector2(sa.xMax / Screen.width, sa.yMax / Screen.height);
        safe.offsetMin = safe.offsetMax = Vector2.zero;
        var ui = safe;

        BuildHud(ui);
        bannerTxt = MakeText(ui, "Banner", new Vector2(0.5f, 0.72f), Vector2.zero, TextAnchor.MiddleCenter, 110);
        bannerTxt.color = new Color(1f, 0.85f, 0.2f, 0f);
        bannerTxt.rectTransform.sizeDelta = new Vector2(1000, 200);

        muteBtn = MakeButton2(ui, "Ovoz", new Vector2(1f, 1f), new Vector2(-40, -130), new Vector2(170, 80), new Color(0.15f, 0.55f, 0.25f), 36, ToggleMute);
        muteLbl = muteBtn.GetComponentInChildren<Text>();
        RefreshMute();
        BuildMenu(ui);
        BuildDialogs(cv.transform);
        hintTxt = MakeText(ui, "Hint", new Vector2(0.5f, 0.25f), Vector2.zero, TextAnchor.MiddleCenter, 48);
        hintTxt.rectTransform.sizeDelta = new Vector2(1000, 300);
        hintTxt.text = "Chapga / o'ngga surish: qator almashtirish\nYuqoriga: sakrash    Pastga: sirg'anish";
    }

    void BuildDialogs(Transform root)
    {
        // tugmalar ishlashi uchun EventSystem (yangi Input System bilan)
        var esys = FindAnyObjectByType<EventSystem>();
        if (esys == null) esys = new GameObject("EventSystem", typeof(EventSystem)).GetComponent<EventSystem>();
        var old = esys.GetComponent<BaseInputModule>();
        if (old != null && !(old is InputSystemUIInputModule)) Destroy(old); // eski input moduli yangi tizim bilan ishlamaydi
        if (esys.GetComponent<InputSystemUIInputModule>() == null)
            esys.gameObject.AddComponent<InputSystemUIInputModule>().AssignDefaultActions();

        // O'yin tugadi dialogi
        dialog = MakePanel(root, "Dialog", new Color(0, 0, 0, 0.7f));
        dlgTxt = MakeText(dialog.transform, "Title", new Vector2(0.5f, 0.5f), new Vector2(0, 330), TextAnchor.MiddleCenter, 64);
        dlgTxt.rectTransform.sizeDelta = new Vector2(1000, 600);
        dlgNote = MakeText(dialog.transform, "Note", new Vector2(0.5f, 0.5f), new Vector2(0, -640), TextAnchor.MiddleCenter, 36);
        dlgNote.color = new Color(1f, 0.8f, 0.4f);
        dlgNote.rectTransform.sizeDelta = new Vector2(1000, 80);
        contBtn = MakeButton(dialog.transform, "Reklama ko'rib davom etish", new Vector2(0, -90), new Color(0.15f, 0.6f, 0.25f), StartAd);
        MakeButton(dialog.transform, "Qayta boshlash", new Vector2(0, -290), new Color(0.15f, 0.4f, 0.8f), Restart);
        MakeButton(dialog.transform, "Bosh menyu", new Vector2(0, -490), new Color(0.4f, 0.4f, 0.45f), GoMenu);
        dialog.SetActive(false);

        // Reklama ekrani (sinov)
        adPanel = MakePanel(root, "AdPanel", new Color(0.05f, 0.05f, 0.1f, 0.97f));
        adTxt = MakeText(adPanel.transform, "AdText", new Vector2(0.5f, 0.5f), Vector2.zero, TextAnchor.MiddleCenter, 72);
        adTxt.rectTransform.sizeDelta = new Vector2(1000, 600);
        adPanel.SetActive(false);
    }

    GameObject MakePanel(Transform parent, string name, Color col)
    {
        var go = new GameObject(name, typeof(RectTransform), typeof(Image));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        go.GetComponent<Image>().color = col;
        return go;
    }

    GameObject MakeButton(Transform parent, string label, Vector2 pos, Color col, UnityEngine.Events.UnityAction act)
    {
        return MakeButton2(parent, label, new Vector2(0.5f, 0.5f), pos, new Vector2(820, 150), col, 46, act);
    }

    GameObject MakeButton2(Transform parent, string label, Vector2 anchor, Vector2 pos, Vector2 size, Color col, int fontSize, UnityEngine.Events.UnityAction act)
    {
        var go = new GameObject(label, typeof(RectTransform), typeof(Image), typeof(Button));
        go.transform.SetParent(parent, false);
        var rt = (RectTransform)go.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
        rt.anchoredPosition = pos;
        rt.sizeDelta = size;
        go.GetComponent<Image>().color = col;
        go.GetComponent<Button>().onClick.AddListener(() => Sfx(cClick, 0.6f));
        go.GetComponent<Button>().onClick.AddListener(act);
        var t = MakeText(go.transform, "Label", new Vector2(0.5f, 0.5f), Vector2.zero, TextAnchor.MiddleCenter, fontSize);
        t.rectTransform.sizeDelta = size;
        t.text = label;
        return go;
    }

    // Bosh menyu: sarlavha, personaj tanlash, "O'ynash"
    void BuildMenu(Transform root)
    {
        menuPanel = new GameObject("Menu", typeof(RectTransform));
        menuPanel.transform.SetParent(root, false);
        var rt = (RectTransform)menuPanel.transform;
        rt.anchorMin = Vector2.zero; rt.anchorMax = Vector2.one;
        rt.offsetMin = rt.offsetMax = Vector2.zero;
        var mt = menuPanel.transform;

        var title = MakeText(mt, "Title", new Vector2(0.5f, 1f), new Vector2(0, -170), TextAnchor.MiddleCenter, 110);
        title.text = "ROYAL RUN";
        title.rectTransform.sizeDelta = new Vector2(1000, 160);
        var sub = MakeText(mt, "Sub", new Vector2(0.5f, 1f), new Vector2(0, -290), TextAnchor.MiddleCenter, 60);
        sub.text = "O'ZBEKISTON";
        sub.color = new Color(1f, 0.82f, 0.2f);
        sub.rectTransform.sizeDelta = new Vector2(1000, 100);

        nameTxt = MakeText(mt, "Name", new Vector2(0.5f, 0.27f), Vector2.zero, TextAnchor.MiddleCenter, 72);
        nameTxt.text = Styles[styleIndex].name;
        nameTxt.rectTransform.sizeDelta = new Vector2(800, 120);

        MakeButton2(mt, "<", new Vector2(0.1f, 0.55f), Vector2.zero, new Vector2(150, 150), new Color(0.15f, 0.4f, 0.8f), 80, () => NextStyle(-1));
        MakeButton2(mt, ">", new Vector2(0.9f, 0.55f), Vector2.zero, new Vector2(150, 150), new Color(0.15f, 0.4f, 0.8f), 80, () => NextStyle(1));
        MakeButton2(mt, "O'YNASH", new Vector2(0.5f, 0.13f), Vector2.zero, new Vector2(760, 170), new Color(0.15f, 0.65f, 0.25f), 64, StartGame);

        menuBestTxt = MakeText(mt, "Best", new Vector2(0.5f, 0.21f), Vector2.zero, TextAnchor.MiddleCenter, 48);
        menuBestTxt.text = "Rekord: " + (int)best;
        menuBestTxt.rectTransform.sizeDelta = new Vector2(800, 80);
    }

    Text MakeText(Transform parent, string name, Vector2 anchor, Vector2 pos, TextAnchor align, int size)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        var t = go.AddComponent<Text>();
        t.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        t.fontSize = size;
        t.fontStyle = FontStyle.Bold;
        t.alignment = align;
        t.color = Color.white;
        t.raycastTarget = false;
        t.horizontalOverflow = HorizontalWrapMode.Overflow;
        t.verticalOverflow = VerticalWrapMode.Overflow;
        var rt = t.rectTransform;
        rt.anchorMin = rt.anchorMax = rt.pivot = anchor;
        rt.anchoredPosition = pos;
        rt.sizeDelta = new Vector2(700, 100);
        var shadow = go.AddComponent<Shadow>();
        shadow.effectDistance = new Vector2(3, -3);
        return t;
    }
}
