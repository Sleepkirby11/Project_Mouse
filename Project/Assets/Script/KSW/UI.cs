using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
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

    // --- 게임 시스템 ---
    public void StartGame(string sceneName) => SceneManager.LoadScene(sceneName);

    public void ExitGame()
    {
        Debug.Log("게임 종료!");
        Application.Quit();
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

    private void UpdateInkBar(float amount)
    {
        if (inkSlider != null) inkSlider.value = amount;
    }
}