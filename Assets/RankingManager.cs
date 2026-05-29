using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;
using UnityEngine.UI;
using TMPro;

// ─── JSON 직렬화용 데이터 클래스 ──────────────────────────────────────────────

[Serializable]
public class RankSubmitRequest
{
    public string id;
    public int score;
    public int stage;
    public string country;
}

[Serializable]
public class RankSubmitResponse
{
    public bool success;
    public string message;
}

[Serializable]
public class RankingEntry
{
    public string id;
    public int score;
    public int stage;
    public string country;
}

[Serializable]
public class RankingFetchResponse
{
    public bool success;
    public List<RankingEntry> rankings;
    public int page;
    public int limit;
    public bool hasNextPage;
}

[Serializable]
public class CheckIdResponse
{
    public bool success;
    public bool exists;
}

// ─── RankingManager: 랭킹 시스템 전체 관리 ───────────────────────────────────

public class RankingManager : MonoBehaviour
{
    [Header("서버 URL (Inspector에서 수정)")]
    public string serverUrl = "https://YOUR_SERVER_URL";

    [Header("메인 메뉴 매니저 연결")]
    [SerializeField] MainMenuManager mainMenuManager; // 점수 UI 새로고침용

    [Header("패널 오브젝트")]
    public GameObject mainPanel;      // 메인 화면
    public GameObject idInputPanel;   // ID 입력 화면
    public GameObject rankingPanel;   // 랭킹 조회 화면

    [Header("메인 패널 버튼")]
    public Button myBestScoreButton;  // My Best Score 버튼
    public Button topScoreButton;     // Top Score 버튼

    [Header("ID 입력 패널")]
    public TMP_InputField idInputField; // ID 입력창
    public Button submitButton;         // 확인 버튼
    public Button cancelButton;         // 취소 버튼
    public TMP_Text messageText;        // 안내/에러 메시지

    [Header("랭킹 패널")]
    public Transform rankingContentParent; // 랭킹 행이 생성될 부모 (ScrollView Content)
    public GameObject rankingRowPrefab;    // 랭킹 행 프리팹
    public Button nextPageButton;          // 다음 페이지
    public Button previousPageButton;      // 이전 페이지
    public Button backButton;              // 뒤로가기
    public TMP_Text rankingStatusText;     // 로딩/에러 상태 표시

    int currentPage = 1;         // 현재 페이지 번호
    const int PAGE_LIMIT = 13;   // 페이지당 표시 인원
    bool isBusy = false;         // 통신 중 여부 (중복 클릭 방지)

    void Start()
    {
        // 시작 시 메인 패널만 표시
        ShowMainPanel();
        RegisterButtons();

        // 서버 1위 점수 가져와서 메인 메뉴에 표시
        StartCoroutine(FetchTopScoreOnStart());

        // 저장된 ID가 있으면 서버에 존재 여부 확인
        string savedId = PlayerPrefs.GetString("RankingPlayerId", "");
        if (!string.IsNullOrEmpty(savedId))
            StartCoroutine(CheckIdOnStart(savedId));
    }

    // 메인 메뉴 진입 시 서버 1위 점수를 읽어와 serverHighScoreText 갱신
    IEnumerator FetchTopScoreOnStart()
    {
        string url = $"{serverUrl}/ranking/top.php?page=1&limit=1";
        UnityWebRequest www = UnityWebRequest.Get(url);
        www.timeout = 5; // 5초 안에 응답 없으면 그냥 넘어감

        yield return www.SendWebRequest();

        if (www.result != UnityWebRequest.Result.Success)
        {
            www.Dispose();
            yield break;
        }

        RankingFetchResponse res = JsonUtility.FromJson<RankingFetchResponse>(www.downloadHandler.text);
        www.Dispose();

        // 1위 항목이 있으면 메인 메뉴 서버 최고 점수 텍스트 갱신
        if (res.success && res.rankings != null && res.rankings.Count > 0)
        {
            if (mainMenuManager != null)
                mainMenuManager.SetServerHighScore(res.rankings[0].score);
        }
    }

