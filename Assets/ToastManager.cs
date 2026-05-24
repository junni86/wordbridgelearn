using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

// 모든 플랫폼(PC/Android/iOS)에서 동작하는 UI 기반 토스트 팝업
public class ToastManager : MonoBehaviour
{
    public static ToastManager Instance { get; private set; }

    [Header("토스트 UI 연결")]
    [SerializeField] CanvasGroup   canvasGroup; // 토스트 패널의 CanvasGroup
    [SerializeField] TMP_Text      toastText;   // 메시지 텍스트
    [SerializeField] RectTransform panelRect;   // 토스트 패널 RectTransform

    [Header("토스트 설정")]
    [SerializeField] float fadeDuration    = 0.25f; // 페이드 인/아웃 시간(초)
    [SerializeField] float displayDuration = 2.0f;  // 표시 유지 시간(초)
    [SerializeField] float paddingX        = 60f;   // 좌우 여백
    [SerializeField] float paddingY        = 30f;   // 상하 여백
    [SerializeField] float maxWidth        = 900f;  // 최대 너비 (초과 시 줄바꿈)

    [Header("위치 설정")]
    [SerializeField] float bottomOffset = 300f; // 화면 하단에서 올라오는 거리(픽셀)

    // 도착한 메시지를 순서대로 보관하는 큐 — 빠르게 연속 호출되어도 손실 없이 하나씩 표시
    readonly Queue<string> messageQueue = new Queue<string>();
    // 현재 큐 소진 코루틴이 도는 중인지 — 중복 시작 방지
    bool isProcessingQueue = false;

    void Awake()
    {
        Instance = this;
        if (canvasGroup != null)
        {
            canvasGroup.alpha = 0f;
            canvasGroup.blocksRaycasts = false; // 초기 상태에서 클릭 차단 해제
        }
    }

    public void Show(string message)
    {
        if (canvasGroup == null || toastText == null) return;

        // 큐에 적재만 하고 즉시 반환 — 표시 작업은 ProcessQueue 가 순차 처리
        messageQueue.Enqueue(message);

        // 이미 처리 중이면 추가만 하고 종료, 아니면 새로 시작
        if (!isProcessingQueue)
        {
            StartCoroutine(ProcessQueue());
        }
    }

    // 큐가 빌 때까지 메시지를 하나씩 페이드인 → 유지 → 페이드아웃 완전 사이클로 표시
    // 이전엔 새 토스트가 오면 이전 코루틴을 즉시 중단해서 페이드 인 중이던 토스트가 사라지는 문제가 있었음
    IEnumerator ProcessQueue()
    {
        isProcessingQueue = true;
        while (messageQueue.Count > 0)
        {
            string next = messageQueue.Dequeue();
            yield return StartCoroutine(ShowRoutine(next));
        }
        isProcessingQueue = false;
    }

    IEnumerator ShowRoutine(string message)
    {
        toastText.text             = message;
        canvasGroup.alpha          = 0f;
        canvasGroup.blocksRaycasts = true; // 토스트 표시 중 클릭 차단 활성화

        // TMP 메시 강제 갱신 후 크기/위치 계산
        yield return null;
        ResizeAndPosition();

        // 페이드 인
        float t = 0f;
        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            canvasGroup.alpha = Mathf.Clamp01(t / fadeDuration);
            yield return null;
        }
        canvasGroup.alpha = 1f;

        // 표시 유지
        yield return new WaitForSeconds(displayDuration);

        // 페이드 아웃
        t = 0f;
        while (t < fadeDuration)
        {
            t += Time.deltaTime;
            canvasGroup.alpha = Mathf.Clamp01(1f - (t / fadeDuration));
            yield return null;
        }
        canvasGroup.alpha          = 0f;
        canvasGroup.blocksRaycasts = false; // 토스트 종료 후 클릭 차단 해제
    }

    void ResizeAndPosition()
    {
        if (panelRect == null || toastText == null) return;

        float availableWidth = maxWidth - paddingX * 2;

        // 최대 너비로 제한 후 메시 강제 갱신 → 정확한 preferredHeight 계산
        toastText.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, availableWidth);
        toastText.ForceMeshUpdate();

        float textWidth  = Mathf.Min(toastText.preferredWidth,  availableWidth);
        float textHeight = toastText.preferredHeight;

        // 패널 크기 적용
        panelRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, textWidth  + paddingX * 2);
        panelRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical,   textHeight + paddingY * 2);

        // 텍스트 rect를 실제 텍스트 너비에 맞게 재조정
        toastText.rectTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, textWidth);

        // 위치: 앵커를 하단 중앙 기준으로 bottomOffset 만큼 위로
        panelRect.anchorMin = new Vector2(0.5f, 0f);
        panelRect.anchorMax = new Vector2(0.5f, 0f);
        panelRect.pivot     = new Vector2(0.5f, 0f);
        panelRect.anchoredPosition = new Vector2(0f, bottomOffset);
    }
}
