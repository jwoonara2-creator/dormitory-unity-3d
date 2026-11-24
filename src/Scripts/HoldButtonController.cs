using System.Collections;
using UnityEngine;
using UnityEngine.EventSystems;

public class HoldButtonController : MonoBehaviour, IPointerDownHandler, IPointerUpHandler
{
    [Header("GPS Icon Reference")]
    [Tooltip("누르고 있을 때 통통 뛸 GPS 아이콘 GameObject")]
    public GameObject targetGpsIcon;

    [Header("Animation Settings")]
    [Tooltip("버튼이 커질 비율 (기본 1.1배)")]
    public float scaleMultiplier = 1.1f;

    [Tooltip("GPS 아이콘이 위아래로 움직이는 높이 (값 조절을 통해 뛰는 폭 조절)")]
    public float bounceHeight = 15f;

    [Tooltip("GPS 아이콘이 뛰는 속도")]
    public float bounceSpeed = 15f;

    private Vector3 originalButtonScale;
    private Vector3 originalGpsPos;
    private Coroutine bounceCoroutine;

    void Start()
    {
        // 버튼의 원래 크기 저장
        originalButtonScale = transform.localScale;

        // GPS 아이콘의 초기 위치 저장 (로컬 좌표 기준)
        if (targetGpsIcon != null)
        {
            originalGpsPos = targetGpsIcon.transform.localPosition;
        }
    }

    // 터치를 시작하는 순간 (버튼을 꾹 누를 때)
    public void OnPointerDown(PointerEventData eventData)
    {
        // 1. 버튼 크기 키우기
        transform.localScale = originalButtonScale * scaleMultiplier;

        // 2. GPS 아이콘 통통 뛰기 시작
        if (targetGpsIcon != null)
        {
            if (bounceCoroutine != null)
            {
                StopCoroutine(bounceCoroutine);
            }
            bounceCoroutine = StartCoroutine(BounceRoutine());
        }
    }

    // 터치를 끝내는 순간 (손을 뗄 때)
    public void OnPointerUp(PointerEventData eventData)
    {
        // 1. 버튼 크기 원래대로 복구
        transform.localScale = originalButtonScale;

        // 2. GPS 아이콘 뛰기 멈추고 원래 위치로 복구
        if (targetGpsIcon != null)
        {
            if (bounceCoroutine != null)
            {
                StopCoroutine(bounceCoroutine);
                bounceCoroutine = null;
            }
            targetGpsIcon.transform.localPosition = originalGpsPos;
        }
    }

    // Y축으로 통통 뛰게 만드는 코루틴
    IEnumerator BounceRoutine()
    {
        while (true)
        {
            // Mathf.Sin을 이용해 시간에 따라 위아래로 왕복하는 값 생성
            float newY = originalGpsPos.y + Mathf.Sin(Time.time * bounceSpeed) * bounceHeight;

            Vector3 currentPos = targetGpsIcon.transform.localPosition;
            currentPos.y = newY;
            targetGpsIcon.transform.localPosition = currentPos;

            yield return null; // 매 프레임 대기
        }
    }
}