    // 게임 시작 시 저장된 ID가 서버에 있는지 확인
    // 없으면 랭킹 관련 데이터 초기화, 서버 응답 없으면 그냥 넘어감
    IEnumerator CheckIdOnStart(string savedId)
    {
        string url = $"{serverUrl}/ranking/check.php?id={UnityWebRequest.EscapeURL(savedId)}";
        UnityWebRequest www = UnityWebRequest.Get(url);
        www.timeout = 5; // 5초 안에 응답 없으면 그냥 넘어감

        yield return www.SendWebRequest();

        // 서버 응답 없거나 네트워크 오류 → 그냥 넘어감
        if (www.result != UnityWebRequest.Result.Success)
        {
            www.Dispose();
            yield break;
        }

        CheckIdResponse res = JsonUtility.FromJson<CheckIdResponse>(www.downloadHandler.text);
        www.Dispose();

        // 서버에 ID가 없으면 랭킹 관련 PlayerPrefs 초기화
        if (res.success && !res.exists)
        {
            Debug.Log($"[Ranking] ID '{savedId}' 서버에 없음 → 랭킹 데이터 초기화");
            PlayerPrefs.DeleteKey("RankingPlayerId");
            PlayerPrefs.DeleteKey("LastSubmittedScore");
            PlayerPrefs.DeleteKey("BestScore");
            PlayerPrefs.DeleteKey("BestStage");
            PlayerPrefs.Save();

            // 메인 메뉴 점수 UI 즉시 갱신
            if (mainMenuManager != null) mainMenuManager.RefreshScoreDisplay();
        }
    }

    // 버튼 이벤트 일괄 등록
    void RegisterButtons()
    {
        myBestScoreButton.onClick.AddListener(OnMyBestScoreClicked);
        topScoreButton.onClick.AddListener(OnTopScoreClicked);
        submitButton.onClick.AddListener(OnSubmitClicked);
        cancelButton.onClick.AddListener(OnCancelClicked);
        nextPageButton.onClick.AddListener(OnNextPageClicked);
        previousPageButton.onClick.AddListener(OnPreviousPageClicked);
        backButton.onClick.AddListener(OnBackClicked);
    }

    // ─── 패널 전환 ────────────────────────────────────────────────────────────

    void ShowMainPanel()
    {
        mainPanel.SetActive(true);
        idInputPanel.SetActive(false);
        rankingPanel.SetActive(false);
    }

    void ShowIdInputPanel()
    {
        mainPanel.SetActive(false);
        idInputPanel.SetActive(true);
        rankingPanel.SetActive(false);

        // 이전에 저장된 ID가 있으면 자동 입력 후 수정 불가 처리
        string savedId = PlayerPrefs.GetString("RankingPlayerId", "");
        if (!string.IsNullOrEmpty(savedId))
        {
            idInputField.text = savedId;
            idInputField.interactable = false; // ID 변경 불가
        }
        else
        {
            idInputField.text = "";
            idInputField.interactable = true;
        }

        SetMessage("");
    }

    void ShowRankingPanel()
    {
        mainPanel.SetActive(false);
        idInputPanel.SetActive(false);
        rankingPanel.SetActive(true);
    }

    // ─── 외부 호출용 public 메서드 (MainMenuManager에서 호출) ─────────────────

    // My Best Score 버튼 → ID 입력 패널 열기
    public void OpenMyScore()
    {
        if (isBusy) return;
        ShowIdInputPanel();
    }

    // Top Score 버튼 → 랭킹 패널 열기
    public void OpenTopScore()
    {
        if (isBusy) return;
        currentPage = 1;
        ShowRankingPanel();
        StartCoroutine(FetchRankings(currentPage));
    }

    // ─── 버튼 핸들러 ──────────────────────────────────────────────────────────

    void OnMyBestScoreClicked()
    {
        OpenMyScore();
    }

    void OnTopScoreClicked()
    {
        OpenTopScore();
    }

