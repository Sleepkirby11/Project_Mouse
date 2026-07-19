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
        PressurePlatePress
    }

    private void Awake()
    {
        if (instance == null)
        {
            instance = this;
        }

        Init();

        PlayBGM(true);
    }



    void Init()
    {
        //배경음 플레이어 초기화
        GameObject bgmObject = new GameObject("BgmPlayer");
        bgmObject.transform.parent = transform;
        bgmPlayer = bgmObject.AddComponent<AudioSource>();
        bgmPlayer.playOnAwake = false;
        bgmPlayer.loop = true;
        if (GameManager.instance != null)
            bgmPlayer.volume = GameManager.instance.bgmVolume;
        else
            bgmPlayer.volume = bgmVolume;
        bgmPlayer.clip = bgmClip;

        //효과음 플레이어 초기화
        GameObject sfxObject = new GameObject("SfxPlayer");
        sfxObject.transform.parent = transform;
        sfxPlayers = new AudioSource[channels];

        for (int i = 0; i < sfxPlayers.Length; i++)
        {
            sfxPlayers[i] = sfxObject.AddComponent<AudioSource>();
            sfxPlayers[i].playOnAwake = false;
            if (GameManager.instance != null)
                sfxPlayers[i].volume = GameManager.instance.sfxVolume;
            else
                sfxPlayers[i].volume = sfxVolume;
        }
    }
    public void UpdateSound()
    {
        if (GameManager.instance != null)
            bgmPlayer.volume = GameManager.instance.bgmVolume;
        else
            bgmPlayer.volume = bgmVolume;

        float globalVol = GameManager.instance != null ? GameManager.instance.sfxVolume : sfxVolume;
        for (int i = 0; i < sfxPlayers.Length; i++)
        {
            float scale = 1f;
            if (sfxPlayers[i].isPlaying && sfxPlayers[i].clip != null)
            {
                foreach (var sfxData in sfxClips)
                {
                    if (sfxData.clip == sfxPlayers[i].clip)
                    {
                        scale = sfxData.volumeScale;
                        break;
                    }
                }
            }
            sfxPlayers[i].volume = globalVol * scale;
        }

        // 씬 내의 다른 모든 AudioSource 볼륨 업데이트
        AudioSource[] allAudioSources = FindObjectsByType<AudioSource>(FindObjectsSortMode.None);
        foreach (var source in allAudioSources)
        {
            if (source != bgmPlayer && System.Array.IndexOf(sfxPlayers, source) < 0)
            {
                if (source.clip != null)
                {
                    float scale = 1f;
                    foreach (var sfxData in sfxClips)
                    {
                        if (sfxData.clip == source.clip)
                        {
                            scale = sfxData.volumeScale;
                            break;
                        }
                    }
                    source.volume = globalVol * scale;
                }
            }
        }
    }
    public void PlaySFX(SFX sfx)
    {
        PlaySFX_Int((int)sfx);
    }

    public void PlaySFX_Int(int sfx)
    {
        if (sfxClips == null || sfx < 0 || sfx >= sfxClips.Length)
        {
            Debug.LogWarning($"[AudioManager] SFX index {sfx} is out of bounds of sfxClips (Length: {sfxClips?.Length ?? 0}). Please assign all {System.Enum.GetValues(typeof(SFX)).Length} clips in the AudioManager Inspector.");
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
            float globalVol = GameManager.instance != null ? GameManager.instance.sfxVolume : sfxVolume;
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

        for (int i = 0; i < sfxPlayers.Length; i++)
        {
            int loopIndex = (i + channelIndex) % sfxPlayers.Length;
            if (sfxPlayers[loopIndex].isPlaying)
                continue;

            channelIndex = loopIndex;
            sfxPlayers[loopIndex].clip = sfxClips[sfxIndex].clip;
            sfxPlayers[loopIndex].pitch = UnityEngine.Random.Range(minPitch, maxPitch);
            float globalVol = GameManager.instance != null ? GameManager.instance.sfxVolume : sfxVolume;
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
        for (int i = 0; i < sfxPlayers.Length; i++)
        {
            if (sfxPlayers[i].isPlaying)
                sfxPlayers[i].Pause();
        }

        AudioSource[] allAudioSources = FindObjectsByType<AudioSource>(FindObjectsSortMode.None);
        foreach (var source in allAudioSources)
        {
            if (source != bgmPlayer && source.isPlaying)
            {
                source.Pause();
            }
        }
    }

    public void ResumeSFX()
    {
        for (int i = 0; i < sfxPlayers.Length; i++)
        {
            sfxPlayers[i].UnPause();
        }

        AudioSource[] allAudioSources = FindObjectsByType<AudioSource>(FindObjectsSortMode.None);
        foreach (var source in allAudioSources)
        {
            if (source != bgmPlayer)
            {
                source.UnPause();
            }
        }
    }
}