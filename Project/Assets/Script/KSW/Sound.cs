using UnityEngine;
using UnityEngine.UI; // 슬라이더와 이미지를 쓰기 위해 필요

public class SoundControl : MonoBehaviour
{
    public enum SoundType
    {
        BGM,
        SFX
    }

    public SoundType soundType;
    public Slider volumeSlider;
    public Image volumeIcon;

    public Sprite soundOnSprite;
    public Sprite soundMuteSprite;

    void Start()
    {
        volumeSlider.onValueChanged.AddListener(OnVolumeChanged);
        if (soundType == SoundType.BGM)
        {
            volumeSlider.value = GameManager.instance.bgmVolume;
        }
        else if (soundType == SoundType.SFX)
        {
            volumeSlider.value = GameManager.instance.sfxVolume;
        }
        if (GameManager.instance != null)
        {
            if (soundType == SoundType.BGM)
            {
                OnVolumeChanged(GameManager.instance.bgmVolume);
            }
            else if (soundType == SoundType.SFX)
            {
                OnVolumeChanged(GameManager.instance.sfxVolume);
            }
        }

    }

    void OnVolumeChanged(float value)
    {
        if (GameManager.instance != null && AudioManager.instance != null)
        {
            if (soundType == SoundType.BGM)
            {
                GameManager.instance.bgmVolume = volumeSlider.value;
            }
            else if (soundType == SoundType.SFX)
            {
                GameManager.instance.sfxVolume = volumeSlider.value;
            }
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