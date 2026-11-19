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
        public List<Pickup> pickups = new List<Pickup>();
        public List<Mover> movers = new List<Mover>();
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
    Material skyMat;

    // Bosqich palitrasi: osmon tepasi, ufq, quyosh, atrof yorug'ligi (osmon/ufq/yer), yulduzlar
    struct Pal
    {
        public Color top, horizon, sun, ambSky, ambEq, ambGround;
        public float sunI, stars;
        public static Pal Lerp(Pal a, Pal b, float t)
        {
            return new Pal
            {
                top = Color.Lerp(a.top, b.top, t), horizon = Color.Lerp(a.horizon, b.horizon, t), sun = Color.Lerp(a.sun, b.sun, t),
                ambSky = Color.Lerp(a.ambSky, b.ambSky, t), ambEq = Color.Lerp(a.ambEq, b.ambEq, t), ambGround = Color.Lerp(a.ambGround, b.ambGround, t),
                sunI = Mathf.Lerp(a.sunI, b.sunI, t), stars = Mathf.Lerp(a.stars, b.stars, t)
            };
        }
    }

    static readonly Pal[] Pals =
    {
        new Pal { top = new Color(0.22f, 0.52f, 0.95f), horizon = new Color(0.8f, 0.91f, 1f), sun = new Color(1f, 0.95f, 0.84f), sunI = 1.35f,   // kunduz
                  ambSky = new Color(0.62f, 0.74f, 0.95f), ambEq = new Color(0.8f, 0.76f, 0.7f), ambGround = new Color(0.5f, 0.42f, 0.34f) },
        new Pal { top = new Color(0.3f, 0.38f, 0.78f), horizon = new Color(1f, 0.7f, 0.45f), sun = new Color(1f, 0.72f, 0.45f), sunI = 1.2f,     // quyosh botishi
                  ambSky = new Color(0.6f, 0.56f, 0.75f), ambEq = new Color(0.9f, 0.62f, 0.45f), ambGround = new Color(0.45f, 0.32f, 0.26f) },
        new Pal { top = new Color(0.17f, 0.15f, 0.42f), horizon = new Color(0.85f, 0.48f, 0.55f), sun = new Color(0.85f, 0.62f, 0.85f), sunI = 0.9f, // shom
                  ambSky = new Color(0.42f, 0.38f, 0.65f), ambEq = new Color(0.6f, 0.44f, 0.52f), ambGround = new Color(0.26f, 0.2f, 0.26f), stars = 0.3f },
        new Pal { top = new Color(0.02f, 0.04f, 0.14f), horizon = new Color(0.12f, 0.17f, 0.36f), sun = new Color(0.55f, 0.66f, 1f), sunI = 0.6f,  // tun
                  ambSky = new Color(0.26f, 0.32f, 0.55f), ambEq = new Color(0.24f, 0.26f, 0.4f), ambGround = new Color(0.12f, 0.12f, 0.17f), stars = 1f },
    };
    Pal pal = Pals[0];

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
        new Style { name = "Alisher", top = new Color(0.95f, 0.93f, 0.88f), pants = new Color(0.1f, 0.12f, 0.25f), shoe = new Color(0.97f, 0.97f, 0.97f),
            hair = new Color(0.16f, 0.1f, 0.07f), skin = new Color(0.96f, 0.77f, 0.62f), accent = new Color(0.1f, 0.13f, 0.3f),
            bag = new Color(0.09f, 0.12f, 0.28f), hat = new Color(0.06f, 0.06f, 0.08f), head = 0 },
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
    readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();
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
        cam.backgroundColor = Pals[0].horizon;
        cam.farClipPlane = 250f;
        cam.fieldOfView = 60f;

        if (FindAnyObjectByType<Light>() == null)
        {
            var l = new GameObject("Sun").AddComponent<Light>();
            l.type = LightType.Directional;
        }
        SetupAtmosphere();

        // o'yinchi
        player = new GameObject("Player").transform;
        body = new GameObject("Model").transform;
        body.SetParent(player, false);
        styleIndex = Mathf.Clamp(PlayerPrefs.GetInt("RoyalRunStyle", 0), 0, Styles.Length - 1);
        BuildCharacter(body);
        bodyRenderers = body.GetComponentsInChildren<Renderer>();

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

        UpdateJuice(dt);

        if (dead)
        {
            ApplyShake();
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
        scoreF += speed * dt * stage * (x2T > 0f ? 2f : 1f);
        UpdateBoosts(dt);
        cam.fieldOfView = Mathf.Lerp(cam.fieldOfView, 60f + (speed - StartSpeed) * 0.4f, 2f * dt);

        int newStage = 1 + (int)(z / StageLen);
        if (newStage > stage) OnStage(newStage);
        UpdateStageVisuals(dt);

        px = Mathf.MoveTowards(px, (lane - 1) * LaneW, 22f * dt);

        if (py > 0f || vy > 0f)
        {
            vy -= Gravity * dt;
            py += vy * dt;
            if (py <= 0f) { Land(vy); py = 0f; vy = 0f; }
        }
        if (sliding)
        {
            slideT -= dt;
            if (slideT <= 0f) sliding = false;
        }

        float h = sliding ? 0.9f : 1.8f;
        player.position = new Vector3(px, py, z);
        float sq = 1f + squash; // squash > 0: cho'zilish, < 0: yassilanish (hajm taxminan saqlanadi)
        float side = 1f / Mathf.Sqrt(Mathf.Max(0.3f, sq));
        body.localScale = new Vector3(0.9f * side, 0.9f * h / 1.8f * sq, 0.9f * side);

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
        foreach (var s in segs) // ko'chadagi mashinalar (faqat ko'rinish, to'qnashuvsiz)
            foreach (var mv in s.movers)
                if (mv.t != null) mv.t.position += new Vector3(0f, 0f, mv.v * dt);
        foreach (var s in segs)
        {
            if (s.z > z + 40f || s.z + SegLen < z - 5f) continue;
            if (invT <= 0f)
            foreach (var o in s.obs)
                if (Overlap(pc, ph, o.c, o.h)) { Die(); return; }
            foreach (var p in s.pickups)
            {
                if (p.taken) continue;
                p.t.Rotate(0, 120f * dt, 0);
                p.t.position = new Vector3(p.c.x, p.baseY + Mathf.Sin(Time.time * 3f + p.c.z) * 0.15f, p.c.z);
                if (Overlap(pc, ph, p.c, new Vector3(0.45f, 0.5f, 0.45f))) TakePickup(p);
            }
            foreach (var c in s.coins)
            {
                if (c.taken) continue;
                c.t.Rotate(0, 240f * dt, 0);
                if (magnetT > 0f && c.c.z < z + 18f && c.c.z > z - 2f) // magnit: tangalar o'yinchiga uchib keladi
                {
                    Vector3 np = Vector3.MoveTowards(c.t.position, new Vector3(px, py + 1f, z), (speed + 22f) * dt);
                    c.t.position = np;
                    c.c = np;
                }
                if (Overlap(pc, ph, c.c, new Vector3(0.35f, 0.35f, 0.35f)))
                {
                    c.taken = true;
                    coinPops.Add(new CoinPop { t = c.t });
                    coinPunch = 1f;
                    coinCount++;
                    scoreF += 10f * stage * (x2T > 0f ? 2f : 1f);
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
        multTxt.text = "x" + stage * (x2T > 0f ? 2 : 1);
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
        AddTrauma(0.85f);
        StartCoroutine(HitStop(0.12f, 0.05f));
        Flash(0.55f);
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
        Time.timeScale = 1f;
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
        AddTrauma(0.35f);
        squash = 0.25f; squashVel = 0f;
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
        bannerTxt.text = "BOSQICH " + n + "\n<size=64>" + ZoneNames[(n - 1) % 4] + "</size>";
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
            float bt = Mathf.Clamp01((2.5f - bannerT) / 0.35f);
            bannerTxt.rectTransform.localScale = Vector3.one * Mathf.LerpUnclamped(0.4f, 1f, EaseOutBack(bt));
        }
        int idx = (stage - 1) % Pals.Length; // kunduz -> quyosh botishi -> shom -> tun -> yana kunduz
        pal = Pal.Lerp(pal, Pals[idx], 0.6f * dt);
        ApplyPalette();
    }

    // ---------------- Atmosfera: osmon, tuman, yorug'lik, rang korreksiyasi ----------------
    void SetupAtmosphere()
    {
        sun = FindAnyObjectByType<Light>();
        if (sun != null)
        {
            sun.transform.rotation = Quaternion.Euler(42f, -35f, 0f);
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.55f; // multfilm uslubidagi yumshoq soyalar
        }

        var sh = Shader.Find("RoyalRun/Sky");
        if (sh != null)
        {
            skyMat = new Material(sh);
            skyMat.SetVector("_SunDir", new Vector4(0.35f, 0.12f, 1f, 0f));
            RenderSettings.skybox = skyMat;
            cam.clearFlags = CameraClearFlags.Skybox;
        }

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogStartDistance = 70f;
        RenderSettings.fogEndDistance = 235f;

        // rang korreksiyasi: biroz to'yinganroq va kontrastliroq (multfilm ko'rinishi)
        var vol = FindAnyObjectByType<UnityEngine.Rendering.Volume>();
        if (vol != null && vol.profile != null)
        {
            var prof = vol.profile;
            if (!prof.TryGet(out UnityEngine.Rendering.Universal.ColorAdjustments ca)) ca = prof.Add<UnityEngine.Rendering.Universal.ColorAdjustments>(true);
            ca.active = true;
            ca.saturation.Override(24f);
            ca.contrast.Override(10f);
            ca.postExposure.Override(0.1f);
            if (prof.TryGet(out UnityEngine.Rendering.Universal.Bloom bloom))
            {
                bloom.threshold.Override(0.95f);
                bloom.intensity.Override(0.6f);
            }
        }

        pal = Pals[0];
        ApplyPalette();
    }

    void ApplyPalette()
    {
        if (skyMat != null)
        {
            skyMat.SetColor("_Top", pal.top);
            skyMat.SetColor("_Horizon", pal.horizon);
            skyMat.SetColor("_Ground", Color.Lerp(pal.horizon, new Color(0.85f, 0.75f, 0.6f), 0.4f));
            skyMat.SetColor("_SunColor", pal.sun);
            skyMat.SetFloat("_Stars", pal.stars);
            skyMat.SetFloat("_Clouds", 1f - pal.stars * 0.7f);
        }
        else cam.backgroundColor = pal.horizon;
        RenderSettings.fogColor = pal.horizon;
        RenderSettings.ambientSkyColor = pal.ambSky;
        RenderSettings.ambientEquatorColor = pal.ambEq;
        RenderSettings.ambientGroundColor = pal.ambGround;
        if (sun != null) { sun.color = pal.sun; sun.intensity = pal.sunI; }
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
        coinPill = (RectTransform)cp;
        coinTxt = MakeText(cp, "Coins", new Vector2(0, 0.5f), new Vector2(30, 0), TextAnchor.MiddleLeft, 56);
        coinTxt.rectTransform.sizeDelta = new Vector2(180, 90);
        MakeIcon(cp, coinSprite, new Vector2(1, 0.5f), new Vector2(-55, 0), 70);

        // bosqich kapsulasi
        var gp = MakePill(hud, new Vector2(40, -180), new Vector2(240, 100));
        stageTxt = MakeText(gp, "Stage", new Vector2(0.5f, 0.5f), Vector2.zero, TextAnchor.MiddleCenter, 36);
        stageTxt.rectTransform.sizeDelta = new Vector2(240, 90);
        stageTxt.color = new Color(0.7f, 0.85f, 1f);

        // faol bonuslar (magnit / x2) va qolgan soniyalar
        var bp = MakePill(hud, new Vector2(40, -300), new Vector2(460, 90));
        boostPill = bp.gameObject;
        boostTxt = MakeText(bp, "Boost", new Vector2(0.5f, 0.5f), Vector2.zero, TextAnchor.MiddleCenter, 40);
        boostTxt.rectTransform.sizeDelta = new Vector2(460, 80);
        boostTxt.color = new Color(1f, 0.85f, 0.3f);
        boostPill.SetActive(false);
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
        armL.localRotation = Quaternion.Euler(-4f, 0, -7f);
        armR.localRotation = Quaternion.Euler(4f, 0, 7f);
        elbowL.localRotation = elbowR.localRotation = Quaternion.Euler(-14f, 0, 0);
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
        squash = 0.22f; squashVel = 0f;
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
        camBasePos = snap ? target : Vector3.Lerp(camBasePos, target, 10f * Time.deltaTime);
        camBaseRot = Quaternion.LookRotation(new Vector3(px * 0.3f, 1.2f, z + 7f) - camBasePos);
        ApplyShake();
    }

    // ---------------- O'yin hissi (juice): silkinish, to'xtash, cho'zilish, tanga ----------------
    // Faqat ko'rinishga ta'sir qiladi: kamera, model shkalasi, UI. O'yin mantig'i va to'qnashuvlar o'zgarmaydi.
    class CoinPop { public Transform t; public float age; }
    readonly List<CoinPop> coinPops = new List<CoinPop>();
    Vector3 camBasePos;
    Quaternion camBaseRot = Quaternion.identity;
    float trauma, shakeT, squash, squashVel, coinPunch, flashA;
    int hitStopId;
    RectTransform coinPill;
    Image flashImg;
    const float ShakeDecay = 1.4f;          // soniyasiga kamayadigan "trauma"
    const float ShakeMaxOffset = 0.35f;     // metr
    const float ShakeMaxRoll = 2.5f;        // gradus

    void AddTrauma(float amount)
    {
        float strength = PlayerPrefs.GetFloat("RoyalRunShake", 1f); // 0 = silkinish o'chiq
        trauma = Mathf.Clamp01(trauma + amount * strength);
    }

    void Land(float fallSpeed)
    {
        float k = Mathf.Clamp01(-fallSpeed / 14f);
        squash = -0.28f * Mathf.Max(0.4f, k); squashVel = 0f;
        if (fallSpeed < -12f) AddTrauma(0.15f); // faqat qattiq qo'nishda
    }

    // Kamera = asosiy holat + silkinish (trauma^2, sinuslar yig'indisi, real vaqtda so'nadi)
    void ApplyShake()
    {
        float real = Time.unscaledDeltaTime;
        Vector3 off = Vector3.zero; float roll = 0f;
        if (trauma > 0f)
        {
            trauma = Mathf.Max(0f, trauma - ShakeDecay * real);
            shakeT += real * 32f;
            float sh = trauma * trauma;
            off = new Vector3(Mathf.Sin(shakeT * 1.7f), Mathf.Sin(shakeT * 2.3f), 0f) * ShakeMaxOffset * sh;
            roll = ShakeMaxRoll * sh * Mathf.Sin(shakeT * 1.1f);
        }
        cam.transform.SetPositionAndRotation(camBasePos + camBaseRot * off, camBaseRot * Quaternion.Euler(0f, 0f, roll));
    }

    // Qisqa "muzlash" zarbasi. Faqat eng oxirgi chaqiruv vaqtni tiklaydi (ustma-ust zarbalar uchun)
    System.Collections.IEnumerator HitStop(float duration, float slow)
    {
        int myId = ++hitStopId;
        Time.timeScale = slow;
        yield return new WaitForSecondsRealtime(duration);
        if (myId == hitStopId) Time.timeScale = 1f;
    }

    void Flash(float alpha)
    {
        if (flashImg == null) return;
        flashA = Mathf.Max(flashA, alpha);
    }

    void UpdateJuice(float dt)
    {
        float real = Time.unscaledDeltaTime;

        // cho'zilish/yassilanish: so'nuvchi prujina, ozgina oshib ketib joyiga qaytadi
        squashVel += (-320f * squash - 16f * squashVel) * dt;
        squash += squashVel * dt;
        if (Mathf.Abs(squash) < 0.002f && Mathf.Abs(squashVel) < 0.02f) { squash = 0f; squashVel = 0f; }

        // olingan tanga: kattalashib yuqoriga sakraydi, keyin yo'qoladi
        for (int i = coinPops.Count - 1; i >= 0; i--)
        {
            var cpp = coinPops[i];
            if (cpp.t == null) { coinPops.RemoveAt(i); continue; }
            cpp.age += dt;
            float k = cpp.age / 0.22f;
            if (k >= 1f) { cpp.t.gameObject.SetActive(false); cpp.t.localScale = Vector3.one; coinPops.RemoveAt(i); continue; }
            float sc = k < 0.35f ? Mathf.Lerp(1f, 1.6f, k / 0.35f) : Mathf.Lerp(1.6f, 0f, (k - 0.35f) / 0.65f);
            cpp.t.localScale = Vector3.one * sc;
            cpp.t.position += Vector3.up * 5f * dt;
            cpp.t.Rotate(0, 900f * dt, 0);
        }

        // tanga hisoblagichi "sakraydi"
        if (coinPill != null)
        {
            coinPunch = Mathf.MoveTowards(coinPunch, 0f, real * 6f);
            coinPill.localScale = Vector3.one * (1f + 0.18f * EaseOutBack(coinPunch));
        }

        // oq chaqnash (bir martalik, tez so'nadi)
        if (flashImg != null)
        {
            flashA = Mathf.MoveTowards(flashA, 0f, real * 2.5f);
            flashImg.color = new Color(1f, 1f, 1f, flashA);
            flashImg.enabled = flashA > 0.001f;
        }
    }

    static float EaseOutBack(float t)
    {
        const float c1 = 1.70158f, c3 = c1 + 1f;
        float u = t - 1f;
        return 1f + c3 * u * u * u + c1 * u * u;
    }

    // Segmentdagi barcha oddiy obyektlarni material bo'yicha bitta meshga birlashtiradi
    void Combine(Seg s)
    {
        var groups = new Dictionary<Material, List<CombineInstance>>();
        var old = new List<GameObject>();
        Matrix4x4 inv = s.root.transform.worldToLocalMatrix;
        foreach (var mf in s.root.GetComponentsInChildren<MeshFilter>())
        {
            if (mf.transform.parent != null && (mf.transform.parent.name == "Coin" || mf.transform.parent.name == "Mover")) continue; // aylanadi yoki yuradi
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
    static readonly Color Gold = new Color(1f, 0.78f, 0.18f);

    // tayyor materiallar (tekstura o'lchami metrda)
    Material MBrick(Color c) { return Mat(c, Tex.Brick, 2f, 0.08f); }
    Material MGirih() { return Mat(Color.white, Tex.Girih, 1.4f, 0.35f); }
    Material MBand(float h) { return Mat(Color.white, Tex.Band, h, 0.4f, 0f, 0f, 0.5f); }
    Material MWood(Color c) { return Mat(c, Tex.Wood, 2f, 0.1f); }
    Material MGravel(Color c) { return Mat(c, Tex.Gravel, 1.6f, 0.04f); }
    Material MPaving(Color c) { return Mat(c, Tex.Paving, 2f, 0.1f); }
    Material MDome(Color c) { return Mat(c, Tex.DomeTiles, 0.9f, 0.5f); }
    Material MMetal(Color c) { return Mat(c, null, 1f, 0.7f, 0.35f); }
    Material MGlass() { return Mat(C(0.1f, 0.17f, 0.28f), null, 1f, 0.92f); }
    Material MGlow(Color c, float e) { return Mat(c, null, 1f, 0.3f, 0f, e); }

    void SpawnSeg()
    {
        var s = new Seg { z = nextSegZ, root = new GameObject("Segment") };
        var tr = s.root.transform;
        float mid = s.z + SegLen / 2f;
        int zone = ZoneAt(s.z);

        // keng yer (uzoqdagi binolar shu ustida turadi): har hududda o'z rangi
        Material ground = zone == 2 ? Mat(C(0.45f, 0.66f, 0.32f), Tex.Gravel, 4f, 0.05f)
                        : zone == 3 ? MPaving(C(0.66f, 0.68f, 0.7f))
                        : zone == 1 ? MPaving(C(0.88f, 0.74f, 0.52f))
                        : MPaving(C(0.82f, 0.72f, 0.56f));
        RB(new Vector3(0, -0.45f, mid), new Vector3(160f, 0.5f, SegLen), 0.05f, ground, tr);
        // temir yo'l to'shamasi: shag'al
        RB(new Vector3(0, -0.25f, mid), new Vector3(9.4f, 0.5f, SegLen), 0.05f, MGravel(C(0.6f, 0.52f, 0.44f)), tr);

        // har bir qator: ko'tarilgan shag'al, yog'och shpallar, po'lat relslar
        for (int l = 0; l < 3; l++)
        {
            float x = (l - 1) * LaneW;
            RB(new Vector3(x, -0.02f, mid), new Vector3(2.4f, 0.12f, SegLen), 0.05f, MGravel(C(0.78f, 0.74f, 0.68f)), tr);
            for (float dz = 0.6f; dz < SegLen; dz += 1.5f)
                RB(new Vector3(x, 0.08f, s.z + dz), new Vector3(2.1f, 0.1f, 0.34f), 0.03f, MWood(C(0.6f, 0.4f, 0.24f)), tr);
            foreach (float sx in new[] { -0.7f, 0.7f })
            {
                RB(new Vector3(x + sx, 0.14f, mid), new Vector3(0.14f, 0.06f, SegLen), 0.02f, C(0.35f, 0.3f, 0.28f), tr);  // rels tagi
                RB(new Vector3(x + sx, 0.2f, mid), new Vector3(0.1f, 0.08f, SegLen), 0.03f, MMetal(C(0.82f, 0.84f, 0.9f)), tr);
            }
        }

        // kontakt tarmog'i: yon ustunlar, konsol va simlar
        foreach (int side in new[] { -1, 1 })
        {
            float zz = s.z + 3f;
            Prim(PrimitiveType.Cylinder, new Vector3(side * 4.75f, 3.3f, zz), new Vector3(0.18f, 3.3f, 0.18f), C(0.3f, 0.32f, 0.36f), tr);
            RB(new Vector3(side * 3.3f, 6.3f, zz), new Vector3(3.0f, 0.1f, 0.1f), 0.03f, C(0.3f, 0.32f, 0.36f), tr);
            var brace = RB(new Vector3(side * 4.0f, 5.75f, zz), new Vector3(1.6f, 0.06f, 0.06f), 0.02f, C(0.3f, 0.32f, 0.36f), tr);
            brace.transform.localRotation = Quaternion.Euler(0, 0, side * -40f);
        }

        // yon tomonlar: bordyur, tosh yo'lak, chiroq, daraxt, binolar
        foreach (int side in new[] { -1, 1 })
        {
            RB(new Vector3(side * 4.85f, 0.12f, mid), new Vector3(0.5f, 0.34f, SegLen), 0.06f, C(0.88f, 0.85f, 0.8f), tr);
            Material walk = zone == 2 ? MGravel(C(0.72f, 0.6f, 0.44f))
                          : zone == 3 ? MPaving(C(0.82f, 0.83f, 0.86f))
                          : zone == 1 ? MPaving(C(0.95f, 0.82f, 0.62f))
                          : MPaving(C(0.95f, 0.86f, 0.72f));
            RB(new Vector3(side * 8.1f, 0.08f, mid), new Vector3(6.1f, 0.26f, SegLen), 0.05f, walk, tr);
            SideDressing(tr, s, side, zone, mid);
        }
        int rs = Random.value < 0.5f ? -1 : 1;
        if (zone == 0 && segIndex % 7 == 2) Tower(tr, rs * 18f, s.z + 4f, 1.2f);
        if (zone == 1 && segIndex % 5 == 2) // Buxoroning katta minorasi
        {
            Obj(MeshKit.Minaret(), new Vector3(rs * 22f, 0, s.z + 6f), new Vector3(2.4f, 2.4f, 2.4f), MBrick(C(0.86f, 0.68f, 0.46f)), tr);
            foreach (float by in new[] { 9.6f, 19.2f, 27.4f })
                Prim(PrimitiveType.Cylinder, new Vector3(rs * 22f, by, s.z + 6f), new Vector3(5.6f, 0.5f, 5.6f) * (1f - by / 60f), TileBlue, tr);
        }
        if (zone == 3 && segIndex % 6 == 1) TvTower(tr, rs * 55f, s.z + 10f);
        if (zone != 2 && segIndex % 8 == 3) Gantry(tr, mid);

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
            case ObsType.Low: // ustidan sakrab o'tish kerak: to'siq, yashiklar yoki bochkalar (to'qnashuv qutisi bir xil)
                int lv = Random.Range(0, 3);
                if (lv == 1)
                {
                    Crate(tr, new Vector3(x - 0.5f, 0.4f, zz), 0.8f);
                    Crate(tr, new Vector3(x + 0.5f, 0.4f, zz), 0.8f);
                    s.obs.Add(new Obs { c = new Vector3(x, 0.4f, zz), h = new Vector3(1f, 0.4f, 0.4f) });
                    break;
                }
                if (lv == 2)
                {
                    foreach (float bx in new[] { -0.68f, 0f, 0.68f }) Barrel(tr, new Vector3(x + bx, 0.2f, zz));
                    TrafficCone(tr, new Vector3(x + (Random.value < 0.5f ? -1.05f : 1.05f), 0f, zz - 0.6f));
                    s.obs.Add(new Obs { c = new Vector3(x, 0.4f, zz), h = new Vector3(1f, 0.4f, 0.35f) });
                    break;
                }
                Stripes(tr, x, 0.6f, zz, 2f, 0.45f);
                foreach (float sx in new[] { -0.85f, 0.85f })
                {
                    RB(new Vector3(x + sx, 0.3f, zz), new Vector3(0.12f, 0.6f, 0.12f), 0.05f, MMetal(C(0.75f, 0.77f, 0.8f)), tr);
                    RB(new Vector3(x + sx, 0.04f, zz), new Vector3(0.18f, 0.08f, 0.6f), 0.03f, C(0.25f, 0.25f, 0.28f), tr);
                }
                Prim(PrimitiveType.Sphere, new Vector3(x - 0.85f, 0.92f, zz), Vector3.one * 0.14f, Color.white, tr).GetComponent<MeshRenderer>().sharedMaterial = MGlow(C(1f, 0.6f, 0.1f), 2f);
                Prim(PrimitiveType.Sphere, new Vector3(x + 0.85f, 0.92f, zz), Vector3.one * 0.14f, Color.white, tr).GetComponent<MeshRenderer>().sharedMaterial = MGlow(C(1f, 0.6f, 0.1f), 2f);
                s.obs.Add(new Obs { c = new Vector3(x, 0.4f, zz), h = new Vector3(1f, 0.4f, 0.3f) });
                break;

            case ObsType.Tall: // poyezd vagoni: chetlab o'tish kerak
                Train(tr, x, zz);
                s.obs.Add(new Obs { c = new Vector3(x, 1.4f, zz), h = new Vector3(1f, 1.4f, 4f) });
                break;

            case ObsType.Overhead: // yuqoridagi to'sin: tagidan sirg'anib o'tish kerak
                Stripes(tr, x, 1.75f, zz, 2.1f, 0.5f);
                RB(new Vector3(x, 2.05f, zz), new Vector3(2.1f, 0.1f, 0.2f), 0.03f, MMetal(C(0.75f, 0.77f, 0.8f)), tr);
                foreach (float sx in new[] { -1.0f, 1.0f })
                {
                    RB(new Vector3(x + sx, 1.05f, zz), new Vector3(0.14f, 2.1f, 0.14f), 0.05f, MMetal(C(0.75f, 0.77f, 0.8f)), tr);
                    RB(new Vector3(x + sx, 0.04f, zz), new Vector3(0.2f, 0.08f, 0.6f), 0.03f, C(0.25f, 0.25f, 0.28f), tr);
                }
                s.obs.Add(new Obs { c = new Vector3(x, 1.7f, zz), h = new Vector3(1f, 0.3f, 0.25f) });
                break;
        }
    }

    // Metro vagoni: oq korpus, ko'k naqshli lenta, derazalar, eshiklar, old tomonda chiroqlar.
    // Old tomoni -Z ga (o'yinchiga) qaragan; to'qnashuv qutisi o'zgarmagan (2 x 2.8 x 8).
    void Train(Transform tr, float x, float zz)
    {
        var body = Mat(C(0.95f, 0.96f, 0.98f), null, 1f, 0.55f);
        var glass = MGlass();
        var steel = MMetal(C(0.7f, 0.73f, 0.78f));
        var dark = C(0.16f, 0.17f, 0.2f);

        // ostki qism va g'ildirak aravachalari
        RB(new Vector3(x, 0.3f, zz), new Vector3(1.7f, 0.35f, 7.2f), 0.08f, dark, tr);
        foreach (float bz in new[] { -2.6f, 2.6f })
            RB(new Vector3(x, 0.32f, zz + bz), new Vector3(1.9f, 0.45f, 1.6f), 0.1f, C(0.22f, 0.22f, 0.25f), tr);

        // korpus va tom
        RB(new Vector3(x, 1.55f, zz), new Vector3(2f, 2.3f, 8f), 0.35f, body, tr);
        RB(new Vector3(x, 2.78f, zz), new Vector3(1.7f, 0.22f, 7.4f), 0.1f, steel, tr);
        RB(new Vector3(x, 2.95f, zz + 1f), new Vector3(0.9f, 0.18f, 1.6f), 0.06f, steel, tr); // konditsioner

        // naqshli ko'k lenta va chiziqlar
        RB(new Vector3(x, 0.85f, zz), new Vector3(2.04f, 0.7f, 7.86f), 0.22f, MBand(0.7f), tr);
        RB(new Vector3(x, 1.27f, zz), new Vector3(2.05f, 0.08f, 7.9f), 0.03f, TileBlue, tr);
        RB(new Vector3(x, 2.4f, zz), new Vector3(2.05f, 0.06f, 7.6f), 0.02f, TileBlue, tr);

        // yon derazalar va eshiklar
        foreach (float wz in new[] { -2.85f, 0f, 2.85f })
            RB(new Vector3(x, 1.85f, zz + wz), new Vector3(2.06f, 0.72f, 1.15f), 0.1f, glass, tr);
        foreach (float dz in new[] { -1.45f, 1.45f })
        {
            RB(new Vector3(x, 1.35f, zz + dz), new Vector3(2.06f, 1.9f, 0.95f), 0.08f, C(0.8f, 0.83f, 0.88f), tr);
            RB(new Vector3(x, 1.75f, zz + dz - 0.22f), new Vector3(2.08f, 0.75f, 0.32f), 0.06f, glass, tr);
            RB(new Vector3(x, 1.75f, zz + dz + 0.22f), new Vector3(2.08f, 0.75f, 0.32f), 0.06f, glass, tr);
        }

        // old tomon: oyna, naqsh, chiroqlar, belgi, yo'nalish tablosi
        float fz = zz - 4f;
        RB(new Vector3(x, 1.95f, fz), new Vector3(1.6f, 0.8f, 0.12f), 0.12f, glass, tr);
        RB(new Vector3(x, 0.85f, fz + 0.02f), new Vector3(1.86f, 0.7f, 0.12f), 0.08f, MBand(0.7f), tr);
        RB(new Vector3(x, 2.5f, fz - 0.01f), new Vector3(0.9f, 0.18f, 0.08f), 0.03f, MGlow(C(1f, 0.6f, 0.15f), 1.6f), tr);
        foreach (float hx in new[] { -0.62f, 0.62f })
        {
            Prim(PrimitiveType.Sphere, new Vector3(x + hx, 1.35f, fz - 0.03f), new Vector3(0.26f, 0.2f, 0.1f), Color.white, tr)
                .GetComponent<MeshRenderer>().sharedMaterial = MGlow(C(1f, 0.95f, 0.75f), 2.2f);
        }
        var emb = RB(new Vector3(x, 1.38f, fz - 0.03f), new Vector3(0.26f, 0.26f, 0.06f), 0.03f, Mat(Gold, null, 1f, 0.7f, 0.3f), tr);
        emb.transform.localRotation = Quaternion.Euler(0, 0, 45);
        RB(new Vector3(x, 0.32f, fz - 0.05f), new Vector3(1.6f, 0.2f, 0.12f), 0.05f, C(0.85f, 0.75f, 0.1f), tr); // bufer
    }

    void AddCoins(Seg s, float x, float zz)
    {
        var rim = Mat(C(0.9f, 0.5f, 0.05f), null, 1f, 0.75f, 0.3f, 0.15f);
        var gold = Mat(Gold, null, 1f, 0.8f, 0.3f, 0.3f);
        var face = Mat(C(1f, 0.95f, 0.6f), null, 1f, 0.8f, 0.2f, 0.45f);
        int pk = segIndex >= 4 && Random.value < 0.16f ? Random.Range(0, 3) : -1;
        for (int i = -2; i <= 2; i++)
        {
            Vector3 pos = new Vector3(x, 1f, zz + i * 2f);
            if (i == 0 && pk >= 0) { AddPickup(s, pos, pk); continue; }
            var holder = new GameObject("Coin").transform;
            holder.SetParent(s.root.transform);
            holder.position = pos;
            var edge = Prim(PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.74f, 0.045f, 0.74f), Gold, holder);
            edge.GetComponent<MeshRenderer>().sharedMaterial = rim;
            edge.transform.localRotation = Quaternion.Euler(90, 0, 0);
            var disc = Prim(PrimitiveType.Cylinder, Vector3.zero, new Vector3(0.58f, 0.07f, 0.58f), Gold, holder);
            disc.GetComponent<MeshRenderer>().sharedMaterial = gold;
            disc.transform.localRotation = Quaternion.Euler(90, 0, 0);
            var star = Obj(MeshKit.RoundedBox(new Vector3(0.26f, 0.26f, 0.16f), 0.04f), Vector3.zero, Vector3.one, face, holder);
            star.transform.localRotation = Quaternion.Euler(0, 0, 45);
            s.coins.Add(new Coin { t = holder, c = pos });
        }
    }

    // qizil-oq chiziqli to'sin taxtasi
    void Stripes(Transform tr, float x, float y, float zz, float w, float h)
    {
        int n = 5;
        var red = Mat(Red, null, 1f, 0.45f);
        var white = Mat(White, null, 1f, 0.45f);
        for (int i = 0; i < n; i++)
            RB(new Vector3(x - w / 2f + w / n * (i + 0.5f), y, zz), new Vector3(w / n, h, 0.16f), 0.05f, i % 2 == 0 ? red : white, tr);
    }

    // ---------------- Hududlar: har bosqichda yangi joy ----------------
    // 0 Samarqand (kunduz), 1 Buxoro (quyosh botishi), 2 Tog'lar (shom), 3 Toshkent (tun) -- osmon palitrasi bilan mos
    static readonly string[] ZoneNames = { "SAMARQAND", "BUXORO", "TOG'LAR", "TOSHKENT" };
    static int ZoneAt(float zz) { return ((int)(Mathf.Max(0f, zz) / StageLen)) % 4; }

    void SideDressing(Transform tr, Seg s, int side, int zone, float mid)
    {
        switch (zone)
        {
            case 0: // Samarqand: madrasalar, gumbazlar, bozor
                Lamp(tr, side * 5.4f, mid, side);
                Tree(tr, side * Random.Range(6.6f, 8.2f), s.z + Random.Range(2f, SegLen - 2f));
                switch (Random.Range(0, 5))
                {
                    case 0: Madrasa(tr, side * 13f, mid, side); break;
                    case 1: DomeHall(tr, side * 13.5f, mid, side); break;
                    case 2: Stall(tr, side * 8.8f, mid, side); House(tr, side * 13f, mid, side); break;
                    default: House(tr, side * 12.5f, mid, side); break;
                }
                StreetProp(tr, side, s.z + Random.Range(1f, SegLen - 1f), zone);
                if (Random.value < 0.7f) FarSkyline(tr, side, s.z + Random.Range(0f, SegLen));
                break;

            case 1: // Buxoro: loy-g'isht uylar, qal'a devori, katta minora, bozor
                Lamp(tr, side * 5.4f, mid, side);
                if (Random.value < 0.5f) Tree(tr, side * Random.Range(6.6f, 8.2f), s.z + Random.Range(2f, SegLen - 2f));
                switch (Random.Range(0, 4))
                {
                    case 0: FortWall(tr, side, mid); break;
                    case 1: Stall(tr, side * 8.8f, mid - 5f, side); Stall(tr, side * 8.8f, mid + 5f, side); MudHouse(tr, side * 13f, mid, side); break;
                    default: MudHouse(tr, side * 12.5f, mid - 4f, side); MudHouse(tr, side * 13.5f, mid + 6f, side); break;
                }
                StreetProp(tr, side, s.z + Random.Range(1f, SegLen - 1f), zone);
                if (Random.value < 0.6f) FarSkyline(tr, side, s.z + Random.Range(0f, SegLen));
                break;

            case 2: // Tog'lar: archalar, toshlar, yog'och panjara, uzoqda qorli cho'qqilar
                Fence(tr, side * 5.4f, s.z, SegLen);
                for (int i = 0; i < 4; i++)
                    Pine(tr, side * Random.Range(7f, 16f), s.z + Random.Range(0f, SegLen), Random.Range(0.8f, 1.5f));
                if (Random.value < 0.6f) Rock(tr, side * Random.Range(6.5f, 12f), s.z + Random.Range(0f, SegLen), Random.Range(0.6f, 1.4f));
                if (Random.value < 0.35f) Crate(tr, new Vector3(side * 6.4f, 0.4f, s.z + Random.Range(2f, SegLen - 2f)), 0.8f);
                if (Random.value < 0.6f) Mountain(tr, side, 30f, s.z + Random.Range(0f, SegLen), Random.Range(30f, 50f));
                if (Random.value < 0.4f) Mountain(tr, side, 18f, s.z + Random.Range(0f, SegLen), Random.Range(12f, 18f));
                break;

            default: // Toshkent: shisha osmono'par binolar, ko'p qavatli uylar, skameyka, bayroqlar
                Lamp(tr, side * 5.4f, mid, side);
                Tree(tr, side * Random.Range(6.6f, 8.2f), s.z + Random.Range(2f, SegLen - 2f));
                CityRoad(tr, s, side, mid);
                if (Random.value < 0.55f) GlassTower(tr, side * Random.Range(22f, 24f), mid, Random.Range(16f, 38f));
                else Apartment(tr, side * 19.5f, mid, side);
                StreetProp(tr, side, s.z + Random.Range(1f, SegLen - 1f), zone);
                if (Random.value < 0.7f) GlassTower(tr, side * Random.Range(30f, 45f), s.z + Random.Range(0f, SegLen), Random.Range(30f, 60f));
                break;
        }
    }

    // yo'lak bo'yidagi kichik narsalar: skameyka, gul tuvak, bayroq, ko'rsatkich, bochka
    void StreetProp(Transform tr, int side, float z, int zone)
    {
        float x = side * 6.3f;
        int r = Random.Range(0, 5);
        if (zone == 1 && r >= 3) { Barrel(tr, new Vector3(x, 0.2f, z)); Barrel(tr, new Vector3(x + side * 0.6f, 0.2f, z + 0.5f)); return; }
        switch (r)
        {
            case 0: Bench(tr, x, z, side); break;
            case 1: Planter(tr, x, z); break;
            case 2: FlagPole(tr, side * 9.6f, z); break;
            case 3: if (zone == 3) Signboard(tr, side * 6.0f, z, side); else Planter(tr, x, z); break;
            default: Bench(tr, x, z, side); Planter(tr, x, z + 1.6f); break;
        }
    }

    // ---------------- Rekvizitlar ----------------
    void Bench(Transform tr, float x, float z, int side)
    {
        var wood = MWood(C(0.68f, 0.45f, 0.26f));
        var iron = MMetal(C(0.15f, 0.16f, 0.18f));
        RB(new Vector3(x, 0.5f, z), new Vector3(0.55f, 0.08f, 1.7f), 0.03f, wood, tr);
        RB(new Vector3(x + side * 0.25f, 0.85f, z), new Vector3(0.07f, 0.4f, 1.7f), 0.03f, wood, tr);
        foreach (float dz in new[] { -0.7f, 0.7f })
        {
            RB(new Vector3(x, 0.26f, z + dz), new Vector3(0.5f, 0.5f, 0.07f), 0.02f, iron, tr);
            RB(new Vector3(x + side * 0.25f, 0.68f, z + dz), new Vector3(0.06f, 0.5f, 0.07f), 0.02f, iron, tr);
        }
    }

    void Planter(Transform tr, float x, float z)
    {
        RB(new Vector3(x, 0.4f, z), new Vector3(1f, 0.5f, 1f), 0.08f, Mat(C(0.92f, 0.88f, 0.8f), null, 1f, 0.2f), tr);
        RB(new Vector3(x, 0.66f, z), new Vector3(0.86f, 0.04f, 0.86f), 0.02f, C(0.35f, 0.25f, 0.18f), tr);
        Prim(PrimitiveType.Sphere, new Vector3(x, 0.8f, z), new Vector3(0.85f, 0.4f, 0.85f), C(0.2f, 0.55f, 0.2f), tr);
        Color[] fl = { C(0.95f, 0.2f, 0.25f), C(1f, 0.8f, 0.15f), C(1f, 0.5f, 0.7f), Color.white };
        for (int i = 0; i < 7; i++)
        {
            float a = i * 0.9f;
            Prim(PrimitiveType.Sphere, new Vector3(x + Mathf.Cos(a) * 0.28f, 0.95f + (i % 2) * 0.05f, z + Mathf.Sin(a) * 0.28f),
                Vector3.one * 0.16f, fl[i % fl.Length], tr);
        }
    }

    void FlagPole(Transform tr, float x, float z)
    {
        Prim(PrimitiveType.Cylinder, new Vector3(x, 3f, z), new Vector3(0.1f, 3f, 0.1f), C(0.85f, 0.86f, 0.9f), tr);
        Prim(PrimitiveType.Sphere, new Vector3(x, 6.05f, z), Vector3.one * 0.18f, Gold, tr);
        float fz = z + 0.9f;
        RB(new Vector3(x, 5.55f, fz), new Vector3(0.05f, 0.36f, 1.7f), 0.02f, C(0.1f, 0.6f, 0.9f), tr);
        RB(new Vector3(x, 5.35f, fz), new Vector3(0.06f, 0.04f, 1.7f), 0.01f, Red, tr);
        RB(new Vector3(x, 5.17f, fz), new Vector3(0.05f, 0.36f, 1.7f), 0.02f, White, tr);
        RB(new Vector3(x, 4.99f, fz), new Vector3(0.06f, 0.04f, 1.7f), 0.01f, Red, tr);
        RB(new Vector3(x, 4.79f, fz), new Vector3(0.05f, 0.36f, 1.7f), 0.02f, C(0.1f, 0.65f, 0.25f), tr);
    }

    // ko'k yo'l ko'rsatkichi (oq strelka bilan)
    void Signboard(Transform tr, float x, float z, int side)
    {
        foreach (float dz in new[] { -0.8f, 0.8f })
            Prim(PrimitiveType.Cylinder, new Vector3(x, 1.3f, z + dz), new Vector3(0.08f, 1.3f, 0.08f), C(0.6f, 0.62f, 0.66f), tr);
        RB(new Vector3(x, 2.4f, z), new Vector3(0.1f, 0.9f, 2f), 0.04f, Mat(C(0.08f, 0.38f, 0.8f), null, 1f, 0.5f), tr);
        RB(new Vector3(x - side * 0.04f, 2.4f, z), new Vector3(0.1f, 0.78f, 1.88f), 0.03f, Mat(White, null, 1f, 0.5f), tr);
        RB(new Vector3(x - side * 0.07f, 2.4f, z), new Vector3(0.1f, 0.7f, 1.8f), 0.03f, Mat(C(0.08f, 0.38f, 0.8f), null, 1f, 0.5f), tr);
        RB(new Vector3(x - side * 0.1f, 2.4f, z - 0.1f), new Vector3(0.06f, 0.12f, 0.9f), 0.02f, White, tr);
        var h1 = RB(new Vector3(x - side * 0.1f, 2.5f, z + 0.3f), new Vector3(0.06f, 0.1f, 0.35f), 0.02f, White, tr);
        h1.transform.localRotation = Quaternion.Euler(40, 0, 0);
        var h2 = RB(new Vector3(x - side * 0.1f, 2.3f, z + 0.3f), new Vector3(0.06f, 0.1f, 0.35f), 0.02f, White, tr);
        h2.transform.localRotation = Quaternion.Euler(-40, 0, 0);
    }

    void Crate(Transform tr, Vector3 pos, float size)
    {
        RB(pos, Vector3.one * size, 0.04f, MWood(C(0.8f, 0.56f, 0.32f)), tr);
        var dark = MWood(C(0.55f, 0.36f, 0.2f));
        float h = size * 0.5f + 0.01f;
        // qirralardagi to'q taxtalar (old va orqa) va diagonal
        foreach (float fz in new[] { -h, h })
        {
            RB(pos + new Vector3(0, size * 0.42f, fz), new Vector3(size, size * 0.14f, 0.04f), 0.01f, dark, tr);
            RB(pos + new Vector3(0, -size * 0.42f, fz), new Vector3(size, size * 0.14f, 0.04f), 0.01f, dark, tr);
            var d = RB(pos + new Vector3(0, 0, fz), new Vector3(size * 1.15f, size * 0.12f, 0.04f), 0.01f, dark, tr);
            d.transform.localRotation = Quaternion.Euler(0, 0, 45);
        }
    }

    void Barrel(Transform tr, Vector3 pos)
    {
        Obj(MeshKit.Barrel(), pos - new Vector3(0, 0.2f, 0), Vector3.one, MWood(C(0.7f, 0.45f, 0.25f)), tr);
        var band = C(0.3f, 0.3f, 0.33f);
        Prim(PrimitiveType.Cylinder, pos + new Vector3(0, 0.0f, 0), new Vector3(0.67f, 0.03f, 0.67f), band, tr);
        Prim(PrimitiveType.Cylinder, pos + new Vector3(0, 0.44f, 0), new Vector3(0.67f, 0.03f, 0.67f), band, tr);
    }

    void TrafficCone(Transform tr, Vector3 pos)
    {
        RB(pos + new Vector3(0, 0.03f, 0), new Vector3(0.55f, 0.06f, 0.55f), 0.02f, C(0.2f, 0.2f, 0.22f), tr);
        Obj(MeshKit.Cone(), pos, Vector3.one, Mat(C(1f, 0.45f, 0.08f), null, 1f, 0.4f), tr);
        Prim(PrimitiveType.Cylinder, pos + new Vector3(0, 0.36f, 0), new Vector3(0.27f, 0.05f, 0.27f), White, tr);
    }

    void Pine(Transform tr, float x, float z, float s)
    {
        Prim(PrimitiveType.Cylinder, new Vector3(x, 0.8f * s, z), new Vector3(0.35f, 0.8f, 0.35f) * s, C(0.4f, 0.26f, 0.15f), tr);
        Color[] g = { C(0.1f, 0.38f, 0.2f), C(0.13f, 0.44f, 0.22f), C(0.17f, 0.5f, 0.25f) };
        for (int i = 0; i < 3; i++)
        {
            float w = (1.7f - i * 0.4f) * s;
            Obj(MeshKit.PineTier(), new Vector3(x, (1.2f + i * 1.3f) * s, z), new Vector3(w, 2.4f * s * (1f - i * 0.12f), w), Mat(g[i], null, 1f, 0.1f), tr);
        }
    }

    void Rock(Transform tr, float x, float z, float s)
    {
        var m = Mat(C(0.58f, 0.58f, 0.6f), Tex.Gravel, 3f, 0.1f);
        Obj(PrimMesh(PrimitiveType.Sphere), new Vector3(x, 0.3f * s, z), new Vector3(1.6f, 1f, 1.3f) * s, m, tr);
        Obj(PrimMesh(PrimitiveType.Sphere), new Vector3(x + 0.6f * s, 0.2f * s, z + 0.5f * s), new Vector3(0.9f, 0.7f, 0.8f) * s, m, tr);
    }

    void Fence(Transform tr, float x, float z0, float len)
    {
        var wood = MWood(C(0.6f, 0.4f, 0.24f));
        for (float dz = 1f; dz < len; dz += 3f)
            RB(new Vector3(x, 0.55f, z0 + dz), new Vector3(0.14f, 1.1f, 0.14f), 0.03f, wood, tr);
        RB(new Vector3(x, 0.85f, z0 + len / 2f), new Vector3(0.08f, 0.12f, len), 0.03f, wood, tr);
        RB(new Vector3(x, 0.45f, z0 + len / 2f), new Vector3(0.08f, 0.12f, len), 0.03f, wood, tr);
    }

    // gap: yo'l chetidan tog' etagigacha bo'lgan masofa (tog' yo'lga yopishib qolmasligi uchun)
    void Mountain(Transform tr, int side, float gap, float z, float h)
    {
        float w = h * Random.Range(0.8f, 1.1f);
        float x = side * (gap + w);
        Obj(MeshKit.Mountain(), new Vector3(x, -0.5f, z), new Vector3(w, h, w), Mat(C(0.48f, 0.52f, 0.6f), null, 1f, 0.05f), tr);
        Obj(MeshKit.SnowCap(), new Vector3(x, -0.5f, z), new Vector3(w, h, w), Mat(C(0.96f, 0.97f, 1f), null, 1f, 0.3f), tr);
        // etagidagi archalar
        for (int i = 0; i < 3; i++)
            Pine(tr, x + Random.Range(-0.7f, 0.7f) * w, z + Random.Range(-0.7f, 0.7f) * w, Random.Range(1.5f, 2.5f));
    }

    // Buxoro uyi: tekis tomli loy-g'isht, chiqib turgan to'sin uchlari, kichik gumbaz, yog'och eshik
    void MudHouse(Transform tr, float x, float z, int side)
    {
        Vector3 dir = new Vector3(-side, 0, 0);
        float hh = Random.Range(4f, 6.5f);
        Color c = Color.HSVToRGB(Random.Range(0.08f, 0.1f), Random.Range(0.35f, 0.45f), Random.Range(0.82f, 0.92f));
        RB(new Vector3(x, hh / 2f, z), new Vector3(5f, hh, 8f), 0.3f, MBrick(c), tr);
        RB(new Vector3(x, hh + 0.2f, z), new Vector3(5.2f, 0.4f, 8.2f), 0.12f, MBrick(c * 0.95f), tr);
        float fx = x - side * 2.5f;
        for (float bz = -3.5f; bz <= 3.6f; bz += 0.7f)
            RB(new Vector3(fx - side * 0.15f, hh - 0.35f, z + bz), new Vector3(0.35f, 0.14f, 0.14f), 0.03f, MWood(C(0.5f, 0.33f, 0.2f)), tr);
        ArchObj(tr, new Vector3(fx - side * 0.02f, 0, z), 1.8f, 2.9f, MBrick(C(0.95f, 0.85f, 0.66f)), dir);
        ArchObj(tr, new Vector3(fx - side * 0.04f, 0, z), 1.4f, 2.5f, MWood(C(0.48f, 0.3f, 0.16f)), dir);
        foreach (float wz in new[] { -2.6f, 2.6f })
        {
            ArchObj(tr, new Vector3(fx - side * 0.02f, 1.6f, z + wz), 0.9f, 1.3f, MGirih(), dir);
            ArchObj(tr, new Vector3(fx - side * 0.04f, 1.7f, z + wz), 0.6f, 1.0f, Navy, dir);
        }
        if (Random.value < 0.5f)
            Obj(MeshKit.Dome(), new Vector3(x + side * 0.8f, hh + 0.3f, z + 1.5f), Vector3.one * 0.5f, Random.value < 0.5f ? MBrick(c) : MDome(Teal), tr);
    }

    // Ark qal'asi uslubidagi devor: qiya devor, tepasida tishlar, dumaloq minoralar
    void FortWall(Transform tr, int side, float mid)
    {
        var wall = MBrick(C(0.86f, 0.7f, 0.5f));
        float x = side * 15f;
        var w = RB(new Vector3(x, 4.2f, mid), new Vector3(4f, 9f, SegLen), 0.2f, wall, tr);
        w.transform.localRotation = Quaternion.Euler(0, 0, side * 7f);
        float topX = x + side * 0.55f;
        for (float dz = -SegLen / 2f + 0.8f; dz < SegLen / 2f; dz += 1.6f)
            RB(new Vector3(topX - side * 1.6f, 9f, mid + dz), new Vector3(0.6f, 0.9f, 0.8f), 0.08f, wall, tr);
        foreach (float dz in new[] { -8f, 8f })
            Obj(MeshKit.FortTower(), new Vector3(x - side * 1.2f, 0, mid + dz), new Vector3(2.4f, 10.5f, 2.4f), wall, tr);
    }

    // Toshkent: shisha osmono'par bino (kechasi derazalari yonadi)
    void GlassTower(Transform tr, float x, float z, float h)
    {
        float w = Random.Range(7f, 10f), d = Random.Range(8f, 12f);
        Color tint = Random.value < 0.5f ? C(0.75f, 0.9f, 1f) : C(0.85f, 0.95f, 0.98f);
        RB(new Vector3(x, h / 2f, z), new Vector3(w, h, d), 0.3f, MWindows(tint), tr);
        RB(new Vector3(x, h + 0.3f, z), new Vector3(w + 0.3f, 0.6f, d + 0.3f), 0.1f, MMetal(C(0.75f, 0.78f, 0.82f)), tr);
        if (Random.value < 0.5f) // tepasidagi kichikroq qavat
            RB(new Vector3(x, h + 2.5f, z), new Vector3(w * 0.6f, 4f, d * 0.6f), 0.2f, MWindows(tint), tr);
        if (Random.value < 0.4f)
        {
            Prim(PrimitiveType.Cylinder, new Vector3(x, h + 5f, z), new Vector3(0.15f, 2.5f, 0.15f), C(0.8f, 0.8f, 0.82f), tr);
            Prim(PrimitiveType.Sphere, new Vector3(x, h + 7.6f, z), Vector3.one * 0.35f, Color.white, tr).GetComponent<MeshRenderer>().sharedMaterial = MGlow(C(1f, 0.2f, 0.15f), 3f);
        }
    }

    // Toshkent: ko'p qavatli turar joy, balkonlar va milliy naqshli lenta
    void Apartment(Transform tr, float x, float z, int side)
    {
        float h = Random.Range(10f, 16f);
        RB(new Vector3(x, h / 2f, z), new Vector3(6f, h, 14f), 0.2f, MWindows(C(1f, 0.93f, 0.82f)), tr);
        float fx = x - side * 3f;
        for (float by = 3f; by < h - 1f; by += 3f)
            RB(new Vector3(fx - side * 0.3f, by, z), new Vector3(0.6f, 0.12f, 13f), 0.04f, C(0.93f, 0.9f, 0.85f), tr);
        RB(new Vector3(fx - side * 0.05f, 1.2f, z), new Vector3(0.12f, 2.4f, 14f), 0.04f, MBand(2.4f), tr);
        RB(new Vector3(x, h + 0.2f, z), new Vector3(6.3f, 0.4f, 14.3f), 0.1f, C(0.85f, 0.8f, 0.72f), tr);
    }

    // uzoqdagi teleminora
    void TvTower(Transform tr, float x, float z)
    {
        Obj(MeshKit.TvTower(), new Vector3(x, 0, z), new Vector3(3f, 90f, 3f), Mat(C(0.92f, 0.93f, 0.96f), null, 1f, 0.5f), tr);
        Prim(PrimitiveType.Cylinder, new Vector3(x, 58.5f, z), new Vector3(7.2f, 1f, 7.2f), Color.white, tr).GetComponent<MeshRenderer>().sharedMaterial = MGlow(C(0.3f, 0.6f, 1f), 1.5f);
        Prim(PrimitiveType.Sphere, new Vector3(x, 90.5f, z), Vector3.one * 0.8f, Color.white, tr).GetComponent<MeshRenderer>().sharedMaterial = MGlow(C(1f, 0.2f, 0.15f), 3f);
    }

    Material MWindows(Color tint)
    {
        var m = Mat(tint, Tex.Windows, 6f, 0.75f);
        if (!m.IsKeywordEnabled("_EMISSION"))
        {
            m.EnableKeyword("_EMISSION");
            m.SetTexture("_EmissionMap", Tex.WindowsGlow);
            m.SetColor("_EmissionColor", Color.white * 0.9f);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        }
        return m;
    }

    // ---------------- Toshkent ko'chasi: yo'l, avtobus, taksi, yengil mashina ----------------
    class Mover { public Transform t; public float v; }

    // yo'lakdan keyin ikki tasmali asfalt yo'l; ichki tasma o'yinchi bilan bir yo'nalishda, tashqisi qarama-qarshi
    void CityRoad(Transform tr, Seg s, int side, float mid)
    {
        RB(new Vector3(side * 13.5f, -0.15f, mid), new Vector3(5f, 0.1f, SegLen), 0.02f, Mat(C(0.22f, 0.23f, 0.26f), Tex.Gravel, 1.2f, 0.25f), tr);
        RB(new Vector3(side * 16.2f, 0.05f, mid), new Vector3(0.4f, 0.3f, SegLen), 0.05f, C(0.85f, 0.85f, 0.82f), tr); // tashqi bordyur
        for (float dz = 1f; dz < SegLen; dz += 4f) // o'rtadagi uzuq chiziq
            RB(new Vector3(side * 13.5f, -0.09f, s.z + dz), new Vector3(0.15f, 0.02f, 2f), 0.01f, Mat(White, null, 1f, 0.3f), tr);
        int n = Random.Range(1, 3);
        for (int i = 0; i < n; i++)
        {
            bool inner = Random.value < 0.5f;
            float x = side * (inner ? 12.3f : 14.7f);
            float dir = inner ? 1f : -1f;
            float r = Random.value;
            int kind = r < 0.3f ? 0 : r < 0.75f ? 1 : 2;
            float v = dir * (kind == 0 ? Random.Range(7f, 10f) : Random.Range(9f, 15f));
            Vehicle(s, new Vector3(x, -0.1f, s.z + Random.Range(2f, SegLen - 2f)), kind, dir, v);
        }
    }

    // kind: 0 shahar avtobusi, 1 sariq taksi, 2 oq yengil mashina. Old tomoni +Z (dir = -1 bo'lsa aylantiriladi)
    void Vehicle(Seg s, Vector3 pos, int kind, float dir, float v)
    {
        var h = new GameObject("Mover").transform;
        h.SetParent(s.root.transform);
        h.position = pos;
        h.rotation = Quaternion.Euler(0, dir > 0 ? 0 : 180, 0);
        var glass = MGlass();
        var tyre = C(0.08f, 0.08f, 0.09f);
        var head = MGlow(C(1f, 0.95f, 0.8f), 2.2f);
        var tail = MGlow(C(1f, 0.1f, 0.08f), 2f);

        void Wheels(float wx, float wy, float r, float[] zs)
        {
            foreach (float wz in zs)
                foreach (float sx in new[] { -wx, wx })
                {
                    var w = Prim(PrimitiveType.Cylinder, new Vector3(sx, wy, wz), new Vector3(r * 2f, 0.12f, r * 2f), tyre, h);
                    w.transform.localRotation = Quaternion.Euler(0, 0, 90);
                    var hub = Prim(PrimitiveType.Cylinder, new Vector3(sx * 1.04f, wy, wz), new Vector3(r, 0.13f, r), C(0.75f, 0.77f, 0.8f), h);
                    hub.transform.localRotation = Quaternion.Euler(0, 0, 90);
                }
        }

        if (kind == 0) // avtobus: oq korpus, ko'k naqshli pastki qism, uzun oynalar, tablo
        {
            var body = Mat(C(0.95f, 0.96f, 0.98f), null, 1f, 0.55f);
            RB(new Vector3(0, 1.75f, 0), new Vector3(2.5f, 2.8f, 10f), 0.3f, body, h);
            RB(new Vector3(0, 0.75f, 0), new Vector3(2.54f, 0.7f, 9.9f), 0.2f, MBand(0.7f), h);
            RB(new Vector3(0, 2.15f, 0.3f), new Vector3(2.56f, 1.0f, 8.4f), 0.12f, glass, h);
            for (float pz = -3.6f; pz <= 4.2f; pz += 1.6f) // deraza oralig'idagi ustunlar
                RB(new Vector3(0, 2.15f, pz), new Vector3(2.58f, 1.0f, 0.12f), 0.03f, body, h);
            RB(new Vector3(0, 2.0f, 4.98f), new Vector3(2.2f, 1.5f, 0.1f), 0.1f, glass, h);         // old oyna
            RB(new Vector3(0, 2.95f, 4.97f), new Vector3(1.6f, 0.25f, 0.08f), 0.03f, MGlow(C(1f, 0.6f, 0.15f), 1.8f), h); // tablo
            RB(new Vector3(0, 3.25f, -1f), new Vector3(1.6f, 0.3f, 3f), 0.1f, MMetal(C(0.75f, 0.78f, 0.82f)), h);
            foreach (float sx in new[] { -0.85f, 0.85f })
            {
                Obj(PrimMesh(PrimitiveType.Sphere), new Vector3(sx, 0.9f, 5.0f), new Vector3(0.3f, 0.2f, 0.1f), head, h);
                Obj(PrimMesh(PrimitiveType.Sphere), new Vector3(sx, 0.9f, -5.0f), new Vector3(0.3f, 0.2f, 0.1f), tail, h);
            }
            Wheels(1.2f, 0.5f, 0.5f, new[] { -3.2f, 3.3f });
        }
        else // taksi yoki yengil mashina
        {
            Color paint = kind == 1 ? C(1f, 0.8f, 0.1f) : C(0.94f, 0.95f, 0.97f);
            var body = Mat(paint, null, 1f, 0.7f, 0.1f);
            RB(new Vector3(0, 0.65f, 0), new Vector3(1.85f, 0.75f, 4.3f), 0.3f, body, h);
            RB(new Vector3(0, 1.25f, -0.2f), new Vector3(1.65f, 0.6f, 2.3f), 0.25f, body, h);       // salon
            RB(new Vector3(0, 1.27f, -0.2f), new Vector3(1.68f, 0.42f, 2.0f), 0.15f, glass, h);      // yon oynalar
            RB(new Vector3(0, 1.25f, 0.88f), new Vector3(1.5f, 0.45f, 0.12f), 0.1f, glass, h);       // old oyna
            RB(new Vector3(0, 0.45f, 2.1f), new Vector3(1.8f, 0.2f, 0.15f), 0.06f, C(0.2f, 0.2f, 0.22f), h); // bufer
            RB(new Vector3(0, 0.45f, -2.1f), new Vector3(1.8f, 0.2f, 0.15f), 0.06f, C(0.2f, 0.2f, 0.22f), h);
            foreach (float sx in new[] { -0.65f, 0.65f })
            {
                Obj(PrimMesh(PrimitiveType.Sphere), new Vector3(sx, 0.75f, 2.14f), new Vector3(0.3f, 0.15f, 0.08f), head, h);
                Obj(PrimMesh(PrimitiveType.Sphere), new Vector3(sx, 0.78f, -2.14f), new Vector3(0.3f, 0.15f, 0.08f), tail, h);
            }
            if (kind == 1) // taksi: tomida chiroqli belgi va yonida shashka chiziq
            {
                RB(new Vector3(0, 1.62f, -0.2f), new Vector3(0.6f, 0.2f, 0.3f), 0.05f, MGlow(C(1f, 0.9f, 0.4f), 1.5f), h);
                for (int i = 0; i < 8; i++)
                    RB(new Vector3(0, 0.78f, -1.4f + i * 0.4f), new Vector3(1.88f, 0.12f, 0.2f), 0.02f, i % 2 == 0 ? C(0.1f, 0.1f, 0.12f) : White, h);
            }
            Wheels(0.88f, 0.32f, 0.32f, new[] { -1.35f, 1.4f });
        }
        s.movers.Add(new Mover { t = h, v = v });
    }

    // ---------------- Bonuslar: magnit, x2 yulduz, sirli quti ----------------
    class Pickup { public Transform t; public Vector3 c; public int kind; public bool taken; public float baseY; }
    float magnetT, x2T;
    Text boostTxt;
    GameObject boostPill;
    const float MagnetTime = 10f, X2Time = 15f;

    void AddPickup(Seg s, Vector3 pos, int kind)
    {
        var holder = new GameObject("Coin").transform; // "Coin" nomi: Combine birlashtirmaydi
        holder.SetParent(s.root.transform);
        holder.position = pos;
        switch (kind)
        {
            case 0: // magnit: qizil taqa, oq uchlari
                var red = Mat(C(0.9f, 0.12f, 0.12f), null, 1f, 0.6f, 0.2f, 0.4f);
                var tip = Mat(C(0.95f, 0.95f, 0.97f), null, 1f, 0.7f, 0.4f, 0.3f);
                RB(new Vector3(0, -0.25f, 0), new Vector3(0.7f, 0.2f, 0.2f), 0.09f, red, holder);
                foreach (float sx in new[] { -0.25f, 0.25f })
                {
                    RB(new Vector3(sx, 0.02f, 0), new Vector3(0.2f, 0.5f, 0.2f), 0.09f, red, holder);
                    RB(new Vector3(sx, 0.33f, 0), new Vector3(0.21f, 0.16f, 0.21f), 0.06f, tip, holder);
                }
                break;
            case 1: // x2: oltin yulduz
                var gold = Mat(Gold, null, 1f, 0.8f, 0.3f, 0.7f);
                for (int k = 0; k < 5; k++)
                {
                    float a = k * 72f;
                    var sp = RB(Quaternion.Euler(0, 0, a) * new Vector3(0, 0.2f, 0), new Vector3(0.2f, 0.45f, 0.12f), 0.05f, gold, holder);
                    sp.transform.localRotation = Quaternion.Euler(0, 0, a);
                }
                Obj(PrimMesh(PrimitiveType.Sphere), Vector3.zero, new Vector3(0.36f, 0.36f, 0.18f), gold, holder);
                break;
            default: // sirli quti: oltin ramka, girih naqshli yuzlar
                var frame = Mat(Gold, null, 1f, 0.7f, 0.3f, 0.35f);
                RB(Vector3.zero, Vector3.one * 0.7f, 0.06f, frame, holder);
                var gi = MGirih();
                RB(Vector3.zero, new Vector3(0.56f, 0.56f, 0.74f), 0.02f, gi, holder);
                RB(Vector3.zero, new Vector3(0.74f, 0.56f, 0.56f), 0.02f, gi, holder);
                RB(Vector3.zero, new Vector3(0.56f, 0.74f, 0.56f), 0.02f, gi, holder);
                break;
        }
        s.pickups.Add(new Pickup { t = holder, c = pos, kind = kind, baseY = pos.y });
    }

    void TakePickup(Pickup p)
    {
        p.taken = true;
        coinPops.Add(new CoinPop { t = p.t });
        Sfx(cLevel, 0.8f);
        AddTrauma(0.15f);
        switch (p.kind)
        {
            case 0: magnetT = MagnetTime; ShowBanner("MAGNIT!"); break;
            case 1: x2T = X2Time; ShowBanner("x2 OCHKO!"); break;
            default:
                int bonus = Random.Range(20, 51);
                coinCount += bonus;
                scoreF += bonus * 10f * stage;
                coinPunch = 1f;
                ShowBanner("+" + bonus + " TANGA");
                break;
        }
    }

    void ShowBanner(string text)
    {
        bannerTxt.text = text;
        bannerT = 2.5f;
    }

    void UpdateBoosts(float dt)
    {
        if (magnetT > 0f) magnetT -= dt;
        if (x2T > 0f) x2T -= dt;
        bool any = magnetT > 0f || x2T > 0f;
        if (boostPill != null && boostPill.activeSelf != any) boostPill.SetActive(any);
        if (any)
        {
            string t = "";
            if (magnetT > 0f) t += "MAGNIT " + Mathf.CeilToInt(magnetT);
            if (x2T > 0f) t += (t.Length > 0 ? "   " : "") + "x2 " + Mathf.CeilToInt(x2T);
            boostTxt.text = t;
        }
    }

    // ---------------- Atrof-muhit (O'zbekiston uslubi) ----------------
    void Lamp(Transform tr, float x, float z, int side)
    {
        Obj(MeshKit.LampPost(), new Vector3(x, 0, z), Vector3.one, MMetal(C(0.12f, 0.13f, 0.16f)), tr);
        Obj(MeshKit.Lantern(), new Vector3(x, 4.1f, z), Vector3.one, MGlow(C(1f, 0.88f, 0.55f), 1.4f), tr);
        Prim(PrimitiveType.Cylinder, new Vector3(x, 5.25f, z), new Vector3(0.36f, 0.04f, 0.36f), C(0.12f, 0.13f, 0.16f), tr);
        // O'zbekiston bayrog'i: ko'k, oq, yashil (qizil ingichka chiziqlar bilan)
        float fz = z + 0.85f;
        RB(new Vector3(x, 3.62f, fz), new Vector3(0.05f, 0.3f, 1.4f), 0.02f, C(0.1f, 0.6f, 0.9f), tr);
        RB(new Vector3(x, 3.46f, fz), new Vector3(0.06f, 0.03f, 1.4f), 0.01f, Red, tr);
        RB(new Vector3(x, 3.3f, fz), new Vector3(0.05f, 0.3f, 1.4f), 0.02f, White, tr);
        RB(new Vector3(x, 3.14f, fz), new Vector3(0.06f, 0.03f, 1.4f), 0.01f, Red, tr);
        RB(new Vector3(x, 2.98f, fz), new Vector3(0.05f, 0.3f, 1.4f), 0.02f, C(0.1f, 0.65f, 0.25f), tr);
    }

    void Tree(Transform tr, float x, float z)
    {
        // kafel bilan qoplangan gul tuvak
        RB(new Vector3(x, 0.35f, z), new Vector3(1.3f, 0.5f, 1.3f), 0.1f, MGirih(), tr);
        RB(new Vector3(x, 0.62f, z), new Vector3(1.4f, 0.08f, 1.4f), 0.03f, C(0.9f, 0.86f, 0.78f), tr);
        Prim(PrimitiveType.Cylinder, new Vector3(x, 1.6f, z), new Vector3(0.3f, 1.1f, 0.3f), C(0.42f, 0.28f, 0.17f), tr);
        float gv = Random.Range(0.5f, 0.62f);
        Color leaf = C(0.2f, gv, 0.18f), leafD = C(0.12f, gv - 0.12f, 0.14f), leafL = C(0.35f, gv + 0.12f, 0.2f);
        Prim(PrimitiveType.Sphere, new Vector3(x, 3.3f, z), Vector3.one * 2.6f, leafD, tr);
        Prim(PrimitiveType.Sphere, new Vector3(x + 0.8f, 3.7f, z + 0.4f), Vector3.one * 2f, leaf, tr);
        Prim(PrimitiveType.Sphere, new Vector3(x - 0.7f, 3.6f, z - 0.5f), Vector3.one * 2.1f, leaf, tr);
        Prim(PrimitiveType.Sphere, new Vector3(x - 0.2f, 4.5f, z + 0.3f), Vector3.one * 1.9f, leafL, tr);
        Prim(PrimitiveType.Sphere, new Vector3(x + 0.4f, 4.2f, z - 0.6f), Vector3.one * 1.4f, leafL, tr);
    }

    // Registon uslubidagi peshtoq: g'isht devor, girih kafelli ramka, ko'p qavatli o'tkir arka, ikki yonida minoralar
    void Madrasa(Transform tr, float x, float z, int side)
    {
        Vector3 dir = new Vector3(-side, 0, 0);
        float fx = x - side * 2f; // yo'lga qaragan yuz
        RB(new Vector3(x, 5f, z), new Vector3(4f, 10f, 14f), 0.25f, MBrick(Sand), tr);
        RB(new Vector3(x - side * 0.05f, 9.6f, z), new Vector3(4.1f, 0.6f, 14.1f), 0.08f, MBand(0.6f), tr);
        RB(new Vector3(x, 10.1f, z), new Vector3(4.2f, 0.3f, 14.2f), 0.1f, C(0.92f, 0.82f, 0.64f), tr);

        // yon tomondagi ikki qavatli arkali tokchalar
        foreach (float nz in new[] { -5.2f, 5.2f })
            foreach (float ny in new[] { 1f, 5f })
            {
                ArchObj(tr, new Vector3(fx - side * 0.02f, ny, z + nz), 2.1f, 3.3f, White, dir);
                ArchObj(tr, new Vector3(fx - side * 0.04f, ny + 0.1f, z + nz), 1.8f, 3.0f, MGirih(), dir);
                ArchObj(tr, new Vector3(fx - side * 0.06f, ny + 0.2f, z + nz), 1.2f, 2.4f, Navy, dir);
            }

        // peshtoq: chiqib turgan g'isht ramka, girih panel, arkalar, eshik
        RB(new Vector3(fx - side * 0.15f, 5.75f, z), new Vector3(0.3f, 11.5f, 8.6f), 0.06f, MBrick(C(0.93f, 0.8f, 0.6f)), tr);
        RB(new Vector3(fx - side * 0.33f, 5.4f, z), new Vector3(0.1f, 9.8f, 7.8f), 0.02f, MGirih(), tr);
        RB(new Vector3(fx - side * 0.34f, 10.9f, z), new Vector3(0.12f, 0.6f, 8.6f), 0.02f, MBand(0.6f), tr);
        ArchObj(tr, new Vector3(fx - side * 0.40f, 0, z), 6.4f, 8.6f, White, dir);
        ArchObj(tr, new Vector3(fx - side * 0.43f, 0, z), 6.0f, 8.25f, MDome(Teal), dir);
        ArchObj(tr, new Vector3(fx - side * 0.46f, 0, z), 4.4f, 6.6f, White, dir);
        ArchObj(tr, new Vector3(fx - side * 0.49f, 0, z), 4.1f, 6.3f, Navy, dir);
        ArchObj(tr, new Vector3(fx - side * 0.52f, 0, z), 2.4f, 3.6f, MWood(C(0.55f, 0.33f, 0.18f)), dir);
        for (int i = -2; i <= 2; i++)
        {
            var d = RB(new Vector3(fx - side * 0.45f, 11.3f, z + i * 1.6f), new Vector3(0.1f, 0.4f, 0.4f), 0.03f, Mat(Gold, null, 1f, 0.6f, 0.3f), tr);
            d.transform.localRotation = Quaternion.Euler(45, 0, 0);
        }
        Tower(tr, x - side * 1.5f, z - 7.6f, 0.6f);
        Tower(tr, x - side * 1.5f, z + 7.6f, 0.6f);
    }

    // Gumbazli bino: bo'rtma firuza gumbaz, kafel kamar, arkali kirish
    void DomeHall(Transform tr, float x, float z, int side)
    {
        Vector3 dir = new Vector3(-side, 0, 0);
        RB(new Vector3(x, 2.75f, z), new Vector3(8f, 5.5f, 8f), 0.3f, MBrick(Sand), tr);
        RB(new Vector3(x, 5.3f, z), new Vector3(8.1f, 0.5f, 8.1f), 0.08f, MBand(0.5f), tr);
        Prim(PrimitiveType.Cylinder, new Vector3(x, 6.6f, z), new Vector3(5f, 1.3f, 5f), C(0.85f, 0.72f, 0.52f), tr);
        Prim(PrimitiveType.Cylinder, new Vector3(x, 7.0f, z), new Vector3(5.2f, 0.35f, 5.2f), TileBlue, tr);
        Prim(PrimitiveType.Cylinder, new Vector3(x, 7.45f, z), new Vector3(5.3f, 0.08f, 5.3f), White, tr);
        Obj(MeshKit.Dome(), new Vector3(x, 7.8f, z), Vector3.one * 1.1f, MDome(Teal), tr);
        Prim(PrimitiveType.Cylinder, new Vector3(x, 12.4f, z), new Vector3(0.12f, 0.6f, 0.12f), Gold, tr);
        Prim(PrimitiveType.Sphere, new Vector3(x, 13.1f, z), Vector3.one * 0.32f, Gold, tr);
        float fx = x - side * 4f;
        RB(new Vector3(fx - side * 0.1f, 2.6f, z), new Vector3(0.2f, 5.2f, 4.4f), 0.04f, MGirih(), tr);
        ArchObj(tr, new Vector3(fx - side * 0.22f, 0, z), 3.4f, 4.4f, White, dir);
        ArchObj(tr, new Vector3(fx - side * 0.25f, 0, z), 3.1f, 4.1f, TileBlue, dir);
        ArchObj(tr, new Vector3(fx - side * 0.28f, 0, z), 2.2f, 3.3f, Navy, dir);
        foreach (float wz in new[] { -2.9f, 2.9f })
        {
            ArchObj(tr, new Vector3(fx - side * 0.02f, 1.6f, z + wz), 1.1f, 2f, White, dir);
            ArchObj(tr, new Vector3(fx - side * 0.04f, 1.7f, z + wz), 0.85f, 1.75f, Navy, dir);
        }
    }

    // Minora: g'isht, ko'k kamarlar va firuza tepa
    void Tower(Transform tr, float x, float z, float scale)
    {
        Obj(MeshKit.Minaret(), new Vector3(x, 0, z), Vector3.one * scale, MBrick(C(0.88f, 0.7f, 0.5f)), tr);
        Prim(PrimitiveType.Cylinder, new Vector3(x, 4f * scale, z), new Vector3(2.45f, 0.25f, 2.45f) * scale, TileBlue, tr);
        Prim(PrimitiveType.Cylinder, new Vector3(x, 8f * scale, z), new Vector3(2.1f, 0.25f, 2.1f) * scale, Teal, tr);
        Prim(PrimitiveType.Cylinder, new Vector3(x, 11.4f * scale, z), new Vector3(1.95f, 0.3f, 1.95f) * scale, TileBlue, tr);
        Prim(PrimitiveType.Cylinder, new Vector3(x, 14.6f * scale, z), new Vector3(1.9f, 0.3f, 1.9f) * scale, Teal, tr);
    }

    // Bozor peshtaxtasi: ikat gilamlar, chiziqli chodir, ko'zalar
    void Stall(Transform tr, float x, float z, int side)
    {
        float back = x + side * 1.1f;
        RB(new Vector3(x, 0.6f, z), new Vector3(2.2f, 0.9f, 3f), 0.1f, MWood(C(0.6f, 0.4f, 0.24f)), tr);
        RB(new Vector3(x, 1.08f, z), new Vector3(2.3f, 0.06f, 3.1f), 0.02f, MWood(C(0.5f, 0.32f, 0.2f)), tr);
        Color[] carpets = { Red, TileBlue, C(0.92f, 0.55f, 0.15f) };
        for (int i = 0; i < 3; i++)
            RB(new Vector3(back, 1.8f, z - 1f + i * 1f), new Vector3(0.08f, 1.5f, 0.9f), 0.03f, Mat(carpets[i], Tex.Ikat, 0.9f, 0.05f), tr);
        var awning = RB(new Vector3(x, 2.75f, z), new Vector3(2.8f, 0.12f, 3.4f), 0.05f, Mat(Color.white, Tex.Awning, 1f, 0.2f), tr);
        awning.transform.localRotation = Quaternion.Euler(0, 0, side * 12f);
        foreach (float pz in new[] { -1.5f, 1.5f })
            Prim(PrimitiveType.Cylinder, new Vector3(x - side * 1.2f, 1.35f, z + pz), new Vector3(0.1f, 1.35f, 0.1f), C(0.45f, 0.3f, 0.2f), tr);
        Obj(MeshKit.Vase(), new Vector3(x, 1.1f, z - 0.6f), Vector3.one * 0.5f, Mat(TileBlue, null, 1f, 0.6f), tr);
        Obj(MeshKit.Vase(), new Vector3(x - side * 0.3f, 1.1f, z + 0.5f), Vector3.one * 0.4f, Mat(Teal, null, 1f, 0.6f), tr);
        // anor/olma uyumi
        for (int i = 0; i < 4; i++)
            Prim(PrimitiveType.Sphere, new Vector3(x - side * 0.4f + (i % 2) * 0.22f, 1.22f + (i / 2) * 0.12f, z + 1.0f + (i % 2) * 0.1f), Vector3.one * 0.22f, C(0.8f, 0.12f, 0.15f), tr);
    }

    void House(Transform tr, float x, float z, int side)
    {
        Vector3 dir = new Vector3(-side, 0, 0);
        float hh = Random.Range(4.5f, 9f);
        Color c = Color.HSVToRGB(Random.Range(0.07f, 0.11f), Random.Range(0.25f, 0.38f), Random.Range(0.82f, 0.95f));
        RB(new Vector3(x, hh / 2f, z), new Vector3(5f, hh, 11f), 0.2f, MBrick(c), tr);
        RB(new Vector3(x - side * 0.03f, hh - 0.6f, z), new Vector3(5.1f, 0.5f, 11.1f), 0.06f, MBand(0.5f), tr);
        RB(new Vector3(x, hh + 0.1f, z), new Vector3(5.2f, 0.25f, 11.2f), 0.08f, C(0.92f, 0.85f, 0.72f), tr);
        float fx = x - side * 2.5f;
        for (int i = -1; i <= 1; i++)
        {
            float wz = z + i * 3.2f;
            if (i == 0) // eshik
            {
                ArchObj(tr, new Vector3(fx - side * 0.02f, 0, wz), 1.7f, 2.9f, White, dir);
                ArchObj(tr, new Vector3(fx - side * 0.04f, 0, wz), 1.4f, 2.6f, MWood(C(0.5f, 0.3f, 0.17f)), dir);
            }
            else
            {
                ArchObj(tr, new Vector3(fx - side * 0.02f, 1.3f, wz), 1.4f, 2.2f, White, dir);
                ArchObj(tr, new Vector3(fx - side * 0.04f, 1.4f, wz), 1.1f, 1.9f, MGlass(), dir);
            }
            if (hh > 6f)
            {
                ArchObj(tr, new Vector3(fx - side * 0.02f, 4.1f, wz), 1.4f, 2.1f, White, dir);
                ArchObj(tr, new Vector3(fx - side * 0.04f, 4.2f, wz), 1.1f, 1.8f, MGlass(), dir);
                RB(new Vector3(fx - side * 0.2f, 4.05f, wz), new Vector3(0.4f, 0.1f, 1.6f), 0.03f, MWood(C(0.55f, 0.35f, 0.2f)), tr); // tokcha
            }
        }
    }

    // Uzoqdagi siluetlar: katta gumbazlar va minoralar (tuman ichida chuqurlik beradi)
    void FarSkyline(Transform tr, int side, float z)
    {
        float x = side * Random.Range(30f, 42f);
        switch (Random.Range(0, 3))
        {
            case 0:
                RB(new Vector3(x, 4f, z), new Vector3(12f, 8f, 12f), 0.3f, MBrick(Sand), tr);
                Prim(PrimitiveType.Cylinder, new Vector3(x, 9f, z), new Vector3(7f, 1.2f, 7f), C(0.85f, 0.72f, 0.52f), tr);
                Obj(MeshKit.Dome(), new Vector3(x, 10f, z), Vector3.one * 1.5f, MDome(Random.value < 0.5f ? Teal : TileBlue), tr);
                break;
            case 1:
                Tower(tr, x, z, Random.Range(1.4f, 1.9f));
                RB(new Vector3(x + side * 6f, 3.5f, z), new Vector3(9f, 7f, 10f), 0.3f, MBrick(C(0.84f, 0.7f, 0.52f)), tr);
                break;
            default:
                RB(new Vector3(x, 6f, z), new Vector3(7f, 12f, 16f), 0.3f, MBrick(C(0.9f, 0.78f, 0.6f)), tr);
                RB(new Vector3(x, 11.4f, z), new Vector3(7.1f, 0.6f, 16.1f), 0.08f, MBand(0.6f), tr);
                Obj(MeshKit.Dome(), new Vector3(x, 12f, z - 4f), Vector3.one * 0.7f, MDome(Teal), tr);
                break;
        }
    }

    // Yo'l ustidagi bezakli ko'k arka-darvoza
    void Gantry(Transform tr, float z)
    {
        Vector3 dir = Vector3.forward;
        RingObj(tr, new Vector3(0, 0, z), 12.6f, 11.5f, 10.2f, 9.6f, MGirih(), dir);
        RingObj(tr, new Vector3(0, 0, z - 0.05f), 10.4f, 9.8f, 10.0f, 9.4f, White, dir);
        RingObj(tr, new Vector3(0, 0, z + 0.05f), 12.8f, 11.7f, 12.5f, 11.4f, White, dir);
        for (int i = -3; i <= 3; i++)
        {
            float ay = i == 0 ? 11f : 10.6f - Mathf.Abs(i) * 0.9f;
            var d = RB(new Vector3(i * 1.4f, ay - 0.1f - Mathf.Abs(i) * 0.05f, z - 0.1f), new Vector3(0.45f, 0.45f, 0.06f), 0.03f, Mat(Gold, null, 1f, 0.6f, 0.3f), tr);
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

    // Do'ppi kiygan, ryukzakli bola (+Z ga qarab yuguradi). Proporsiya: bosh ~1/4 bo'y, uzun oyoqlar.
    // Kostyum: krem kofta (ko'kragida naqshli to'q panellar, kumush zamok, katta kapyushon), to'q orqa panel va oltin romb,
    // chiziqli manjet va etak, jogger shim (oq yon chiziq, yig'ilgan pochalar), qalin ikki rangli krossovka, oltin rombli ryukzak.
    void BuildCharacter(Transform m)
    {
        Style st = Styles[styleIndex];
        Color skin = st.skin, skinDark = new Color(st.skin.r * 0.86f, st.skin.g * 0.8f, st.skin.b * 0.78f);
        Color panel = st.accent;
        var cloth = Mat(st.top, null, 1f, 0.15f);
        var panelM = Mat(panel, null, 1f, 0.2f);
        var skinM = Mat(skin, null, 1f, 0.3f);
        var hairM = Mat(st.hair, null, 1f, 0.3f);
        var bagM = Mat(st.bag, null, 1f, 0.3f);
        var goldM = Mat(Gold, null, 1f, 0.7f, 0.4f);
        var whiteM = Mat(C(0.97f, 0.96f, 0.93f), null, 1f, 0.25f);
        var silver = MMetal(C(0.82f, 0.84f, 0.88f));
        var sphere = PrimMesh(PrimitiveType.Sphere);
        var capsule = PrimMesh(PrimitiveType.Capsule);

        upper = Pivot(m, new Vector3(0, 0.9f, 0));
        Vector3 U(float x, float y, float z) { return new Vector3(x, y - 0.9f, z); }
        GameObject Rot(GameObject g, float x, float y, float z) { g.transform.localRotation = Quaternion.Euler(x, y, z); return g; }
        void Ball(Vector3 p, Vector3 s, Material mat) { Obj(sphere, p, s, mat, upper); }

        // ---- kofta ----
        RB(U(0, 1.2f, 0), new Vector3(0.6f, 0.6f, 0.36f), 0.16f, cloth, upper);
        RB(U(0, 0.94f, 0), new Vector3(0.62f, 0.1f, 0.38f), 0.05f, panelM, upper);                     // etak
        foreach (float sy in new[] { 0.92f, 0.96f })
            RB(U(0, sy, 0), new Vector3(0.63f, 0.012f, 0.39f), 0.005f, whiteM, upper);                  // etakdagi oq chiziqlar
        foreach (float sx in new[] { -0.105f, 0.105f })                                                  // ko'krakdagi naqshli panellar
        {
            RB(U(sx, 1.21f, 0.178f), new Vector3(0.09f, 0.5f, 0.03f), 0.012f, panelM, upper);
            for (int i = 0; i < 4; i++)
                Rot(RB(U(sx, 1.03f + i * 0.12f, 0.195f), new Vector3(0.04f, 0.04f, 0.012f), 0.006f, i % 2 == 0 ? whiteM : goldM, upper), 0, 0, 45);
        }
        RB(U(0, 1.21f, 0.186f), new Vector3(0.024f, 0.56f, 0.02f), 0.008f, silver, upper);             // zamok
        RB(U(0, 1.45f, 0.2f), new Vector3(0.035f, 0.06f, 0.018f), 0.008f, silver, upper);              // tutqich
        // orqa: to'q panel va oltin romb
        RB(U(0, 1.24f, -0.178f), new Vector3(0.46f, 0.42f, 0.03f), 0.02f, panelM, upper);
        Rot(RB(U(0, 1.24f, -0.195f), new Vector3(0.14f, 0.14f, 0.012f), 0.01f, goldM, upper), 0, 0, 45);
        // kapyushon: krem tashqi, to'q ichi, oldida to'q yoqa va oq iplar
        RB(U(0, 1.5f, -0.15f), new Vector3(0.5f, 0.26f, 0.26f), 0.12f, cloth, upper);
        RB(U(0, 1.52f, -0.09f), new Vector3(0.36f, 0.16f, 0.16f), 0.07f, panelM, upper);
        RB(U(0, 1.5f, 0.05f), new Vector3(0.36f, 0.08f, 0.26f), 0.035f, panelM, upper);
        foreach (float dx in new[] { -0.055f, 0.055f })
        {
            Prim(PrimitiveType.Cylinder, U(dx, 1.38f, 0.195f), new Vector3(0.016f, 0.07f, 0.016f), C(0.97f, 0.96f, 0.93f), upper);
            Ball(U(dx, 1.3f, 0.195f), Vector3.one * 0.028f, silver);
        }

        // ---- ryukzak ----
        RB(U(0, 1.2f, -0.3f), new Vector3(0.42f, 0.5f, 0.22f), 0.1f, bagM, upper);
        RB(U(0, 1.47f, -0.28f), new Vector3(0.15f, 0.05f, 0.06f), 0.025f, bagM, upper);                // tutqich
        RB(U(0, 1.41f, -0.3f), new Vector3(0.44f, 0.018f, 0.23f), 0.008f, goldM, upper);               // tepa zamok
        RB(U(0, 1.08f, -0.42f), new Vector3(0.33f, 0.25f, 0.07f), 0.05f, bagM, upper);                 // cho'ntak
        RB(U(0, 1.21f, -0.44f), new Vector3(0.3f, 0.016f, 0.025f), 0.008f, goldM, upper);
        Rot(RB(U(0, 1.08f, -0.458f), new Vector3(0.15f, 0.15f, 0.025f), 0.015f, goldM, upper), 0, 0, 45);
        Rot(RB(U(0, 1.08f, -0.47f), new Vector3(0.1f, 0.1f, 0.02f), 0.012f, bagM, upper), 0, 0, 45);
        Rot(RB(U(0, 1.08f, -0.478f), new Vector3(0.045f, 0.045f, 0.015f), 0.008f, goldM, upper), 0, 0, 45);
        foreach (float sx in new[] { -0.19f, 0.19f })
        {
            RB(U(sx, 1.47f, -0.03f), new Vector3(0.07f, 0.05f, 0.4f), 0.02f, bagM, upper);             // yelka tasmasi
            RB(U(sx, 1.36f, 0.178f), new Vector3(0.065f, 0.2f, 0.03f), 0.015f, bagM, upper);          // old tasma
            RB(U(sx, 1.27f, 0.19f), new Vector3(0.07f, 0.03f, 0.02f), 0.008f, silver, upper);         // to'qa
        }

        // ---- bosh ----
        Prim(PrimitiveType.Cylinder, U(0, 1.53f, 0), new Vector3(0.14f, 0.05f, 0.14f), skin, upper).GetComponent<MeshRenderer>().sharedMaterial = skinM;
        Ball(U(0, 1.7f, 0.01f), new Vector3(0.46f, 0.5f, 0.46f), skinM);
        Ball(U(0, 1.62f, 0.03f), new Vector3(0.4f, 0.36f, 0.4f), skinM);                                // yonoq va iyak
        foreach (float ex in new[] { -0.225f, 0.225f })
            Ball(U(ex, 1.69f, 0f), new Vector3(0.07f, 0.1f, 0.06f), skinM);                             // quloq

        // soch: hajmli, do'ppi ostidan peshonaga va quloq ustiga tutamlar chiqib turadi
        Ball(U(0, 1.76f, -0.03f), new Vector3(0.5f, 0.46f, 0.48f), hairM);
        Ball(U(0, 1.62f, -0.14f), new Vector3(0.36f, 0.26f, 0.22f), hairM);                             // ensa
        foreach (float ex in new[] { -0.21f, 0.21f })
        {
            RB(U(ex, 1.7f, 0.05f), new Vector3(0.045f, 0.12f, 0.09f), 0.02f, hairM, upper);           // chakka
            Rot(RB(U(ex * 1.08f, 1.83f, 0.02f), new Vector3(0.06f, 0.12f, 0.16f), 0.03f, hairM, upper), 0, 0, ex < 0 ? -30f : 30f);
        }
        float[,] tufts = { { -0.15f, 1.85f, 0.15f, 30f }, { -0.07f, 1.84f, 0.19f, 12f }, { 0.02f, 1.84f, 0.2f, -6f }, { 0.1f, 1.85f, 0.18f, -20f }, { 0.17f, 1.86f, 0.13f, -36f } };
        for (int i = 0; i < tufts.GetLength(0); i++)
            Rot(RB(U(tufts[i, 0], tufts[i, 1], tufts[i, 2]), new Vector3(0.09f, 0.11f, 0.06f), 0.03f, hairM, upper), 25f, 0, tufts[i, 3]);

        // yuz
        var eyeW = Mat(C(0.99f, 0.99f, 0.98f), null, 1f, 0.9f);
        var iris = Mat(C(0.38f, 0.22f, 0.1f), null, 1f, 0.9f);
        var pupil = Mat(C(0.04f, 0.03f, 0.03f), null, 1f, 0.9f);
        var shine = MGlow(C(1f, 1f, 1f), 0.6f);
        foreach (float ex in new[] { -0.085f, 0.085f })
        {
            Ball(U(ex, 1.69f, 0.205f), new Vector3(0.095f, 0.115f, 0.05f), eyeW);
            Ball(U(ex, 1.685f, 0.224f), new Vector3(0.066f, 0.082f, 0.035f), iris);
            Ball(U(ex, 1.685f, 0.234f), new Vector3(0.036f, 0.045f, 0.02f), pupil);
            Ball(U(ex + 0.014f, 1.703f, 0.241f), Vector3.one * 0.02f, shine);
            Rot(RB(U(ex, 1.772f, 0.2f), new Vector3(0.1f, 0.026f, 0.035f), 0.012f, hairM, upper), 0, 0, ex < 0 ? -10f : 10f);
        }
        Ball(U(0, 1.64f, 0.228f), new Vector3(0.05f, 0.045f, 0.05f), Mat(skinDark, null, 1f, 0.3f));
        var lip = Mat(C(0.55f, 0.2f, 0.16f), null, 1f, 0.4f);
        RB(U(0, 1.585f, 0.224f), new Vector3(0.06f, 0.018f, 0.02f), 0.008f, lip, upper);
        Rot(RB(U(-0.037f, 1.592f, 0.22f), new Vector3(0.032f, 0.016f, 0.02f), 0.007f, lip, upper), 0, 0, -28);
        Rot(RB(U(0.037f, 1.592f, 0.22f), new Vector3(0.032f, 0.016f, 0.02f), 0.007f, lip, upper), 0, 0, 28);
        foreach (float ex in new[] { -0.13f, 0.13f })
            Ball(U(ex, 1.625f, 0.175f), new Vector3(0.06f, 0.04f, 0.03f), Mat(C(1f, 0.62f, 0.56f), null, 1f, 0.3f));

        // ---- bosh kiyimi ----
        if (st.head == 0) // do'ppi
        {
            RB(U(0, 1.92f, -0.01f), new Vector3(0.46f, 0.18f, 0.46f), 0.07f, Mat(Color.white, Tex.Doppi, 0.23f, 0.3f, 0f, 0f, 0.5f), upper);
            RB(U(0, 1.84f, -0.01f), new Vector3(0.465f, 0.02f, 0.465f), 0.008f, Mat(C(0.95f, 0.5f, 0.15f), null, 1f, 0.3f), upper); // pastki zeh
        }
        else if (st.head == 1) // beysbolka
        {
            var capM = Mat(st.hat, null, 1f, 0.25f);
            RB(U(0, 1.93f, -0.01f), new Vector3(0.5f, 0.2f, 0.5f), 0.1f, capM, upper);
            RB(U(0, 1.86f, 0.28f), new Vector3(0.4f, 0.035f, 0.24f), 0.015f, capM, upper);
            Rot(RB(U(0, 1.93f, 0.24f), new Vector3(0.08f, 0.08f, 0.025f), 0.012f, whiteM, upper), 0, 0, 45);
            Prim(PrimitiveType.Sphere, U(0, 2.035f, -0.01f), Vector3.one * 0.05f, st.hat, upper);
        }
        else // sochga bog'langan lenta
        {
            Ball(U(0, 1.8f, -0.02f), new Vector3(0.5f, 0.4f, 0.48f), hairM);
            RB(U(0, 1.88f, 0.16f), new Vector3(0.38f, 0.05f, 0.12f), 0.025f, Mat(st.hat, null, 1f, 0.3f), upper);
            Rot(RB(U(0.17f, 1.92f, 0.08f), new Vector3(0.13f, 0.08f, 0.04f), 0.025f, st.hat, upper), 0, 0, 25);
            Rot(RB(U(0.24f, 1.89f, 0.08f), new Vector3(0.13f, 0.08f, 0.04f), 0.025f, st.hat, upper), 0, 0, -25);
        }
        if (st.ponytail)
        {
            Prim(PrimitiveType.Sphere, U(0, 1.8f, -0.27f), Vector3.one * 0.12f, st.hat, upper);
            Ball(U(0, 1.68f, -0.34f), new Vector3(0.19f, 0.26f, 0.18f), hairM);
            Ball(U(0, 1.5f, -0.38f), new Vector3(0.16f, 0.28f, 0.16f), hairM);
            Ball(U(0, 1.33f, -0.4f), new Vector3(0.12f, 0.22f, 0.12f), hairM);
        }
        if (st.glasses)
        {
            var gl = Mat(C(0.03f, 0.03f, 0.05f), null, 1f, 0.95f);
            foreach (float ex in new[] { -0.085f, 0.085f })
                RB(U(ex, 1.69f, 0.245f), new Vector3(0.13f, 0.085f, 0.025f), 0.025f, gl, upper);
            RB(U(0, 1.705f, 0.245f), new Vector3(0.05f, 0.016f, 0.025f), 0.008f, gl, upper);
        }
        if (st.pattern)
            for (int i = 0; i < 3; i++)
                Rot(RB(U(0.2f, 1.06f + i * 0.14f, 0.18f), new Vector3(0.07f, 0.07f, 0.015f), 0.012f, i % 2 == 0 ? White : TileBlue, upper), 0, 0, 45);

        // qo'llar va oyoqlar
        armL = Pivot(upper, U(-0.36f, 1.42f, 0));
        armR = Pivot(upper, U(0.36f, 1.42f, 0));
        elbowL = BuildArm(armL, cloth, panelM, whiteM, skinM);
        elbowR = BuildArm(armR, cloth, panelM, whiteM, skinM);
        legL = Pivot(m, new Vector3(-0.14f, 0.9f, 0));
        legR = Pivot(m, new Vector3(0.14f, 0.9f, 0));
        kneeL = BuildLeg(legL, st, -1);
        kneeR = BuildLeg(legR, st, 1);
    }

    Transform BuildArm(Transform shoulder, Material sleeve, Material cuff, Material stripe, Material skin)
    {
        var sphere = PrimMesh(PrimitiveType.Sphere);
        var capsule = PrimMesh(PrimitiveType.Capsule);
        Obj(sphere, Vector3.zero, Vector3.one * 0.2f, sleeve, shoulder);
        Obj(capsule, new Vector3(0, -0.16f, 0), new Vector3(0.18f, 0.17f, 0.18f), sleeve, shoulder);
        var elbow = Pivot(shoulder, new Vector3(0, -0.32f, 0));
        Obj(capsule, new Vector3(0, -0.13f, 0), new Vector3(0.17f, 0.15f, 0.17f), sleeve, elbow);
        RB(new Vector3(0, -0.26f, 0), new Vector3(0.17f, 0.09f, 0.17f), 0.04f, cuff, elbow);
        foreach (float sy in new[] { -0.24f, -0.28f })
            RB(new Vector3(0, sy, 0), new Vector3(0.175f, 0.01f, 0.175f), 0.004f, stripe, elbow);
        Obj(sphere, new Vector3(0, -0.35f, 0.01f), new Vector3(0.12f, 0.14f, 0.1f), skin, elbow);         // kaft
        Obj(sphere, new Vector3(0, -0.41f, 0.02f), new Vector3(0.1f, 0.06f, 0.08f), skin, elbow);         // barmoqlar
        Obj(sphere, new Vector3(0, -0.33f, 0.06f), new Vector3(0.05f, 0.08f, 0.05f), skin, elbow);        // bosh barmoq
        return elbow;
    }

    Transform BuildLeg(Transform hip, Style st, int side)
    {
        var capsule = PrimMesh(PrimitiveType.Capsule);
        var pants = Mat(st.pants, null, 1f, 0.15f);
        var pipe = Mat(C(0.97f, 0.96f, 0.93f), null, 1f, 0.2f);
        var shoe = Mat(st.shoe, null, 1f, 0.45f);
        var acc = Mat(st.accent == White ? st.pants : st.accent, null, 1f, 0.3f);
        var sole = Mat(C(0.86f, 0.87f, 0.9f), null, 1f, 0.3f);
        Obj(capsule, new Vector3(0, -0.21f, 0), new Vector3(0.26f, 0.23f, 0.26f), pants, hip);
        RB(new Vector3(side * 0.125f, -0.21f, 0), new Vector3(0.015f, 0.34f, 0.04f), 0.006f, pipe, hip);
        var knee = Pivot(hip, new Vector3(0, -0.42f, 0));
        Obj(capsule, new Vector3(0, -0.17f, 0), new Vector3(0.235f, 0.19f, 0.235f), pants, knee);
        RB(new Vector3(side * 0.112f, -0.17f, 0), new Vector3(0.015f, 0.24f, 0.04f), 0.006f, pipe, knee);
        Obj(capsule, new Vector3(0, -0.34f, 0), new Vector3(0.2f, 0.05f, 0.2f), pants, knee);            // yig'ilgan pocha
        // qalin krossovka: oq korpus, kulrang taglik, to'q yon chiziq, bog'ich joyi, tovon, oq bog'ichlar
        RB(new Vector3(0, -0.43f, 0.06f), new Vector3(0.25f, 0.14f, 0.42f), 0.07f, shoe, knee);
        RB(new Vector3(0, -0.495f, 0.06f), new Vector3(0.27f, 0.06f, 0.44f), 0.025f, sole, knee);
        Rot2(RB(new Vector3(0, -0.43f, 0.04f), new Vector3(0.26f, 0.045f, 0.24f), 0.015f, acc, knee), -14f);
        RB(new Vector3(0, -0.365f, 0.15f), new Vector3(0.14f, 0.035f, 0.18f), 0.015f, acc, knee);
        for (int i = 0; i < 3; i++)
            RB(new Vector3(0, -0.345f, 0.09f + i * 0.05f), new Vector3(0.12f, 0.012f, 0.015f), 0.004f, pipe, knee);
        RB(new Vector3(0, -0.4f, -0.14f), new Vector3(0.15f, 0.1f, 0.04f), 0.015f, acc, knee);           // tovon
        return knee;
    }

    static void Rot2(GameObject g, float x) { g.transform.localRotation = Quaternion.Euler(x, 0, 0); }

    // ---------------- Shakl yaratish yordamchilari ----------------
    GameObject Obj(Mesh mesh, Vector3 pos, Vector3 scale, Color col, Transform parent)
    {
        return Obj(mesh, pos, scale, Mat(col), parent);
    }

    GameObject Obj(Mesh mesh, Vector3 pos, Vector3 scale, Material mat, Transform parent)
    {
        var go = new GameObject("Mesh");
        go.transform.SetParent(parent, false);
        go.transform.localPosition = pos;
        go.transform.localScale = scale;
        go.AddComponent<MeshFilter>().sharedMesh = mesh;
        go.AddComponent<MeshRenderer>().sharedMaterial = mat;
        return go;
    }

    GameObject RB(Vector3 pos, Vector3 size, float r, Color col, Transform parent)
    {
        return RB(pos, size, r, Mat(col), parent);
    }

    GameObject RB(Vector3 pos, Vector3 size, float r, Material mat, Transform parent)
    {
        // o'lchamni 0.05 ga yaxlitlab, bir xil meshlar qayta ishlatiladi
        size = new Vector3(Mathf.Round(size.x * 20f) / 20f, Mathf.Round(size.y * 20f) / 20f, Mathf.Round(size.z * 20f) / 20f);
        return Obj(MeshKit.RoundedBox(size, r), pos, Vector3.one, mat, parent);
    }

    GameObject ArchObj(Transform parent, Vector3 pos, float w, float h, Color col, Vector3 dir)
    {
        return ArchObj(parent, pos, w, h, Mat(col), dir);
    }

    GameObject ArchObj(Transform parent, Vector3 pos, float w, float h, Material mat, Vector3 dir)
    {
        var go = Obj(MeshKit.Arch(w, h), pos, Vector3.one, mat, parent);
        go.transform.localRotation = Quaternion.LookRotation(dir);
        return go;
    }

    GameObject RingObj(Transform parent, Vector3 pos, float wo, float ho, float wi, float hi, Color col, Vector3 dir)
    {
        return RingObj(parent, pos, wo, ho, wi, hi, Mat(col), dir);
    }

    GameObject RingObj(Transform parent, Vector3 pos, float wo, float ho, float wi, float hi, Material mat, Vector3 dir)
    {
        var go = Obj(MeshKit.Ring(wo, ho, wi, hi), pos, Vector3.one, mat, parent);
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

    // Material: rang, ixtiyoriy tekstura (metrdagi o'lchami bilan), silliqlik, metall, nur sochish
    Material Mat(Color c) { return Mat(c, null, 1f, 0.12f, 0f, 0f, 0f); }

    Material Mat(Color c, Texture2D tex, float tileSize = 1f, float smooth = 0.12f, float metal = 0f, float emit = 0f, float offY = 0f)
    {
        string key = c.ToString("F3") + (tex != null ? tex.name : "-") + tileSize.ToString("F2") + smooth.ToString("F2")
                     + metal.ToString("F2") + emit.ToString("F2") + offY.ToString("F2");
        if (mats.TryGetValue(key, out var m)) return m;
        var sh = Shader.Find("Universal Render Pipeline/Lit");
        if (sh == null) sh = Shader.Find("Standard");
        m = new Material(sh) { color = c };
        m.SetColor("_BaseColor", c);
        m.SetFloat("_Smoothness", smooth);
        m.SetFloat("_Glossiness", smooth);
        m.SetFloat("_Metallic", metal);
        if (tex != null)
        {
            m.SetTexture("_BaseMap", tex);
            m.mainTexture = tex;
            var sc = new Vector2(1f / tileSize, 1f / tileSize);
            var off = new Vector2(0f, offY);
            m.SetTextureScale("_BaseMap", sc); m.SetTextureOffset("_BaseMap", off);
            m.mainTextureScale = sc; m.mainTextureOffset = off;
        }
        if (emit > 0f)
        {
            m.EnableKeyword("_EMISSION");
            m.SetColor("_EmissionColor", c * emit);
            m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
        }
        mats[key] = m;
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
        var fl = MakePanel(cv.transform, "Flash", new Color(1f, 1f, 1f, 0f));
        flashImg = fl.GetComponent<Image>();
        flashImg.raycastTarget = false;
        flashImg.enabled = false;
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
        if (pillSprite == null) pillSprite = MakePillSprite();
        var img = go.GetComponent<Image>();
        img.sprite = pillSprite;
        img.type = Image.Type.Sliced;
        img.color = col;
        var btnShadow = go.AddComponent<Shadow>(); // pastki qalin soya: "bosiladigan" 3D tugma
        btnShadow.effectColor = new Color(0f, 0f, 0f, 0.4f);
        btnShadow.effectDistance = new Vector2(0f, -10f);
        var btnOutline = go.AddComponent<Outline>();
        btnOutline.effectColor = new Color(1f, 1f, 1f, 0.35f);
        btnOutline.effectDistance = new Vector2(3f, -3f);
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
        title.color = new Color(1f, 0.86f, 0.28f);
        title.rectTransform.sizeDelta = new Vector2(1000, 160);
        var tOut = title.gameObject.AddComponent<Outline>();
        tOut.effectColor = new Color(0.08f, 0.2f, 0.55f);
        tOut.effectDistance = new Vector2(6f, -6f);
        var sub = MakeText(mt, "Sub", new Vector2(0.5f, 1f), new Vector2(0, -290), TextAnchor.MiddleCenter, 60);
        sub.text = "O'ZBEKISTON";
        sub.color = Color.white;
        var sOut = sub.gameObject.AddComponent<Outline>();
        sOut.effectColor = new Color(0.08f, 0.2f, 0.55f);
        sOut.effectDistance = new Vector2(4f, -4f);
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
