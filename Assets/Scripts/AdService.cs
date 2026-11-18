using System;
using Unity.Services.LevelPlay;
using UnityEngine;

// Unity LevelPlay: mukofotli reklama (davom etish), to'liq ekran (interstitial) va banner.
// App Key va Ad Unit ID lar platform.ironsrc.com (LevelPlay) dan olingan.
public class AdService
{
    const string AppKey = "29ac7e49d";
    const string RewardedId = "dny8475hq49pjklx";
    const string InterstitialId = "8yc2x1dfeu5sij6l";
    const string BannerId = "5n0c5u7hg713jvlp";

    // true bo'lsa: ilova ochilganda LevelPlay Test Suite chiqadi (SDK, kalit va ad unit to'g'riligini tekshiradi,
    // test reklamalarni ko'rsatadi). Tekshirib bo'lgach false qiling.
    const bool TestSuite = true;

    // oxirgi xato matni (o'yin ichida ko'rsatiladi)
    public string LastError = "";

    static AdService inst;
    public static AdService I { get { if (inst == null) inst = new AdService(); return inst; } }

    // LevelPlay faqat Android/iOS da ishlaydi (kompyuterda reklama yo'q, sinov ekrani chiqadi)
    public bool Supported
    {
        get
        {
#if (UNITY_ANDROID || UNITY_IOS) && !UNITY_EDITOR
            return true;
#else
            return false;
#endif
        }
    }

    public bool RewardedReady { get { return rewarded != null && rewarded.IsAdReady(); } }
    public bool InterstitialReady { get { return interstitial != null && interstitial.IsAdReady(); } }

    LevelPlayRewardedAd rewarded;
    LevelPlayInterstitialAd interstitial;
    LevelPlayBannerAd banner;

    public string BannerStatus = "";
    bool initStarted, initDone, bannerWanted, bannerLoaded;
    bool rewardEarned;
    Action rewardGranted, showFinished;
    float retryT;

    public void Init()
    {
        if (!Supported || initStarted) return;
        initStarted = true;
        if (TestSuite) LevelPlay.SetMetaData("is_test_suite", "enable");
        LevelPlay.OnInitSuccess += OnInitSuccess;
        LevelPlay.OnInitFailed += OnInitFailed;
        LevelPlay.Init(AppKey);
    }

    void OnInitSuccess(LevelPlayConfiguration config)
    {
        initDone = true;
        if (TestSuite) LevelPlay.LaunchTestSuite();

        rewarded = new LevelPlayRewardedAd(RewardedId);
        rewarded.OnAdLoadFailed += e => { LastError = "Rewarded: " + e.ErrorCode + " " + e.ErrorMessage; Debug.Log("Rewarded yuklanmadi: " + e); };
        rewarded.OnAdRewarded += (info, reward) => { rewardEarned = true; };
        rewarded.OnAdClosed += info => FinishRewarded();
        rewarded.OnAdDisplayFailed += (info, e) => { Debug.Log("Rewarded ko'rsatilmadi: " + e); FinishRewarded(); };
        rewarded.LoadAd();

        interstitial = new LevelPlayInterstitialAd(InterstitialId);
        interstitial.OnAdLoadFailed += e => { LastError = "Interstitial: " + e.ErrorCode + " " + e.ErrorMessage; Debug.Log("Interstitial yuklanmadi: " + e); };
        interstitial.OnAdClosed += info => FinishInterstitial();
        interstitial.OnAdDisplayFailed += (info, e) => { Debug.Log("Interstitial ko'rsatilmadi: " + e); FinishInterstitial(); };
        interstitial.LoadAd();

        var cfg = new LevelPlayBannerAd.Config.Builder()
            .SetSize(LevelPlayAdSize.BANNER)
            .SetPosition(LevelPlayBannerPosition.BottomCenter)
            .SetDisplayOnLoad(false)
            .SetRespectSafeArea(true)
            .Build();
        banner = new LevelPlayBannerAd(BannerId, cfg);
        banner.OnAdLoaded += info => { bannerLoaded = true; if (bannerWanted) banner.ShowAd(); };
        banner.OnAdLoadFailed += e => { bannerLoaded = false; BannerStatus = "Banner: " + e.ErrorCode + " " + e.ErrorMessage; LastError = BannerStatus; Debug.Log("Banner yuklanmadi: " + e); };
        banner.OnAdDisplayed += info => { BannerStatus = "Banner ko'rinyapti"; Debug.Log(BannerStatus); };
        banner.OnAdDisplayFailed += (info, e) => { BannerStatus = "Banner ko'rsatilmadi: " + e.ErrorMessage; Debug.Log(BannerStatus); };
        banner.LoadAd();
    }

    void OnInitFailed(LevelPlayInitError error)
    {
        LastError = "Init: " + error.ErrorCode + " " + error.ErrorMessage;
        Debug.Log("LevelPlay ishga tushmadi: " + error);
        initStarted = false; // keyinroq qayta urinadi
    }

    // yuklanmagan reklamalarni vaqti-vaqti bilan qayta urinish
    public void Tick(float dt)
    {
        if (!Supported) return;
        retryT -= dt;
        if (retryT > 0f) return;
        retryT = 20f;
        if (!initDone) { Init(); return; }
        if (rewarded != null && !rewarded.IsAdReady()) rewarded.LoadAd();
        if (interstitial != null && !interstitial.IsAdReady()) interstitial.LoadAd();
        if (banner != null && !bannerLoaded) banner.LoadAd();
    }

    // ---- Mukofotli reklama ----
    public void ShowRewarded(Action onReward, Action onFail)
    {
        if (!RewardedReady) { if (onFail != null) onFail(); return; }
        rewardEarned = false;
        rewardGranted = onReward;
        showFinished = onFail;
        BeginShow();
        rewarded.ShowAd();
    }

    void FinishRewarded()
    {
        AudioListener.pause = false;
        var grant = rewardGranted; var fail = showFinished;
        rewardGranted = null; showFinished = null;
        bool ok = rewardEarned;
        rewardEarned = false;
        rewarded.LoadAd(); // keyingisini oldindan yuklaymiz
        if (ok) { if (grant != null) grant(); }
        else if (fail != null) fail(); // oxirigacha ko'rilmadi: mukofot yo'q
    }

    // ---- To'liq ekran reklama ----
    public void ShowInterstitial(Action onDone)
    {
        if (!InterstitialReady) { if (onDone != null) onDone(); return; }
        showFinished = onDone;
        BeginShow();
        interstitial.ShowAd();
    }

    void FinishInterstitial()
    {
        AudioListener.pause = false;
        var done = showFinished;
        showFinished = null;
        interstitial.LoadAd();
        if (done != null) done();
    }

    void BeginShow()
    {
        AudioListener.pause = true; // reklama paytida o'yin ovozi o'chadi
    }

    // ---- Banner ----
    public void ShowBanner(bool show)
    {
        bannerWanted = show;
        if (banner == null) return;
        if (!show) banner.HideAd();
        else if (bannerLoaded) banner.ShowAd();
    }
}
