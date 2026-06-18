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

    //대화 데이터 관리 인스펙터 연결 잊지 말것
    Dictionary<int, string[]> talkData;

    void Awake()
    {
        if(instance == null)
        {
            instance = this;
            DontDestroyOnLoad(gameObject);
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
    }
}