    void OnSubmitClicked()
    {
        if (isBusy) return;

        string id = idInputField.text.Trim();

        // ID가 비어있으면 전송 안 함
        if (string.IsNullOrEmpty(id))
        {
            SetMessage("Please enter your ID.");
            return;
        }

        int bestScore = PlayerPrefs.GetInt("BestScore", 0);
        int lastSubmitScore = PlayerPrefs.GetInt("LastSubmittedScore", -1);

        // 이전에 제출한 점수보다 높을 때만 전송
        if (bestScore <= lastSubmitScore)
        {
            SetMessage("Your best score is already registered.");
            return;
        }

        // 저장된 ID가 있으면 suffix 포함된 ID 그대로 사용, 없으면 새 ID에 suffix 붙이기
        string savedRankingId = PlayerPrefs.GetString("RankingPlayerId", "");
        string finalId;
        if (!string.IsNullOrEmpty(savedRankingId))
        {
            // 이미 저장된 ID (suffix 포함) → 그대로 사용
            finalId = savedRankingId;
        }
        else
        {
            // 새 ID 입력 → 기기 고유 ID 뒤 4자리 suffix 붙이기 (예: player001_ab3f)
            string deviceId = SystemInfo.deviceUniqueIdentifier;
            string deviceSuffix = deviceId.Substring(Mathf.Max(0, deviceId.Length - 4));
            finalId = $"{id}_{deviceSuffix}";
        }

        StartCoroutine(SubmitRanking(finalId));
    }

    void OnCancelClicked()
    {
        if (isBusy) return;
        ShowMainPanel();
    }

    void OnNextPageClicked()
    {
        if (isBusy) return;
        currentPage++;
        StartCoroutine(FetchRankings(currentPage));
    }

    void OnPreviousPageClicked()
    {
        if (isBusy) return;
        if (currentPage <= 1) return;
        currentPage--;
        StartCoroutine(FetchRankings(currentPage));
    }

    void OnBackClicked()
    {
        if (isBusy) return;
        ShowMainPanel();
    }

    // ─── 서버 통신: 점수 제출 ─────────────────────────────────────────────────

    IEnumerator SubmitRanking(string id)
    {
        SetBusy(true);
        SetMessage("Submitting...");

        // PlayerPrefs에서 최고 점수/스테이지 읽기
        int bestScore = PlayerPrefs.GetInt("BestScore", 0);
        int bestStage = PlayerPrefs.GetInt("BestStage", 1);
        // 서버 업로드 직전 PlayerPrefs 실제 읽힌 값 — stage 갱신 누락 원인 추적용
        Debug.Log($"[Ranking/Submit] 업로드 직전 PlayerPrefs 읽기 → BestScore={bestScore}, BestStage={bestStage}");

        // 국가 코드 읽기 (예: KR, US)
        string country = RegionInfo.CurrentRegion.TwoLetterISORegionName;
        // Debug.Log($"[Ranking] 국가 코드: {country}");

        // 요청 JSON 생성
        string json = JsonUtility.ToJson(new RankSubmitRequest
        {
            id = id,
            score = bestScore,
            stage = bestStage,
            country = country
        });
        // Debug.Log($"[Ranking] 전송 JSON: {json}");

        UnityWebRequest www = new UnityWebRequest($"{serverUrl}/ranking/submit.php", "POST");
        www.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
        www.downloadHandler = new DownloadHandlerBuffer();
        www.SetRequestHeader("Content-Type", "application/json");
        www.timeout = 10; // 10초 안에 응답 없으면 실패 처리

        yield return www.SendWebRequest();

        // 디버그: HTTP 응답 결과 로그
        Debug.Log($"[Ranking] Submit result: {www.result} | code: {www.responseCode} | error: {www.error}");
        Debug.Log($"[Ranking] Submit body: {www.downloadHandler.text}");

        if (www.result == UnityWebRequest.Result.Success)
        {
            RankSubmitResponse res = JsonUtility.FromJson<RankSubmitResponse>(www.downloadHandler.text);

            if (res.success)
            {
                // 성공: ID와 제출 점수 저장 후 메인 화면으로 복귀
                Debug.Log("[Ranking] Submit success → 메인 화면으로 이동");
                PlayerPrefs.SetString("RankingPlayerId", id);
                PlayerPrefs.SetInt("LastSubmittedScore", PlayerPrefs.GetInt("BestScore", 0));
                PlayerPrefs.Save();
                www.Dispose();
                SetBusy(false);
                ShowMainPanel();
                yield break;
            }

            // 서버가 success: false 반환
            Debug.Log($"[Ranking] Submit failed: {res.message}");
            SetMessage($"Error: {res.message}");
        }
        else
        {
            // 네트워크 오류
            Debug.Log($"[Ranking] Submit network error: {www.error}");
            SetMessage($"Failed: {www.error}");
        }

        www.Dispose();
        SetBusy(false);
    }

