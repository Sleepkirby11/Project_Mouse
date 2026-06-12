using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement; // �� ��ȯ�� ���� �ʼ������� �����ؾ� �մϴ�.

[RequireComponent(typeof(BoxCollider2D))]
public class SceneTransitionTrigger : MonoBehaviour
{
    [Header("�̵��� �� ����")]
    [Tooltip("�̵��ϰ��� �ϴ� ����Ƽ ���� ��Ȯ�� �̸��� �����ּ���.")]
    [SerializeField] private string nextSceneName;

    [Header("��ȯ ���� ����")]
    [Tooltip("Ʈ���ſ� �ε��� �� �� �� �ڿ� ���� ��ȯ���� ���մϴ�.")]
    [SerializeField] private float transitionDelay = 0.5f;

    private bool isTransitioning = false;

    private void Awake()
    {
        BoxCollider2D collider = GetComponent<BoxCollider2D>();
        collider.isTrigger = true;
    }

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (other.CompareTag("Player") && !isTransitioning)
        {
            StartCoroutine(TransitionRoutine());
        }
    }

    private IEnumerator TransitionRoutine()
    {
        isTransitioning = true;
        Debug.Log($"[SceneTransition] {nextSceneName} ������ �̵��� �����մϴ�. ({transitionDelay}�� ���...)");


        yield return new WaitForSeconds(transitionDelay);

        SceneManager.LoadScene(nextSceneName);
    }

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