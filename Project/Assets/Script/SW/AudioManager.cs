using UnityEngine;

public class AudioManager : MonoBehaviour
{
    public static AudioManager instance;

    [Header("#BGM")]
    public AudioClip bgmClip;
    public float bgmVolume;
    AudioSource bgmPlayer;

    [Header("#SFX")]
    public AudioClip[] sfxClips;
    public float sfxVolume;
    public int channels;
    AudioSource[] sfxPlayers;
    int channelIndex;

    //효과음 추가 시 순서대로 입력할 것
    public enum SFX
    {
        
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
            sfxPlayers[i].volume = sfxVolume;
        }
    }
    public void UpdateSound()
    {
        bgmPlayer.volume = bgmVolume;
        for (int i = 0; i < sfxPlayers.Length; i++)
        {
            sfxPlayers[i].volume = sfxVolume;
        }
    }
    public void PlaySFX(SFX sfx)
    {
        PlaySFX_Int((int)sfx);
    }

    public void PlaySFX_Int(int sfx)
    {
        for (int i = 0; i < sfxPlayers.Length; i++)
        {
            int loopIndex = (i + channelIndex) % sfxPlayers.Length;
            if (sfxPlayers[loopIndex].isPlaying)
                continue;

            channelIndex = loopIndex;
            sfxPlayers[loopIndex].clip = sfxClips[sfx];
            sfxPlayers[loopIndex].Play();
            break;
        }
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