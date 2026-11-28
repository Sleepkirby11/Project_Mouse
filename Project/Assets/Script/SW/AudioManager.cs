using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class AudioManager : MonoBehaviour
{
    public static AudioManager instance;

    [Header("#BGM")]
    public AudioClip bgmClip;
    public float bgmVolume;
    AudioSource bgmPlayer;

    [System.Serializable]
    public struct SFXData
    {
        public string name; // 식별용 이름
        public AudioClip clip;
        [Range(0f, 1f)] public float volumeScale;
    }

    [Header("#SFX")]
    public SFXData[] sfxClips;
    public float sfxVolume;
    public int channels;
    AudioSource[] sfxPlayers;
    int channelIndex;

    [Header("#UI")]
    public AudioClip uiClickClip;
    [Range(0f, 1f)] public float uiClickVolumeScale = 1f;
    AudioSource uiPlayer;

    private int lastPlayFrame;
    private int[] sfxPlayCountsInFrame;
    private Dictionary<AudioClip, float> clipVolumeScaleMap;
    private readonly List<AudioSource> cachedExternalSources = new List<AudioSource>();
    private float lastExternalScanTime = -10f;
    private const float ExternalScanCooldown = 0.5f;

    //효과음 추가 시 순서대로 입력할 것
    public enum SFX
    {
        ArcherArrow,
        BasicEnemyAttack,
        BlueBossClaw,
        BlueBossDash,
        BlueBossLaser,
        EnemyHurt,
        GreenBossWind,
        Ice,
        IceHammer,
        JumpEnemy_Jump,
        JumpEnemy_Land,
        ParryingCounter,
        ParryingShield_attackSword,
        PlayerDash,
        PlayerWalk,
        RedBossDie,
        RedBossFire,
        RedBossTP,
        RGB_explosion,
        RGB_Gear,
        RGB_Hurricane,
        Death,
        BossHpZero,
        PlayerHurt,
        FlyingEnemy_Attack,
        RGB_Lightning,
        RGB_Mushroom,
        RGB_PoisonArea,
        RGB_Bullet,
        PlayerJump,
        RGB_BlackHole,
        RedBoss_LastStand,
        BlueBossDie,
        PressurePlatePress,
        MantisStab,
        MantisSlam,
        MantisCut
    }

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }
        else if (instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Init();

        if (!SaveManager.IsBootstrappingSession)
        {
            PlayBGM(true);
        }
    }

    private void OnDestroy()
    {
        if (instance == this)
        {
            instance = null;
        }
    }



    void Init()
    {
        clipVolumeScaleMap = new Dictionary<AudioClip, float>();
        if (sfxClips != null)
        {
            for (int i = 0; i < sfxClips.Length; i++)
            {
                if (sfxClips[i].clip != null && !clipVolumeScaleMap.ContainsKey(sfxClips[i].clip))
                {
                    clipVolumeScaleMap.Add(sfxClips[i].clip, sfxClips[i].volumeScale);
                }
            }
        }

        //배경음 플레이어 초기화
        GameObject bgmObject = new GameObject("BgmPlayer");
        bgmObject.transform.parent = transform;
        bgmPlayer = bgmObject.AddComponent<AudioSource>();
        bgmPlayer.playOnAwake = false;
        bgmPlayer.loop = true;
        bgmPlayer.volume = GetGlobalBgmVolume();
        bgmPlayer.clip = bgmClip;

        //효과음 플레이어 초기화
        GameObject sfxObject = new GameObject("SfxPlayer");
        sfxObject.transform.parent = transform;
        sfxPlayers = new AudioSource[channels];

        float initSfxVol = GetGlobalSfxVolume();

        for (int i = 0; i < sfxPlayers.Length; i++)
        {
            sfxPlayers[i] = sfxObject.AddComponent<AudioSource>();
            sfxPlayers[i].playOnAwake = false;
            sfxPlayers[i].volume = initSfxVol;
        }

        // UI 버튼 전용 플레이어 초기화 (일시정지 PauseSFX의 영향을 받지 않고 SFX 볼륨만 따름)
        CreateUiPlayer(initSfxVol);
    }

    private void CreateUiPlayer(float initialVolume)
    {
        if (uiPlayer != null) return;
        GameObject uiObject = new GameObject("UiPlayer");
        uiObject.transform.parent = transform;
        uiPlayer = uiObject.AddComponent<AudioSource>();
        uiPlayer.playOnAwake = false;
        uiPlayer.ignoreListenerPause = true;
        uiPlayer.volume = initialVolume;
    }

    private float GetGlobalBgmVolume()
    {
        return GameManager.instance != null
            ? Mathf.Clamp01(GameManager.instance.bgmVolume)
            : Mathf.Clamp01(bgmVolume);
    }

    private float GetGlobalSfxVolume()
    {
        return GameManager.instance != null
            ? Mathf.Clamp01(GameManager.instance.sfxVolume)
            : Mathf.Clamp01(sfxVolume);
    }

    private float GetClipVolumeScale(AudioClip clip)
    {
        if (clip != null && clipVolumeScaleMap != null && clipVolumeScaleMap.TryGetValue(clip, out float scale))
        {
            return scale;
        }
        return 1f;
    }

    private void RefreshExternalAudioSources(bool force)
    {
        if (!force && (Time.unscaledTime - lastExternalScanTime) < ExternalScanCooldown)
        {
            return;
        }

        lastExternalScanTime = Time.unscaledTime;
        cachedExternalSources.Clear();

        AudioSource[] allAudioSources = FindObjectsByType<AudioSource>(FindObjectsSortMode.None);
        for (int i = 0; i < allAudioSources.Length; i++)
        {
            AudioSource source = allAudioSources[i];
            if (source == null || source == bgmPlayer || source == uiPlayer) continue;
            if (sfxPlayers != null && System.Array.IndexOf(sfxPlayers, source) >= 0) continue;
            cachedExternalSources.Add(source);
        }
    }

    public void UpdateSound()
    {
        if (bgmPlayer != null)
        {
            bgmPlayer.volume = GetGlobalBgmVolume();
        }

        float globalVol = GetGlobalSfxVolume();

        if (uiPlayer != null)
        {
            uiPlayer.volume = globalVol;
        }

        if (sfxPlayers != null)
        {
            for (int i = 0; i < sfxPlayers.Length; i++)
            {
                if (sfxPlayers[i] == null) continue;
                float scale = sfxPlayers[i].isPlaying ? GetClipVolumeScale(sfxPlayers[i].clip) : 1f;
                sfxPlayers[i].volume = globalVol * scale;
            }
        }

        // 슬라이더 드래그 중 매 프레임 씬 전체를 재탐색하지 않도록 쿨다운 캐싱 사용
        RefreshExternalAudioSources(force: false);
        for (int i = cachedExternalSources.Count - 1; i >= 0; i--)
        {
            AudioSource source = cachedExternalSources[i];
            if (source == null)
            {
                cachedExternalSources.RemoveAt(i);
                continue;
            }
            if (source.clip != null)
            {
                source.volume = globalVol * GetClipVolumeScale(source.clip);
            }
        }
    }
    public void PlaySFX(SFX sfx)
    {
        PlaySFX_Int((int)sfx);
    }

    public void PlayUIClick()
    {
        if (uiClickClip == null)
        {
            return;
        }

        float globalVol = GetGlobalSfxVolume();
        if (globalVol <= 0.0001f)
        {
            return;
        }

        if (uiPlayer == null)
        {
            CreateUiPlayer(globalVol);
        }

        uiPlayer.volume = globalVol;
        uiPlayer.PlayOneShot(uiClickClip, Mathf.Clamp01(uiClickVolumeScale));
    }

    private bool CheckFrameLimit(int sfx)
    {
        if (sfxClips == null) return false;
        
        if (sfxPlayCountsInFrame == null || sfxPlayCountsInFrame.Length != sfxClips.Length)
        {
            sfxPlayCountsInFrame = new int[sfxClips.Length];
        }

        int currentFrame = Time.frameCount;
        if (currentFrame != lastPlayFrame)
        {
            lastPlayFrame = currentFrame;
            System.Array.Clear(sfxPlayCountsInFrame, 0, sfxPlayCountsInFrame.Length);
        }

        if (sfxPlayCountsInFrame[sfx] >= 2)
        {
            return false; // 한 프레임에 동일 효과음 최대 2개 제한 초과
        }

        sfxPlayCountsInFrame[sfx]++;
        return true;
    }

    public void PlaySFX_Int(int sfx)
    {
        if (sfxClips == null || sfx < 0 || sfx >= sfxClips.Length)
        {
            Debug.LogWarning($"[AudioManager] SFX index {sfx} is out of bounds of sfxClips (Length: {sfxClips?.Length ?? 0}). Please assign all {System.Enum.GetValues(typeof(SFX)).Length} clips in the AudioManager Inspector.");
            return;
        }

        if (!CheckFrameLimit(sfx))
        {
            return;
        }

        for (int i = 0; i < sfxPlayers.Length; i++)
        {
            int loopIndex = (i + channelIndex) % sfxPlayers.Length;
            if (sfxPlayers[loopIndex].isPlaying)
                continue;

            channelIndex = loopIndex;
            sfxPlayers[loopIndex].clip = sfxClips[sfx].clip;
            sfxPlayers[loopIndex].pitch = 1f; // 피치 리셋
            float globalVol = GetGlobalSfxVolume();
            sfxPlayers[loopIndex].volume = globalVol * sfxClips[sfx].volumeScale;
            sfxPlayers[loopIndex].Play();
            break;
        }
    }

    public void PlaySFXPitched(SFX sfx, float minPitch, float maxPitch)
    {
        int sfxIndex = (int)sfx;
        if (sfxClips == null || sfxIndex < 0 || sfxIndex >= sfxClips.Length)
        {
            return;
        }

        if (!CheckFrameLimit(sfxIndex))
        {
            return;
        }

        for (int i = 0; i < sfxPlayers.Length; i++)
        {
            int loopIndex = (i + channelIndex) % sfxPlayers.Length;
            if (sfxPlayers[loopIndex].isPlaying)
                continue;

            channelIndex = loopIndex;
            sfxPlayers[loopIndex].clip = sfxClips[sfxIndex].clip;
            sfxPlayers[loopIndex].pitch = UnityEngine.Random.Range(minPitch, maxPitch);
            float globalVol = GetGlobalSfxVolume();
            sfxPlayers[loopIndex].volume = globalVol * sfxClips[sfxIndex].volumeScale;
            sfxPlayers[loopIndex].Play();
            break;
        }
    }

    public void ChangeBGM(AudioClip newBGM)
    {
        bgmClip = newBGM;
        bgmPlayer.Stop();
        bgmPlayer.clip = newBGM;
        PlayBGM(true);
    }

    public void PlayBGM(bool isPlay)
    {
        if (isPlay)
        {
            bgmPlayer.Play();
            bgmPlayer.loop = true;
        }
        else
        {
            bgmPlayer.Stop();
        }
    }

    public bool IsPlayingBGM()
    {
        return bgmPlayer != null && bgmPlayer.isPlaying;
    }

    public void PauseBGM()
    {
        if (bgmPlayer != null && bgmPlayer.isPlaying)
        {
            bgmPlayer.Pause();
        }
    }

    public void ResumeBGM()
    {
        if (bgmPlayer != null)
        {
            bgmPlayer.UnPause();
        }
    }

    public void StopSFX()
    {
        for (int i = 0; i < sfxPlayers.Length; i++)
        {
            sfxPlayers[i].Stop();
        }
    }

    public void PauseSFX()
    {
        if (sfxPlayers != null)
        {
            for (int i = 0; i < sfxPlayers.Length; i++)
            {
                if (sfxPlayers[i] != null && sfxPlayers[i].isPlaying)
                    sfxPlayers[i].Pause();
            }
        }

        RefreshExternalAudioSources(force: true);
        for (int i = cachedExternalSources.Count - 1; i >= 0; i--)
        {
            AudioSource source = cachedExternalSources[i];
            if (source == null)
            {
                cachedExternalSources.RemoveAt(i);
                continue;
            }
            if (source.isPlaying)
            {
                source.Pause();
            }
        }
    }

    public void ResumeSFX()
    {
        if (sfxPlayers != null)
        {
            for (int i = 0; i < sfxPlayers.Length; i++)
            {
                if (sfxPlayers[i] != null)
                    sfxPlayers[i].UnPause();
            }
        }

        for (int i = cachedExternalSources.Count - 1; i >= 0; i--)
        {
            AudioSource source = cachedExternalSources[i];
            if (source == null)
            {
                cachedExternalSources.RemoveAt(i);
                continue;
            }
            source.UnPause();
        }
    }
}