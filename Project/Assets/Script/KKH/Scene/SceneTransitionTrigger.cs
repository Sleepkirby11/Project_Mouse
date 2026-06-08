using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement; // 씬 전환을 위해 필수적으로 포함해야 합니다.

[RequireComponent(typeof(BoxCollider2D))]
public class SceneTransitionTrigger : MonoBehaviour
{
    [Header("이동할 씬 설정")]
    [Tooltip("이동하고자 하는 유니티 씬의 정확한 이름을 적어주세요.")]
    [SerializeField] private string nextSceneName;

    [Header("전환 연출 설정")]
    [Tooltip("트리거에 부딪힌 후 몇 초 뒤에 씬을 전환할지 정합니다.")]
    [SerializeField] private float transitionDelay = 0.5f;

    private bool isTransitioning = false;

    private void Awake()
    {
        // 충돌체 설정을 트리거로 강제 고정합니다.
        BoxCollider2D collider = GetComponent<BoxCollider2D>();
        collider.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        // 플레이어 태그를 확인하고, 이미 전환 중이 아닐 때만 실행합니다.
        if (other.CompareTag("Player") && !isTransitioning)
        {
            StartCoroutine(TransitionRoutine());
        }
    }

    private IEnumerator TransitionRoutine()
    {
        isTransitioning = true;
        Debug.Log($"[SceneTransition] {nextSceneName} 씬으로 이동을 시작합니다. ({transitionDelay}초 대기...)");

        // 이 타이밍에 페이드아웃 애니메이션을 실행하면 좋습니다.
        // 예: FadeManager.Instance.FadeOut();

        // 설정한 시간만큼 대기
        yield return new WaitForSeconds(transitionDelay);

        // 다음 씬 로드
        SceneManager.LoadScene(nextSceneName);
    }

    // 에디터 뷰에서 트리거 영역을 알아보기 쉽게 빨간색 박스로 표시합니다.
    private void OnDrawGizmos()
    {
        BoxCollider2D collider = GetComponent<BoxCollider2D>();
        if (collider != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireCube(transform.position + (Vector3)collider.offset, collider.size);
        }
    }
}