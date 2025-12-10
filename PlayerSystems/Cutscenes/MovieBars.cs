using UnityEngine;
using DG.Tweening;

public class MovieBars : MonoBehaviour
{
    [SerializeField] RectTransform topBar;
    [SerializeField] RectTransform bottomBar;

    Vector2 _topOriginal;
    Vector2 _bottomOriginal;

    void Awake()
    {
        _topOriginal = topBar.anchoredPosition;
        _bottomOriginal = bottomBar.anchoredPosition;
    }

    public void PlayEnterAnimation(float duration, System.Action onComplete)
    {
        float topHeight = topBar.rect.height;
        float bottomHeight = bottomBar.rect.height;
        
        // kill if accidentally there were previous tweens
        DOTween.Kill(topBar);
        DOTween.Kill(bottomBar);

        // teleport offscreen (relative to final)
        topBar.anchoredPosition += new Vector2(0,  topHeight);
        bottomBar.anchoredPosition += new Vector2(0, -bottomHeight);

        var tweenCount = 2;
        void OneDone()
        {
            tweenCount--;
            if (tweenCount <= 0)
                onComplete?.Invoke();
        }

        topBar.DOAnchorPos(_topOriginal, duration)
            .SetEase(Ease.OutQuad)
            .OnComplete(OneDone);
        
        bottomBar.DOAnchorPos(_bottomOriginal, duration)
            .SetEase(Ease.OutQuad)
            .OnComplete(OneDone);
    }

    public void PlayExitAnimation(float duration, System.Action onComplete)
    {
        float topHeight = topBar.rect.height;
        float bottomHeight = bottomBar.rect.height;
        
        // teleport offscreen (relative to final)
        var newTop = topBar.anchoredPosition + new Vector2(0,  topHeight);
        var newBottom = bottomBar.anchoredPosition + new Vector2(0, -bottomHeight);
        
        // kill if accidentally there were previous tweens
        DOTween.Kill(topBar);
        DOTween.Kill(bottomBar);

        var tweenCount = 2;
        void OneDone()
        {
            tweenCount--;
            if (tweenCount <= 0)
                onComplete?.Invoke();
        }

        topBar.DOAnchorPos(newTop, duration)
            .SetEase(Ease.OutQuad)
            .OnComplete(OneDone);
        
        bottomBar.DOAnchorPos(newBottom, duration)
            .SetEase(Ease.OutQuad)
            .OnComplete(OneDone);
    }
}
