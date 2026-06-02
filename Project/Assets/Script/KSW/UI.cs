using UnityEngine;
using UnityEngine.UI;          // UI(Slider) 제어용 추가
using UnityEngine.SceneManagement; // 씬 이동
using TMPro;

public class UI : MonoBehaviour
{
    // 어디서나 UI에 접근할 수 있도록 싱글톤 인스턴스 생성
    public static UI Instance { get; private set; }

    // 다음 발표 때 실행
    // 게임 시작 버튼 누를 때 호출 
    public void StartGame()
    {
        SceneManager.LoadScene("Main"); //시작 씬 이름 넣을 것
    }

    ////설정(구현 예정)

    // 종료 버튼 누를 때 호출
    public void ExitGame()
    {
        Debug.Log("게임 종료!"); // 에디터에서는 안 꺼지므로 로그로 확인
        Application.Quit();     // 실제 빌드된 게임에서는 프로그램 종료
    }


    //체력바
    [Header("체력바 설정")]
    [SerializeField] private Slider hpSlider;
    [SerializeField] private TMP_Text hpText;
    [SerializeField] private float maxHP = 100f;
    private float currentHP;

    void Awake()
    {
        // 싱글톤 초기화
        if (Instance == null) Instance = this;
        else Destroy(gameObject);
    }

    void Start()
    {
        currentHP = maxHP;
        if (hpSlider != null)
        {
            hpSlider.minValue = 0f;
            hpSlider.maxValue = maxHP;

            UpdateHPBar();
        }
    }

    //테스트용: 스페이스바 누르면 데미지 15, H키 누르면 회복 15
    //void Update()
    //{
    //    if (Input.GetKeyDown(KeyCode.Space))
    //    {
    //        TakeDamage(15f);
    //    }
    //    if (Input.GetKeyDown(KeyCode.H))
    //    {
    //        Heal(15f);
    //    }
    //}

    //플레이어의 현재 체력을 직접 받아서 UI만 바꾸는 역할로 변경
    public void TakeDamage(float currentHpFromPlayer)
    {
        currentHP = currentHpFromPlayer;
        currentHP = Mathf.Clamp(currentHP, 0f, maxHP);

        UpdateHPBar();
    }

    public void Heal(float amount)
    {
        currentHP += amount;
        currentHP = Mathf.Clamp(currentHP, 0f, maxHP);

        UpdateHPBar();
    }

    private void UpdateHPBar()
    {
        if (hpSlider != null)
        {
            hpSlider.value = currentHP;
        }
        if (hpText != null)
        {
            hpText.text = $"{(int)currentHP} / {(int)maxHP}";
        }
    }

    private void Die()
    {
        Debug.Log("사망");
    }
}