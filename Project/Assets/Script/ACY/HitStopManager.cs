using System.Collections;
using UnityEngine;

public class HitStopManager : MonoBehaviour
{
    private static HitStopManager _instance;
    public static HitStopManager Instance
    {
        get
        {
            if (_instance == null)
                Debug.LogError("[HitStopManager] 씬에 존재하지 않습니다.");
            return _instance;
        }
    }

    private void Awake()
    {
        if (_instance == null) _instance = this;
        else Destroy(gameObject);
    }

    public void DoHitStop(float duration, float timeScale = 0.05f)
    {
        StopAllCoroutines();
        StartCoroutine(HitStopRoutine(duration, timeScale));
    }

    private IEnumerator HitStopRoutine(float duration, float timeScale)
    {
        Time.timeScale = timeScale;
        yield return new WaitForSecondsRealtime(duration);
        Time.timeScale = 1f;
    }
}