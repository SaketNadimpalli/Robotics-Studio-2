using UnityEngine;
using UnityEngine.UI;

[RequireComponent(typeof(ScrollRect))]
public class AutoScrollLog : MonoBehaviour
{
    [Tooltip("How close to bottom (in normalized units 0-1) counts as 'at bottom'")]
    public float stickThreshold = 0.05f;

    private ScrollRect scrollRect;
    private bool stickToBottom = true;
    private float lastContentHeight;

    void Awake()
    {
        scrollRect = GetComponent<ScrollRect>();
        scrollRect.onValueChanged.AddListener(OnScrolled);
    }

    void OnScrolled(Vector2 pos)
    {
        // verticalNormalizedPosition: 1 = top, 0 = bottom
        // User is "at bottom" if within threshold of 0
        stickToBottom = scrollRect.verticalNormalizedPosition <= stickThreshold;
    }

    void LateUpdate()
    {
        if (scrollRect.content == null) return;
        float currentHeight = scrollRect.content.rect.height;

        // Content grew (new log line arrived)
        if (currentHeight > lastContentHeight + 0.01f)
        {
            if (stickToBottom)
                scrollRect.verticalNormalizedPosition = 0f;
            // else: leave scroll position alone — user is reading history
        }

        lastContentHeight = currentHeight;
    }
}