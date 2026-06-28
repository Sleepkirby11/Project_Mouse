using UnityEngine;
using UnityEngine.SceneManagement;

public class GameStarting : MonoBehaviour
{
    public GameObject settingPanel;
    public GameObject licensePanel;
    public GameObject mainMenuPanel; 

    public void PlayGame()
    {
        if (settingPanel != null) settingPanel.SetActive(false);
        if (licensePanel != null) licensePanel.SetActive(false);

        if (mainMenuPanel != null) mainMenuPanel.SetActive(false);

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
    }

    public void CloseSetting()
    {
        if (settingPanel != null) settingPanel.SetActive(false);
    }

    // 라이선스창 열기/닫기
    public void OpenLicense()
    {
        if (licensePanel != null) licensePanel.SetActive(true);
    }

    public void CloseLicense()
    {
        if (licensePanel != null) licensePanel.SetActive(false);
    }
}