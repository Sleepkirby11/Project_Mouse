using UnityEngine;
using UnityEngine.SceneManagement;

public class GameStarting : MonoBehaviour
{
    public GameObject settingPanel;
    public GameObject licensePanel; 

    // 게임 시작 버튼 누를 때 호출
    public void PlayGame()
    {
        SceneManager.LoadScene("Tutorial");
    }

    // 종료 버튼 누를 때 호출
    public void QuitGame()
    {
        Debug.Log("게임 종료!");
        Application.Quit();
    }

    // 설정창 열기/닫기
    public void OpenSetting()
    {
        if (settingPanel != null) settingPanel.SetActive(true);
        if (GameManager.instance != null) GameManager.instance.PauseOnOff();
    }

    public void CloseSetting()
    {
        if (settingPanel != null) settingPanel.SetActive(false);
        if (GameManager.instance != null) GameManager.instance.PauseOnOff();
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