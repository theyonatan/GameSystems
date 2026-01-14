using System.Globalization;
using UnityEngine;
using TMPro;
using UnityEngine.UI;

public class UpdateTextBasedOnSlider : MonoBehaviour
{
    TextMeshProUGUI _text;
    [SerializeField] Slider slider;
    
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        _text = GetComponent<TextMeshProUGUI>();

        if (slider == null)
        {
            Debug.LogError("slider is null!!!!");
            return;
        }
        
        UpdateText(slider.value);
        slider.onValueChanged.AddListener(UpdateText);
    }

    public void UpdateText(float value)
    {
        _text.text = value.ToString(CultureInfo.CurrentCulture);
    }
}
