using UnityEngine;
using GoogleMobileAds.Api;
using System;
using System.Collections;

public class AdsManager : MonoBehaviour
{
    public static AdsManager Instance { get; private set; }

#if UNITY_IOS
    const string INTERSTITIAL_ID = "ca-app-pub-8504316980589965/6355811935"; // iOS
    // 보상 광고 — 사용자가 AdMob 콘솔에서 만든 실제 ID로 교체 필요. 테스트 ID 임시 사용
    const string REWARDED_ID = "ca-app-pub-8504316980589965/8735001600"; // iOS 테스트 ID
#else
    const string INTERSTITIAL_ID = "ca-app-pub-8504316980589965/4764251449"; // Android
    const string REWARDED_ID = "ca-app-pub-8504316980589965/3019640499"; // Android 테스트 ID (실제 ID로 교체)
#endif

    // 테스트 ID
    // Interstitial Android: ca-app-pub-3940256099942544/1033173712
    // Interstitial iOS:     ca-app-pub-3940256099942544/4411468910

    InterstitialAd interstitialAd;
    int retryCount = 0;

    // 보상 광고
    RewardedAd rewardedAd;
    int rewardedRetryCount = 0;

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        MobileAds.RaiseAdEventsOnUnityMainThread = true;
        MobileAds.Initialize(_ => Debug.Log("[AdsManager] SDK init complete"));
        LoadInterstitial();
        LoadRewarded();
    }

    void LoadInterstitial(int retry = 0)
    {
        if (retry >= 3) { Debug.LogWarning("[AdsManager] Load failed 3 times — giving up"); return; }
        retryCount = retry;
        interstitialAd?.Destroy();
        InterstitialAd.Load(INTERSTITIAL_ID, new AdRequest(), (ad, error) =>
        {
            if (error != null)
            {
                Debug.LogWarning($"[AdsManager] Load failed ({retry + 1}/3): {error}");
                Invoke(nameof(LoadInterstitialRetry), 5f);
                return;
            }
            interstitialAd = ad;
        });
    }

    void LoadInterstitialRetry() => LoadInterstitial(retryCount + 1);

    public void ShowInterstitial(Action onComplete = null)
    {
        // 광고 제거 구매자는 광고 스킵
        if (IAPManager.Instance != null && IAPManager.Instance.IsNoAds)
        {
            Debug.Log("[AdsManager] 광고 제거 구매자 — 광고 스킵");
            onComplete?.Invoke();
            return;
        }

        // IAPManager 없이도 PlayerPrefs 직접 확인 (안전망)
        if (PlayerPrefs.GetInt("NO_ADS", 0) == 1)
        {
            onComplete?.Invoke();
            return;
        }

        if (interstitialAd != null && interstitialAd.CanShowAd())
        {
            StartCoroutine(ShowInterstitialDelayed(onComplete));
        }
        else
        {
            onComplete?.Invoke();
            LoadInterstitial();
        }
    }

    IEnumerator ShowInterstitialDelayed(Action onComplete)
    {
        yield return new WaitForSecondsRealtime(0.5f);

        // GMA SDK가 Unity 스크립팅 스레드를 포그라운드로 인식하도록 Activity JNI 접촉
        HasWindowFocus();

        if (interstitialAd == null || !interstitialAd.CanShowAd())
        {
            onComplete?.Invoke();
            LoadInterstitial();
            yield break;
        }

        interstitialAd.OnAdFullScreenContentClosed += () => { onComplete?.Invoke(); LoadInterstitial(); };
        interstitialAd.OnAdFullScreenContentFailed += _ => { onComplete?.Invoke(); LoadInterstitial(); };
        interstitialAd.Show();
    }

    // ─── 보상 광고 (힌트 추가 충전 등에 사용) ───────────────────────────────

    void LoadRewarded(int retry = 0)
    {
        if (retry >= 3) { Debug.LogWarning("[AdsManager] Rewarded load failed 3 times — giving up"); return; }
        rewardedRetryCount = retry;
        rewardedAd?.Destroy();
        RewardedAd.Load(REWARDED_ID, new AdRequest(), (ad, error) =>
        {
            if (error != null)
            {
                Debug.LogWarning($"[AdsManager] Rewarded load failed ({retry + 1}/3): {error}");
                Invoke(nameof(LoadRewardedRetry), 5f);
                return;
            }
            rewardedAd = ad;
        });
    }

    void LoadRewardedRetry() => LoadRewarded(rewardedRetryCount + 1);

    // 보상 광고 표시 — 시청 완료 시 onReward 콜백 호출 (보상 수령 시점)
    // onClosed는 광고 닫힘 시점 (시청 여부 무관, UI 복귀용)
    public bool IsRewardedReady => rewardedAd != null && rewardedAd.CanShowAd();

    public void ShowRewardedAd(Action onReward, Action onClosed = null)
    {
        if (rewardedAd == null || !rewardedAd.CanShowAd())
        {
            Debug.LogWarning("[AdsManager] Rewarded 광고 준비 안 됨 — 보상 없이 닫힘 처리");
            onClosed?.Invoke();
            LoadRewarded();
            return;
        }

        bool rewardGranted = false;
        rewardedAd.OnAdFullScreenContentClosed += () =>
        {
            onClosed?.Invoke();
            LoadRewarded(); // 다음 광고 미리 로드
        };
        rewardedAd.OnAdFullScreenContentFailed += err =>
        {
            Debug.LogWarning($"[AdsManager] Rewarded show failed: {err}");
            onClosed?.Invoke();
            LoadRewarded();
        };

        rewardedAd.Show(_ =>
        {
            if (rewardGranted) return;
            rewardGranted = true;
            onReward?.Invoke();
        });
    }

    bool HasWindowFocus()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        try
        {
            using var player = new AndroidJavaClass("com.unity3d.player.UnityPlayer");
            using var activity = player.GetStatic<AndroidJavaObject>("currentActivity");
            return activity.Call<bool>("hasWindowFocus");
        }
        catch { return true; }
#else
        return true;
#endif
    }
}
