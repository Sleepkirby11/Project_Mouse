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
        BossHpZero
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
    }
    public void PlaySFX(SFX sfx)
    {
        PlaySFX_Int((int)sfx);
    }

    public void PlaySFX_Int(int sfx)
    {
        if (sfxClips == null || sfx < 0 || sfx >= sfxClips.Length)
        {
            Debug.LogWarning($"[AudioManager] SFX index {sfx} is out of bounds of sfxClips (Length: {sfxClips?.Length ?? 0}). Please assign all 21 clips in the AudioManager Inspector.");
            return;
        }

        for (int i = 0; i < sfxPlayers.Length; i++)
        {
            int loopIndex = (i + channelIndex) % sfxPlayers.Length;
            if (sfxPlayers[loopIndex].isPlaying)
                continue;

            channelIndex = loopIndex;
            sfxPlayers[loopIndex].clip = sfxClips[sfx].clip;
            float globalVol = GameManager.instance != null ? GameManager.instance.sfxVolume : sfxVolume;
            sfxPlayers[loopIndex].volume = globalVol * sfxClips[sfx].volumeScale;
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

    public void StopSFX()
    {
        for (int i = 0; i < sfxPlayers.Length; i++)
        {
            sfxPlayers[i].Stop();
        }
    }
}