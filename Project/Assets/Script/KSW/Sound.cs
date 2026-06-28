using UnityEngine;
using UnityEngine.UI; // 슬라이더와 이미지를 쓰기 위해 필요

public class SoundControl : MonoBehaviour
{
    public Slider volumeSlider;    
    public Image volumeIcon;       

    public Sprite soundOnSprite;   
    public Sprite soundMuteSprite; 

    void Start()
    {
        volumeSlider.onValueChanged.AddListener(OnVolumeChanged);

        OnVolumeChanged(volumeSlider.value);
    }

    void OnVolumeChanged(float value)
    {
        if (value <= 0)
        {
            volumeIcon.sprite = soundMuteSprite;
        }
        else
        {
            volumeIcon.sprite = soundOnSprite;
        }
    }
}