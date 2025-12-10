using System;
using UnityEngine;
using TMPro;

public class SpeechCanvas : MonoBehaviour
{
    [Header("Assignables")]
    private Camera _cam;
    private RectTransform _chatBubbleRectTransform;
    public GameObject ChatBubble;
    
    [Header("Pointing Stuff")]
    public RectTransform BubblePointer; // that points at who is talking
    public TextMeshProUGUI TextBox;
    private Transform _pointTo;
    private bool _isPointing = false;

    private void OnEnable()
    {
        _cam = Camera.main;
        _chatBubbleRectTransform = ChatBubble.GetComponent<RectTransform>();
    }

    public void SetText(string text)
    {
        TextBox.text = text;
    }

    private void Update()
    {
        if (_isPointing)
        {
            PointTo(_pointTo.position);
        }
    }

    public void StartPointing(Transform worldTransform)
    {
        _pointTo = worldTransform;
        _isPointing = true;
    }

    public void StopPointing()
    {
        _isPointing = false;
    }

    private void PointTo(Vector3 worldPos)
    {
        if (!IsCharacterOnScreen(worldPos))
            return;
        
        // 1. Convert both ends of the tail to *screen* coordinates
        Vector2 bubbleScreen = RectTransformUtility
            .WorldToScreenPoint(null, _chatBubbleRectTransform.position); // UI → screen
        Vector2 targetScreen = _cam.WorldToScreenPoint(worldPos);                            // 3-D → screen

        // 2. Convert those screen coords into the local space of the Pointer’s parent
        RectTransform parent = (RectTransform)BubblePointer.parent;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parent, bubbleScreen, null, out Vector2 localStart);  // ← camera is null
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            parent, targetScreen, null, out Vector2 localEnd);    // ← camera is null

        // 3. Stretch & rotate the pointer
        Vector2 dir    = localEnd - localStart;
        float   length = dir.magnitude;
        
        BubblePointer.localPosition = localStart + dir * 0.5f;
        BubblePointer.sizeDelta     = new Vector2(BubblePointer.sizeDelta.x, length);
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg - 90f;
        BubblePointer.localRotation = Quaternion.Euler(0, 0, angle);
    }

    private bool IsCharacterOnScreen(Vector3 worldPos)
    {
        Vector3 vp = _cam.WorldToViewportPoint(worldPos);
        bool onScreen = vp.z > 0f                // in front of the camera
                        && vp.x is >= 0f and <= 1f  // inside left/right
                        && vp.y is >= 0f and <= 1f; // inside top/bottom

        BubblePointer.gameObject.SetActive(onScreen);
        
        return onScreen;
    }
}
