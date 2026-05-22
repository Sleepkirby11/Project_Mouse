using UnityEngine;
using UnityEngine.SceneManagement; // 씬 이동

public class UI : MonoBehaviour
{
    // 게임 시작 버튼 누를 때 호출 (씬 추가 예정)
    //public void StartGame()
    //{
    //    SceneManager.LoadScene(""); //시작 씬 이름 넣을 것
    //}

    //설정(구현 예정)

    // 종료 버튼 누를 때 호출
    public void ExitGame()
    {
        Debug.Log("게임 종료!"); // 에디터에서는 안 꺼지므로 로그로 확인
        Application.Quit();     // 실제 빌드된 게임에서는 프로그램 종료
    }

    //체력바

}
