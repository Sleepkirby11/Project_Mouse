using UnityEngine;
using UnityEngine.SceneManagement;

public class GameClear : MonoBehaviour
{
    public string titleSceneName = "KSW_Scene";

    public void GoToTitle()
    {
        if (GameManager.instance != null)
        {
            GameManager.instance.LoadScene(titleSceneName);
        }
        else
        {
            SceneManager.LoadSceneAsync(titleSceneName);
        }
    }

    public void QuitGame()
    {
        Debug.Log("게임 종료!");
        Application.Quit();
    }
}