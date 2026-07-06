using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using System.Collections;
using TMPro;
using Unity.VisualScripting;

public class UI : MonoBehaviour
{
    // 어디서나 UI에 접근할 수 있도록 싱글톤 인스턴스 생성
    public static UI Instance { get; private set; }

    //플레이어 status에 미리 접근
    PlayerStatus playerStatus;

    [Header("체력바 설정")]
    [SerializeField] private Slider hpSlider;

    [Header("잉크 게이지 설정")]
    [SerializeField] private Slider inkSlider;

    [Header("쿨타임 게이지 설정")]
    [SerializeField] private Slider coolTimeSlider;

    [Header("특수 잉크 게이지 설정")]
    [SerializeField] private Slider specialInkSlider;

    [Header("보스 체력바 설정")]
    [SerializeField] private Slider bossHpSlider;

    [Header("대화창")]
    public GameObject talkPanel;
    public Text talkText;
    public GameObject scanObject;
    public bool isAction;
    public int talkIndex;

    [Header("팔레트")]
    public GameObject pal;

    [Header("설정 패널")]
    public GameObject settingPanel;

    [Header("라이센스 패널")]
    public GameObject licensePanel; 

    void Awake()
    {
        // 싱글톤 초기화
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        GameObject playerObj = GameObject.FindWithTag("Player");
        if (playerObj != null)
        {
            playerStatus = playerObj.GetComponent<PlayerStatus>();
            UpdateHPBar();
            UpdateCoolTimeBar();
            UpdateInkBar();
        }
    }

    // 게임 시작 버튼 누를 때 호출
    public void StartGame()
    {
        SceneManager.LoadScene("Tutorial");
    }

    //설정 메뉴 추가 예정

    // 종료 버튼 누를 때 호출
    public void ExitGame()
    {
        Debug.Log("게임 종료!"); // 에디터에서는 안 꺼지므로 로그로 확인
        Application.Quit();     // 실제 빌드된 게임에서는 프로그램 종료
    }

    //플레이어 체력 상태 업데이트
    public void UpdateHPBar()
    {
        if (hpSlider != null && playerStatus != null) hpSlider.value = playerStatus.HP / playerStatus.MaxHP;
    }

    // 보스 체력바 활성화 및 초기 체력 설정
    public void ShowBossHPBar(float initialRatio)
    {
        Debug.Log($"[UI] ShowBossHPBar 호출됨. 초기 비율: {initialRatio}");
        if (bossHpSlider != null)
        {
            bossHpSlider.gameObject.SetActive(true);
            bossHpSlider.value = initialRatio;
            Debug.Log($"[UI] bossHpSlider가 이제 활성화되었습니다. 값: {bossHpSlider.value}");
        }
        else
        {
            Debug.LogError("[UI] bossHpSlider가 null입니다! 인스펙터에서 UIManager (UI 스크립트)의 Boss HP Slider 필드에 EnemyHP 슬라이더를 할당해 주세요.");
        }
    }

    // 보스 체력바 상태 업데이트
    public void UpdateBossHPBar(float currentHP, float maxHP)
    {
        Debug.Log($"[UI] UpdateBossHPBar 호출됨: {currentHP}/{maxHP}");
        if (bossHpSlider != null && maxHP > 0)
        {
            bossHpSlider.value = currentHP / maxHP;
        }
        else if (bossHpSlider == null)
        {
            Debug.LogWarning("[UI] UpdateBossHPBar: bossHpSlider가 null입니다.");
        }
    }

    public void UpdateCoolTimeBar()
    {
        if (coolTimeSlider != null && playerStatus != null)
        {
            coolTimeSlider.value = playerStatus.currentCoolTime / playerStatus.coolTime;
        }
    }

    // 보스 체력바 비활성화
    public void HideBossHPBar()
    {
        Debug.Log("[UI] HideBossHPBar 호출됨.");
        if (bossHpSlider != null)
        {
            bossHpSlider.gameObject.SetActive(false);
            Debug.Log("[UI] bossHpSlider가 비활성화되었습니다.");
        }
        else
        {
            Debug.LogWarning("[UI] HideBossHPBar: bossHpSlider가 null입니다.");
        }
    }

    public void ActivePal(bool isActive)
    {
        pal.SetActive(isActive);
    }

    public void ChangeStance(int amount)
    {
        if (playerStatus == null) return;
        Debug.Log("체인지");
        switch (amount)
        {
            case 0:
                playerStatus.currentStance = PlayerStatus.Stance.White;
                break;
            case 1:
                playerStatus.currentStance = PlayerStatus.Stance.Red;
                break;
            case 2:
                playerStatus.currentStance = PlayerStatus.Stance.Blue;
                break;
            case 3:
                playerStatus.currentStance = PlayerStatus.Stance.Green;
                break;
        }
    }

    //플레이어 ink 상태 업데이트
    public void UpdateInkBar()
    {
        if (inkSlider != null && playerStatus != null) inkSlider.value = playerStatus.ink / playerStatus.maxInk;
    }

    //플레이어 specialInk 상태 업데이트
    public void UpdateSpedialInkBar()
    {
        if (specialInkSlider != null && playerStatus != null) specialInkSlider.value = playerStatus.specialInk / playerStatus.maxSpecialInk;
    }

    //대화에 필요한 Action
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
        string talkData = GameManager.instance.GetTalk(id, talkIndex);

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
    
    // 설정창 열기/닫기
    public void OpenSetting()
    {
        if (settingPanel != null) settingPanel.SetActive(true);
    }

    public void CloseSetting()
    {
        if (settingPanel != null) settingPanel.SetActive(false);
    }

    // [추가] 라이선스창 열기
    public void OpenLicense()
    {
        if (licensePanel != null) licensePanel.SetActive(true);
    }

    // [추가] 라이선스창 닫기
    public void CloseLicense()
    {
        if (licensePanel != null) licensePanel.SetActive(false);
    }
}