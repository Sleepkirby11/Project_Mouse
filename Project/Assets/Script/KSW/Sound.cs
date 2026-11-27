using UnityEngine;
using UnityEngine.UI;

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

    private float lastNonZeroVolume = 0.7f;
    private bool listenerBound;

    void Awake()
    {
        EnsureParentSettingPanelStyled();
        BindControls();
    }

    void OnEnable()
    {
        EnsureParentSettingPanelStyled();
        BindControls();
        SyncFromGameManager();
    }

    void Start()
    {
        EnsureParentSettingPanelStyled();
        BindControls();
        SyncFromGameManager();
    }

    private void EnsureParentSettingPanelStyled()
    {
        Transform cur = transform;
        while (cur != null)
        {
            if (cur.name == "settingPanel")
            {
                SettingsUIStyler.EnsureStyled(cur.gameObject);
                break;
            }
            cur = cur.parent;
        }
    }

    private void BindControls()
    {
        if (listenerBound) return;

        if (volumeSlider != null)
        {
            volumeSlider.onValueChanged.RemoveListener(OnVolumeChanged);
            volumeSlider.onValueChanged.AddListener(OnVolumeChanged);
        }

        if (volumeIcon != null)
        {
            volumeIcon.color = Color.white;
            Button iconBtn = volumeIcon.GetComponent<Button>();
            if (iconBtn != null)
            {
                iconBtn.onClick.RemoveListener(ToggleMute);
                iconBtn.onClick.AddListener(ToggleMute);
            }
        }

        listenerBound = true;
    }

    private void SyncFromGameManager()
    {
        if (volumeSlider == null) return;

        if (GameManager.instance != null)
        {
            float targetVol = (soundType == SoundType.BGM)
                ? GameManager.instance.bgmVolume
                : GameManager.instance.sfxVolume;

            if (targetVol > 0.01f)
            {
                lastNonZeroVolume = targetVol;
            }

            volumeSlider.SetValueWithoutNotify(targetVol);
            OnVolumeChanged(targetVol);
        }
        else
        {
            RefreshVisuals();
        }
    }

    public void ToggleMute()
    {
        if (volumeSlider == null) return;

        if (volumeSlider.value > 0.005f)
        {
            lastNonZeroVolume = volumeSlider.value;
            volumeSlider.value = 0f;
        }
        else
        {
            volumeSlider.value = Mathf.Clamp01(lastNonZeroVolume > 0.05f ? lastNonZeroVolume : 0.7f);
        }
    }

    void OnVolumeChanged(float value)
    {
        if (value > 0.01f)
        {
            lastNonZeroVolume = value;
        }

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

        RefreshVisuals();
    }

    public void RefreshVisuals()
    {
        float value = volumeSlider != null ? volumeSlider.value : 0f;
        bool isMuted = value <= 0.001f;

        if (volumeIcon != null)
        {
            volumeIcon.color = Color.white;
            if (isMuted && soundMuteSprite != null)
            {
                volumeIcon.sprite = soundMuteSprite;
            }
            else if (!isMuted && soundOnSprite != null)
            {
                volumeIcon.sprite = soundOnSprite;
            }
        }

        if (volumeSlider != null && volumeSlider.fillRect != null)
        {
            Image fillImg = volumeSlider.fillRect.GetComponent<Image>();
            if (fillImg != null)
            {
                fillImg.color = new Color(0.85f, 0.54f, 0.24f, 1f);
            }
        }

        Transform pctTf = transform.Find("VolumeValueText");
        if (pctTf == null && transform.parent != null)
        {
            pctTf = transform.parent.Find("VolumeValueText");
        }
        if (pctTf != null)
        {
            Text pctText = pctTf.GetComponent<Text>();
            if (pctText != null)
            {
                int pct = Mathf.RoundToInt(Mathf.Clamp01(value) * 100f);
                pctText.text = $"{pct}%";
                pctText.color = new Color(0.38f, 0.23f, 0.18f, 1f);
            }
        }
    }
}
