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
        volumeSlider.value = GameManager.instance.volume;
        if(GameManager.instance != null)
            OnVolumeChanged(GameManager.instance.volume);
    }

    void OnVolumeChanged(float value)
    {
        if(GameManager.instance != null && AudioManager.instance != null)
        {
            GameManager.instance.volume = volumeSlider.value;
            AudioManager.instance.UpdateSound();
        }
        
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