using UnityEngine;
using System.Collections;
#if UNITY_ANDROID && !UNITY_EDITOR
using Google.Play.AppUpdate;
using Google.Play.Common;
#endif
#if UNITY_IOS && !UNITY_EDITOR
using UnityEngine.Networking;
#endif

// 인앱 업데이트 매니저 — Android/iOS 통합
// - Android: Google Play Immediate Update (OS 레벨 강제 업데이트 UX)
// - iOS: iTunes Lookup API로 최신 버전 확인 후 App Store 페이지 열기 (사용자 자율 업데이트)
// - Editor / 기타 플랫폼: 동작 안 함
public class InAppUpdateManager : MonoBehaviour
{
    // 씬 재로드 시 중복 생성 방지를 위한 정적 인스턴스 참조
    private static InAppUpdateManager instance;

    [Header("iOS App Store 설정")]
    [Tooltip("App Store Connect에서 확인 가능한 9~10자리 앱 숫자 ID. iOS 빌드 전에 반드시 입력.")]
    [SerializeField] private string iosAppStoreId = "";

#if UNITY_ANDROID && !UNITY_EDITOR
    private AppUpdateManager appUpdateManager;
#endif

    private void Awake()
    {
        // 이미 인스턴스가 존재하면 본 오브젝트는 파기 (중복 업데이트 체크 방지)
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }

        instance = this;
        // 씬이 다시 로드되어도 유지하여 업데이트 체크가 반복되지 않도록 함
        DontDestroyOnLoad(gameObject);
    }

    private void Start()
    {
#if UNITY_ANDROID && !UNITY_EDITOR
        // 안드로이드 디바이스: Google Play Immediate Update
        appUpdateManager = new AppUpdateManager();
        StartCoroutine(CheckForAndroidUpdate());
#elif UNITY_IOS && !UNITY_EDITOR
        // iOS 디바이스: iTunes Lookup → App Store 유도
        StartCoroutine(CheckForIosUpdate());
#endif
    }

    // ─── Android (Google Play In-App Update) ─────────────────────────────────

#if UNITY_ANDROID && !UNITY_EDITOR
    private IEnumerator CheckForAndroidUpdate()
    {
        // Play Store에 현재 앱의 업데이트 가능 여부 조회
        var appUpdateInfoOperation = appUpdateManager.GetAppUpdateInfo();

        yield return appUpdateInfoOperation;

        // 조회 실패 시 로그만 남기고 종료
        if (!appUpdateInfoOperation.IsSuccessful)
        {
            Debug.Log("Update check failed: " + appUpdateInfoOperation.Error);
            yield break;
        }

        var appUpdateInfo = appUpdateInfoOperation.GetResult();

        // 이전 실행 중 Immediate 업데이트가 중단되었던 경우 자동 재개
        if (appUpdateInfo.UpdateAvailability == UpdateAvailability.DeveloperTriggeredUpdateInProgress)
        {
            StartCoroutine(StartImmediateUpdate(appUpdateInfo));
            yield break;
        }

        // 신규 업데이트가 있고, Immediate 타입이 허용되는 경우에만 업데이트 시작
        if (appUpdateInfo.UpdateAvailability == UpdateAvailability.UpdateAvailable
            && appUpdateInfo.IsUpdateTypeAllowed(AppUpdateOptions.ImmediateAppUpdateOptions()))
        {
            StartCoroutine(StartImmediateUpdate(appUpdateInfo));
        }
    }

    private IEnumerator StartImmediateUpdate(AppUpdateInfo appUpdateInfo)
    {
        // 즉시(Immediate) 업데이트 흐름으로 Play Store UI 호출
        var startUpdateRequest = appUpdateManager.StartUpdate(
            appUpdateInfo,
            AppUpdateOptions.ImmediateAppUpdateOptions()
        );

        yield return startUpdateRequest;

        // 업데이트 시작 중 오류 발생 시 로그 기록
        if (startUpdateRequest.Error != AppUpdateErrorCode.NoError)
        {
            Debug.Log("Update Error: " + startUpdateRequest.Error);
        }
    }
#endif

    // ─── iOS (iTunes Lookup + App Store URL) ─────────────────────────────────

#if UNITY_IOS && !UNITY_EDITOR
    // iTunes Lookup 응답 JSON 파싱용 클래스 (JsonUtility는 최상위 클래스만 매핑)
    [System.Serializable]
    private class ITunesLookupResponse
    {
        public int resultCount;
        public ITunesAppInfo[] results;
    }

    [System.Serializable]
    private class ITunesAppInfo
    {
        public string version;     // App Store 최신 버전
        public string trackViewUrl; // App Store 페이지 URL (Fallback용)
    }

    private IEnumerator CheckForIosUpdate()
    {
        if (string.IsNullOrEmpty(iosAppStoreId))
        {
            Debug.LogWarning("[InAppUpdate] iOS App Store ID가 설정되지 않아 업데이트 체크를 건너뜀");
            yield break;
        }

        // iTunes Lookup API — Apple이 운영하는 공개 엔드포인트, 별도 인증 불필요
        string url = $"https://itunes.apple.com/lookup?id={iosAppStoreId}";

        using (UnityWebRequest req = UnityWebRequest.Get(url))
        {
            yield return req.SendWebRequest();

            if (req.result != UnityWebRequest.Result.Success)
            {
                Debug.Log("iOS update check failed: " + req.error);
                yield break;
            }

            // 응답 파싱
            ITunesLookupResponse parsed = null;
            try
            {
                parsed = JsonUtility.FromJson<ITunesLookupResponse>(req.downloadHandler.text);
            }
            catch (System.Exception e)
            {
                Debug.Log("[InAppUpdate] iTunes 응답 파싱 실패: " + e.Message);
                yield break;
            }

            // 출시 직후 등 응답에 결과가 없을 수 있음 → 그냥 종료
            if (parsed == null || parsed.resultCount <= 0 || parsed.results == null || parsed.results.Length == 0)
            {
                Debug.Log("[InAppUpdate] iTunes 응답에 앱 정보 없음 (출시 직후이거나 잘못된 ID)");
                yield break;
            }

            string latestVersion = parsed.results[0].version;
            if (string.IsNullOrEmpty(latestVersion))
            {
                Debug.Log("[InAppUpdate] iTunes 응답에서 버전 정보 누락");
                yield break;
            }

            // 원격 버전이 현재 버전보다 높으면 App Store 열기
            if (IsRemoteVersionNewer(Application.version, latestVersion))
            {
                Debug.Log($"[InAppUpdate] 새 버전 발견 — current={Application.version}, latest={latestVersion}");
                // itms-apps:// 스킴으로 App Store 앱이 즉시 해당 페이지로 열림
                Application.OpenURL($"itms-apps://itunes.apple.com/app/id{iosAppStoreId}");
            }
        }
    }

    // "1.2.3" 형식의 시맨틱 버전을 컴포넌트별로 비교 — remote 가 current 보다 높으면 true
    private static bool IsRemoteVersionNewer(string current, string remote)
    {
        if (string.IsNullOrEmpty(current) || string.IsNullOrEmpty(remote)) return false;

        string[] c = current.Split('.');
        string[] r = remote.Split('.');
        int len = Mathf.Max(c.Length, r.Length);

        for (int i = 0; i < len; i++)
        {
            int ci = (i < c.Length && int.TryParse(c[i], out var v1)) ? v1 : 0;
            int ri = (i < r.Length && int.TryParse(r[i], out var v2)) ? v2 : 0;
            if (ri > ci) return true;
            if (ri < ci) return false;
        }
        return false;
    }
#endif
}
