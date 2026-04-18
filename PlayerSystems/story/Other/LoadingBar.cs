using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

public class LoadingBar : MonoBehaviour
{
    [SerializeField] private Slider slider;
    
    [System.Serializable]
    public class FloatEvent : UnityEvent<float> { }

    public FloatEvent OnProgressUpdate = new();
    public UnityEvent OnProgressFinished = new();

    private bool _finished;

    public void SetProgress(float value)
    {
        value = Mathf.Clamp01(value);

        if (slider)
            slider.value = value;

        OnProgressUpdate?.Invoke(value);

        if (_finished || !(value >= 1f)) return;
        
        _finished = true;
        OnProgressFinished?.Invoke();
    }
}