    // ─── 서버 통신: 랭킹 조회 ─────────────────────────────────────────────────

    IEnumerator FetchRankings(int page)
    {
        SetBusy(true);
        SetRankingStatus("Loading...");
        ClearRankingRows();

        // 조회 전 페이지 버튼 임시 비활성화
        previousPageButton.interactable = false;
        nextPageButton.interactable = false;

        string url = $"{serverUrl}/ranking/top.php?page={page}&limit={PAGE_LIMIT}";
        UnityWebRequest www = UnityWebRequest.Get(url);
        www.timeout = 10; // 10초 안에 응답 없으면 실패 처리

        yield return www.SendWebRequest();

        // 디버그: HTTP 응답 결과 로그
        Debug.Log($"[Ranking] Fetch result: {www.result} | code: {www.responseCode} | error: {www.error}");
        Debug.Log($"[Ranking] Fetch body: {www.downloadHandler.text}");

        if (www.result == UnityWebRequest.Result.Success)
        {
            RankingFetchResponse res = JsonUtility.FromJson<RankingFetchResponse>(www.downloadHandler.text);

            if (res.success)
            {
                Debug.Log($"[Ranking] Fetch success: {res.rankings?.Count}개 항목");
                SetRankingStatus("");
                DisplayRankings(res);

                // 페이지 버튼 상태 갱신
                previousPageButton.interactable = (page > 1);
                nextPageButton.interactable = res.hasNextPage;
            }
            else
            {
                Debug.Log("[Ranking] Fetch success:false 반환");
                SetRankingStatus("Failed to load rankings.");
            }
        }
        else
        {
            Debug.Log($"[Ranking] Fetch network error: {www.error}");
            SetRankingStatus($"Error: {www.error}");
        }

        www.Dispose();
        SetBusy(false);
    }

    // ─── 랭킹 UI 표시 ────────────────────────────────────────────────────────

    // 서버 응답 데이터를 받아 랭킹 행 프리팹을 생성해 표시
    void DisplayRankings(RankingFetchResponse res)
    {
        // 현재 페이지의 시작 순위 (1페이지 → 1위, 2페이지 → 16위 ...)
        int startRank = (res.page - 1) * res.limit + 1;

        for (int i = 0; i < res.rankings.Count; i++)
        {
            RankingEntry entry = res.rankings[i];
            GameObject row = Instantiate(rankingRowPrefab, rankingContentParent);

            if (row.TryGetComponent(out RankingRowItem item))
                item.SetData(startRank + i, entry.id, entry.country, entry.score, entry.stage);
        }
    }

    // 랭킹 행 전체 제거
    void ClearRankingRows()
    {
        foreach (Transform child in rankingContentParent)
            Destroy(child.gameObject);
    }

    // ─── 유틸 ─────────────────────────────────────────────────────────────────

    // 통신 중 상태 설정 — 패널별 버튼 잠금/해제
    void SetBusy(bool busy)
    {
        isBusy = busy;
        if (submitButton != null) submitButton.interactable = !busy;
        if (cancelButton != null) cancelButton.interactable = !busy;
        if (backButton != null) backButton.interactable = !busy;
        if (myBestScoreButton != null) myBestScoreButton.interactable = !busy;
        if (topScoreButton != null) topScoreButton.interactable = !busy;
    }

    // ID 입력 패널 안내 메시지 설정 + 토스트 동시 표시
    void SetMessage(string msg)
    {
        if (messageText != null)
            messageText.text = msg;

        if (!string.IsNullOrEmpty(msg))
            ToastManager.Instance?.Show(msg);
    }

    // 랭킹 패널 상태 메시지 설정 + 토스트 동시 표시
    void SetRankingStatus(string msg)
    {
        if (rankingStatusText != null)
            rankingStatusText.text = msg;

        if (!string.IsNullOrEmpty(msg))
            ToastManager.Instance?.Show(msg);
    }
}
