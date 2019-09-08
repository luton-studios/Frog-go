using UnityEngine;
using System;
using GoogleMobileAds.Api;

public class AdController : MonoBehaviour {
    private const string ADMOB_APP_ID = "REDACTED";
    private const int AD_WATCH_CAP = 5; // 5 ads maximum per day.

    public static AdController instance { get; private set; }

    public GameUI gameUi;
    public ShopController shop;
    public AchievementController achieve;

    public float bannerHeightPercent {
        get {
            return bannerViewHeight / Screen.height;
        }
    }

    private BannerView bannerView;
    private RewardBasedVideoAd rewardVideo;
    private bool isWatchingRewardVid;
    private bool isShowingAd;
    private bool isLoadingBanner;
    private bool isDestroyingBanner;
    private float bannerViewHeight;
    
    private void Awake() {
        instance = this;
        isWatchingRewardVid = false;
        isShowingAd = false;
        isLoadingBanner = false;
        isDestroyingBanner = false;

        if(!PlayerPrefs.HasKey("AdCapCurrentDate")) {
            // Initialize current day for watch cap.
            PlayerPrefs.SetInt("AdCapCurrentDate", GetCurrentAdDay());
        }
    }

    private void Start() {
        MobileAds.Initialize(ADMOB_APP_ID);
        rewardVideo = RewardBasedVideoAd.Instance;
        rewardVideo.OnAdLoaded += HandleVideoAdLoaded;
        rewardVideo.OnAdRewarded += HandleVideoAdRewarded;
        rewardVideo.OnAdFailedToLoad += HandleVideoAdFailed;
        rewardVideo.OnAdClosed += HandleVideoAdClosed;
    }

    private void Update() {
        if(isDestroyingBanner && !isLoadingBanner && bannerView != null) {
            // Wait for ad to load before destroying/unloading it.
            bannerView.Destroy();
            bannerView = null;
            bannerViewHeight = 0f;

            // Reset top HUD to shift down from banner ad.
            gameUi.AdjustForBannerAd(this, false);
            isDestroyingBanner = false;
        }
    }

    public void StartBannerSession() {
#if !DEBUG_MODE
        if(bannerView == null) {
            CreateBannerInstance();
        }
#endif
    }

    public void EndBannerSession() {
#if !DEBUG_MODE
        if(bannerView != null) {
            isDestroyingBanner = true; // Queue ad destroy the first available frame that ad is loaded.
        }
#endif
    }

    public void WatchRewardVideo() {
        if(isWatchingRewardVid)
            return; // There is already a pending/existing request.

        // Request to load ad.
        isWatchingRewardVid = RequestNewRewardVideo();

        if(isWatchingRewardVid) {
            // Display loading dialog.
            DialogBoxUI.instance.Display("LOADING", "Please wait patiently while the ad loads", HandleVideoAdUserCancelLoad);
            DialogBoxUI.instance.cancelButtonLabel.text = "CANCEL";
        }
    }

    private void HandleVideoAdUserCancelLoad() {
        if(isShowingAd)
            return; // Cannot cancel loading when there's an ad showing.

        // Do not show ad when it loads successfully.
        isWatchingRewardVid = false;
    }

    private void HandleVideoAdLoaded(object sender, EventArgs args) {
        if(isWatchingRewardVid) {
            // Show the ad once it is done loading, provided that user does not cancel the loading.
            DialogBoxUI.instance.CloseAndReset();
            rewardVideo.Show();
            isShowingAd = true;
        }
    }

    private void HandleVideoAdRewarded(object sender, Reward args) {
        isWatchingRewardVid = false;
        isShowingAd = false;
        
        DialogBoxUI.instance.Display("REWARD", "You earned [FFF723]20 Frog Points[-]!", null);
        shop.AwardFrogPoints(20);

        int totalAdsWatched = PlayerPrefs.GetInt("AdsWatched", 0);
        int adsWatchedToday = PlayerPrefs.GetInt("AdsWatchedToday", 0);

        totalAdsWatched++;
        adsWatchedToday++;

        PlayerPrefs.SetInt("AdsWatched", totalAdsWatched);
        PlayerPrefs.SetInt("AdsWatchedToday", adsWatchedToday);
                
        achieve.UpdateProgressAll(true);
    }

    private void HandleVideoAdFailed(object sender, AdFailedToLoadEventArgs args) {
        if(isWatchingRewardVid) {
            DialogBoxUI.instance.Display("ERROR", "An ad cannot be played at this time. Please try again later.\nCode: " + args.Message, null);
        }

        isWatchingRewardVid = false;
        isShowingAd = false;
    }

    private void HandleVideoAdClosed(object sender, EventArgs args) {
        // User closed video ad prematurely.
        isWatchingRewardVid = false;
        isShowingAd = false;
    }

    private bool RequestNewRewardVideo() {
        // Determine if we need to reset ads watched today (day changed).
        int curAdDate = GetCurrentAdDay();
        int savedAdDate = PlayerPrefs.GetInt("AdCapCurrentDate", curAdDate);

        if(curAdDate > savedAdDate) {
            // Reset watch cap, and then set day to current.
            PlayerPrefs.SetInt("AdsWatchedToday", 0);
            PlayerPrefs.SetInt("AdCapCurrentDate", curAdDate);
        }

        // Determine if we reached the limit for today.
        int adsWatchedToday = PlayerPrefs.GetInt("AdsWatchedToday", 0);

        if(adsWatchedToday >= AD_WATCH_CAP) {
            DialogBoxUI.instance.Display("AD LIMIT REACHED", "You have reached your limit of " + AD_WATCH_CAP + " ads per day. Try again tomorrow!", null);
            return false;
        }

#if UNITY_ANDROID
        string testAdUnitID = "REDACTED";
#else
        string testAdUnitID = "unexpected_platform";
#endif

        AdRequest request = GetBuilder().Build();
        rewardVideo.LoadAd(request, testAdUnitID);
        return true;
    }

    private void CreateBannerInstance() {
        // This one only works for android
#if UNITY_ANDROID
        string testAdUnitID = "REDACTED";
#else
        string testAdUnitID = "unexpected_platform";
#endif

        bannerView = new BannerView(testAdUnitID, AdSize.Banner, AdPosition.Top);
        bannerView.OnAdLoaded += HandleBannerAdLoaded;

        AdRequest request = GetBuilder().Build();
        bannerView.LoadAd(request);

        isDestroyingBanner = false;
        isLoadingBanner = true;
    }

    public void HandleBannerAdLoaded(object sender, EventArgs args) {
        // Adjust elements.
        bannerViewHeight = bannerView.GetHeightInPixels();
        gameUi.AdjustForBannerAd(this, true);

        isLoadingBanner = false;
    }

    private int GetCurrentAdDay() {
        DateTime now = DateTime.Now;
        return (now.Year * 367) + now.DayOfYear;
    }

    private AdRequest.Builder GetBuilder() {
        AdRequest.Builder builder = new AdRequest.Builder();
        return builder;
    }
}