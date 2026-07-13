using UnityEngine;

public class BGMChanger : MonoBehaviour
{
    public AudioClip newBGM;

    void OnEnable()
    {
        if (AudioManager.instance != null && newBGM != null)
        {
            if (AudioManager.instance.bgmClip != newBGM)
            {
                AudioManager.instance.ChangeBGM(newBGM);
            }
            else
            {
                if (!AudioManager.instance.IsPlayingBGM())
                {
                    AudioManager.instance.PlayBGM(true);
                }
            }
        }
    }
}
