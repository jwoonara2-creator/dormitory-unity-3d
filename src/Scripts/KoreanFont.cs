using UnityEngine;
using UnityEngine.UI;

// Put on a Canvas: swaps every Text to an OS font that can draw Hangul.
public class KoreanFont : MonoBehaviour
{
    void Awake()
    {
        var font = UiUtil.CreateKoreanFont(32);
        if (font == null) return;
        foreach (var t in GetComponentsInChildren<Text>(true)) t.font = font;
    }
}
