using UnityEngine;
using GoogleMobileAds.Api;
using System;
using System.Collections;
using System.Runtime.InteropServices; // iOS 네이티브 ATT 플러그인 호출용

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

#if UNITY_IOS && !UNITY_EDITOR
    // iOS App Tracking Transparency (ATT) 네이티브 함수 (Plugins/iOS/ATTPlugin.mm 참조)
    [DllImport("__Internal")] private static extern void _RequestATTAuthorization();
    [DllImport("__Internal")] private static extern int _GetATTAuthorizationStatus();
#endif

    InterstitialAd interstitialAd;
    int retryCount = 0;

    // 보상 광고
    RewardedAd rewardedAd;
    int rewardedRetryCount = 0;

    // 디버깅용 토스트 헬퍼 — ToastManager가 아직 생성 전이거나 씬에 없을 수 있으니 null 안전 호출
    void DebugToast(string message)
    {
        Debug.Log("[AdsManager] " + message);
        if (ToastManager.Instance != null)
        {
            ToastManager.Instance.Show(message);
        }
    }

    void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
        DontDestroyOnLoad(gameObject);
        // iOS는 ATT 권한 다이얼로그 응답 후 SDK 초기화해야 광고가 정상적으로 로드됨
        StartCoroutine(InitializeAdsRoutine());
    }

    // 광고 SDK 초기화 루틴
    // iOS: ATT 권한 요청 → 사용자 응답 대기 → SDK 초기화 순서로 진행
    // Android/Editor: ATT 단계 건너뛰고 즉시 SDK 초기화
    IEnumerator InitializeAdsRoutine()
    {
#if UNITY_IOS && !UNITY_EDITOR
        // iOS 15+ 는 앱이 UIApplicationState.Active 일 때만 ATT 다이얼로그를 띄움
        // Awake 직후엔 아직 Inactive 라 _RequestATTAuthorization() 호출이 무시되는 경우가 있어
        // 첫 프레임 + 렌더 사이클 + 포커스 확보까지 대기한 뒤 호출
        yield return null;                      // 1프레임 양보 (Start 단계까지 진행)
        yield return new WaitForEndOfFrame();   // 렌더 사이클 완료 대기
        float focusWait = 3f;                   // 포커스 안전장치 (최대 3초)
        while (!Application.isFocused && focusWait > 0f)
        {
            yield return new WaitForSeconds(0.1f);
            focusWait -= 0.1f;
        }

        // 권한 상태 확인 — 0(NotDetermined)이면 다이얼로그를 띄워야 함
        int status = _GetATTAuthorizationStatus();
        if (status == 0)
        {
            _RequestATTAuthorization();
            // 사용자가 다이얼로그에 응답할 때까지 폴링 (최대 30초 안전장치)
            float timeout = 30f;
            while (_GetATTAuthorizationStatus() == 0 && timeout > 0f)
            {
                yield return new WaitForSeconds(0.3f);
                timeout -= 0.3f;
            }
            status = _GetATTAuthorizationStatus();
        }
        DebugToast($"ATT status: {status} (0:미응답 1:제한 2:거부 3:허용)");
#endif
        MobileAds.RaiseAdEventsOnUnityMainThread = true;
        MobileAds.Initialize(_ => DebugToast("SDK init complete"));
        LoadInterstitial();
        LoadRewarded();
        yield break;
    }

    void LoadInterstitial(int retry = 0)
    {
        if (retry >= 3) { DebugToast("전면광고 로드 3회 실패 — 포기"); return; }
        retryCount = retry;
        interstitialAd?.Destroy();
        InterstitialAd.Load(INTERSTITIAL_ID, new AdRequest(), (ad, error) =>
        {
            if (error != null)
            {
                DebugToast($"전면광고 로드 실패 ({retry + 1}/3): {error}");
                Invoke(nameof(LoadInterstitialRetry), 5f);
                return;
            }
            interstitialAd = ad;
            DebugToast("전면광고 로드 성공");
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
        if (retry >= 3) { DebugToast("보상광고 로드 3회 실패 — 포기"); return; }
        rewardedRetryCount = retry;
        rewardedAd?.Destroy();
        RewardedAd.Load(REWARDED_ID, new AdRequest(), (ad, error) =>
        {
            if (error != null)
            {
                DebugToast($"보상광고 로드 실패 ({retry + 1}/3): {error}");
                Invoke(nameof(LoadRewardedRetry), 5f);
                return;
            }
            rewardedAd = ad;
            DebugToast("보상광고 로드 성공");
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
            DebugToast("보상광고 준비 안 됨 — 닫힘 처리");
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
            DebugToast($"보상광고 표시 실패: {err}");
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
