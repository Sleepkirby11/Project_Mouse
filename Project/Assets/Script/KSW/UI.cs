using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System.Collections;
using TMPro;

public class UI : MonoBehaviour
{
    // 어디서나 UI에 접근할 수 있도록 싱글톤 인스턴스 생성
    public static UI Instance { get; private set; }

    [Header("체력바 설정")]
    [SerializeField] private Slider hpSlider;
    [SerializeField] private TMP_Text hpText;
    [SerializeField] private float maxHP = 100f;
    private float currentHP;

    [Header("잉크 게이지 설정")]
    [SerializeField] private Slider inkSlider;
    [SerializeField] private float maxInk;
    private float currentInk;

    void Awake()
    {
        // 싱글톤 초기화
        if (Instance == null) Instance = this;
    }

    void Start()
    {
        // 초기화
        currentHP = maxHP;
        currentInk = maxInk;

        if (hpSlider != null) { hpSlider.maxValue = maxHP; hpSlider.value = maxHP; }
        if (inkSlider != null) { inkSlider.maxValue = maxInk; inkSlider.value = maxInk; }

        UpdateHPBar();
    }

    // 게임 시작 버튼 누를 때 호출
    public void StartGame()
    {
        SceneManager.LoadScene(1);
    }

    //설정 메뉴 추가 예정

    // 종료 버튼 누를 때 호출
    public void ExitGame()
    {
        Debug.Log("게임 종료!"); // 에디터에서는 안 꺼지므로 로그로 확인
        Application.Quit();     // 실제 빌드된 게임에서는 프로그램 종료
    }
   
    // --- 체력 제어 (플레이어 스크립트에서 호출) ---
    public void TakeDamage(float amount)
    {
        currentHP = Mathf.Clamp(currentHP - amount, 0f, maxHP);
        UpdateHPBar();
    }

    public void Heal(float amount)
    {
        currentHP = Mathf.Clamp(currentHP + amount, 0f, maxHP);
        UpdateHPBar();
    }

    private void UpdateHPBar()
    {
        if (hpSlider != null) hpSlider.value = currentHP;
        if (hpText != null) hpText.text = $"{(int)currentHP} / {(int)maxHP}";
    }

    // --- 잉크 제어 (공격 스크립트에서 호출) ---
    public void UseInk(float amount)
    {
        currentInk = Mathf.Clamp(amount, 0f, maxInk);
        UpdateInkBar(amount);
    }

    public void ChargeInk(float amount)
    {
        currentInk = Mathf.Clamp(amount, 0f, maxInk);
        UpdateInkBar(amount);
    }

    public void ChangeStance(int amount)
    {
        PlayerStatus player = GameObject.FindWithTag("Player").GetComponent<PlayerStatus>();
        switch (amount)
        {
            case 0:
                player.currentStance = PlayerStatus.Stance.White;
                break;
            case 1:
                player.currentStance = PlayerStatus.Stance.Red;
                break;
            case 2:
                player.currentStance = PlayerStatus.Stance.Blue;
                break;
            case 3:
                player.currentStance = PlayerStatus.Stance.Green;
                break;
        }
    }

    private void UpdateInkBar(float amount)
    {
        if (inkSlider != null) inkSlider.value = amount;
    }

    [Header("대화창")]
    public GameManager talkManager;
    public GameObject talkPanel;
    public Text talkText;
    public GameObject scanObject;
    public bool isAction;
    public int talkIndex;

    public void Action(GameObject scanObj)
    {
        isAction = true;
        scanObject = scanObj;
        GameManager objData = scanObject.GetComponent<GameManager>();
        Talk(objData.id, objData.isNPC);
        
        talkPanel.SetActive(isAction);
    }

    void Talk(int id, bool isNPC)
    {
        string talkData = talkManager.GetTalk(id, talkIndex);

        if(talkData == null)
        {
            isAction = false;
            return;
        }

        if (isNPC)
        {
            talkText.text = talkData;
        }
        else
        {
            talkText.text = talkData;
        }

        isAction = true;
        talkIndex++;
    }
}