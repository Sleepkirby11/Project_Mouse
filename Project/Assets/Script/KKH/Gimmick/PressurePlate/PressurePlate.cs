using UnityEngine;
using UnityEngine.Events;
using System.Collections;

public class PressurePlate : MonoBehaviour
{
    [Header("연동할 장치 이벤트")]
    [SerializeField] private UnityEvent onPlatePressed;  // 발판 눌렸을 때
    [SerializeField] private UnityEvent onPlateReleased; // 발판에서 발 뗐을 때
    [SerializeField] private  float pressedDelay = 0.2f; // 발판이 눌리는 딜레이

    private Collider2D myCollider;
    private bool isPressed = false;

    private void Start()
    {
        myCollider = GetComponent<Collider2D>();

        // 시작할 때는 트리거를 꺼서 밟고 올라설 수 있게 합니다.
        if (myCollider != null)
        {
            myCollider.isTrigger = false;
        }
    }

    // 트리거가 꺼져있을 때 (단단한 바닥 상태일 때) 플레이어가 밟으면 실행됨
    private void OnCollisionEnter2D(Collision2D collision)
    {
        foreach (ContactPoint2D contact in collision.contacts)
        {
            if (collision.gameObject.CompareTag("Player") && !isPressed)
            {
                // 위에서 아래로 밟았는지 체크 (옆면 충돌 방지)
                // 플레이어의 아래쪽 방향과 충돌 표면의 법선(Normal)을 비교
                if (contact.normal.y < -0.7f)
                {
                    isPressed = true;
                    StartCoroutine(PlateActivate());
                }
            }
        }

    }

    // 트리거가 켜졌을 때 (눌려 들어간 상태일 때) 플레이어가 나갔는지 감시
    private void OnTriggerExit2D(Collider2D other)
    {
        if (other.CompareTag("Player") && isPressed)
        {
            PlateDeactivate();
        }
    }

    private IEnumerator PlateActivate()
    {
        yield return new WaitForSeconds(pressedDelay);
        myCollider.isTrigger = true; // 트리거 활성화 (푹 가라앉는 느낌 유도)

        Debug.Log("발판 작동!");
        onPlatePressed?.Invoke(); // 연결된 기믹 작동
    }

    private void PlateDeactivate()
    {
        isPressed = false;
        myCollider.isTrigger = false; // 다시 단단한 바닥으로 복구

        Debug.Log("발판 해제!");
        onPlateReleased?.Invoke(); // 연결된 기믹 해제 (필요시 사용)
    }
}