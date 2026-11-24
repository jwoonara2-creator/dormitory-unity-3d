using System.Collections;
using UnityEngine;
using UnityEngine.UI;

// A short "how to use" hint ("터치로 화면 제어") that appears for about two seconds every time the user enters
// a room, then fades out by itself. It never blocks touches (the user can start dragging right away).
public class FirstEntryHint : MonoBehaviour
{
    const float FadeIn = 0.25f;
    const float Hold = 1.4f;   // fully visible
    const float FadeOut = 0.4f; // total ~2 seconds

    static FirstEntryHint current;

    public static void Show(string message)
    {
        if (current != null) Destroy(current.gameObject); // never stack two hints

        var go = new GameObject("FirstEntryHint");
        current = go.AddComponent<FirstEntryHint>();
        current.Build(message);
    }

    CanvasGroup group;

    void Build(string message)
    {
        var canvasGo = new GameObject("HintCanvas", typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster), typeof(CanvasGroup));
        canvasGo.transform.SetParent(transform, false);
        var canvas = canvasGo.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 60;
        var scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(720, 1280);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;

        group = canvasGo.GetComponent<CanvasGroup>();
        group.alpha = 0f;
        group.blocksRaycasts = false; // never swallow the first drag
        group.interactable = false;

        var pill = new GameObject("Pill", typeof(RectTransform), typeof(Image));
        pill.transform.SetParent(canvasGo.transform, false);
        var rt = (RectTransform)pill.transform;
        rt.anchorMin = rt.anchorMax = rt.pivot = new Vector2(0.5f, 0.5f);
        rt.anchoredPosition = new Vector2(0f, -250f); // a little below the center, clear of the title and buttons
        rt.sizeDelta = new Vector2(500f, 96f);
        var img = pill.GetComponent<Image>();
        img.color = new Color(0.08f, 0.1f, 0.14f, 0.4f);
        img.raycastTarget = false;

        var textGo = new GameObject("Label", typeof(RectTransform), typeof(Text));
        textGo.transform.SetParent(pill.transform, false);
        var trt = (RectTransform)textGo.transform;
        trt.anchorMin = Vector2.zero; trt.anchorMax = Vector2.one; trt.offsetMin = trt.offsetMax = Vector2.zero;
        var text = textGo.GetComponent<Text>();
        var bundled = Resources.Load<Font>("KoreanFont");
        text.font = bundled != null ? bundled : UiUtil.CreateKoreanFont(32);
        text.fontSize = 38;
        text.alignment = TextAnchor.MiddleCenter;
        text.color = Color.white;
        text.raycastTarget = false;
        text.text = message;

        StartCoroutine(Play());
    }

    IEnumerator Play()
    {
        yield return Fade(0f, 1f, FadeIn);
        yield return new WaitForSecondsRealtime(Hold);
        yield return Fade(1f, 0f, FadeOut);
        Destroy(gameObject);
    }

    IEnumerator Fade(float from, float to, float seconds)
    {
        for (float t = 0f; t < seconds; t += Time.unscaledDeltaTime)
        {
            if (group != null) group.alpha = Mathf.Lerp(from, to, t / seconds);
            yield return null;
        }
        if (group != null) group.alpha = to;
    }
}
