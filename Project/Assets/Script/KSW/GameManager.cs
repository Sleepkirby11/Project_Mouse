using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

public class GameManager : MonoBehaviour
{
    public static GameManager instance;

    public int sceneIndex;

    [Header("대화창")]
    public int id;
    public bool isNPC;

    [Header("사운드")]
    public float bgmVolume;
    public float sfxVolume;

    //대화 데이터 관리 인스펙터 연결 잊지 말것
    Dictionary<int, string[]> talkData;

    public bool isSetting;


    void Awake()
    {
        if(instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        talkData = new Dictionary<int, string[]>();
        GenerateTalkData();
    }

    void GenerateTalkData() // 대화 데이터 생성 함수
    {
        talkData.Add(1000, new string[] { "안녕!" });
    }

    public string GetTalk(int id, int talkIndex) // 대화 데이터 반환 함수
    {
        if(talkIndex == talkData[id].Length)
            return null;
        else
            return talkData[id][talkIndex];
    }
    
    public void LoadScene(string index)
    {
        SceneManager.LoadScene(index);
        CameraSetting.instance.colliders.Clear();
    }

    public void PauseOnOff()
    {
        if(Time.timeScale == 0f)
            Time.timeScale = 1f;
        else if(Time.timeScale == 1f)
            Time.timeScale = 0f;
        isSetting = !isSetting;
        if(Player.instance != null)
            Player.instance.CloseSetting();
    }

}
