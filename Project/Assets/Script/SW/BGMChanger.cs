using UnityEngine;

public class BGMChanger : MonoBehaviour
{
    public AudioClip newBGM;

    void OnEnable()
    {
        if (AudioManager.instance != null && newBGM != null)
        {
            AudioManager.instance.ChangeBGM(newBGM);
        }
    }
}
