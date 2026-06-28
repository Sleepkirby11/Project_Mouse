using System.Collections;
using UnityEngine;

public class IcePlatform : MonoBehaviour
{
    [Header("설정")]
    [SerializeField] private float shakeDuration = 0.8f;  // 흔들리는 시간
    [SerializeField] private float shakeMagnitude = 0.05f; // 흔들리는 강도
    [SerializeField] private float destroyDelay = 2.0f;    // 떨어진 후 오브젝트가 파괴될 시간

    private Vector3 originalPos;
    private Rigidbody2D rb;
    private bool isTriggered = false;

    void Start()
    {
        rb = GetComponent<Rigidbody2D>();
        originalPos = transform.localPosition;

        // 처음에는 떨어지지 않도록 Kinematic으로 설정
        rb.bodyType = RigidbodyType2D.Kinematic;
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        // 플레이어 태그 확인 및 중복 발동 방지
        if (collision.gameObject.CompareTag("Player") && !isTriggered)
        {
            // 플레이어가 '위'에서 밟았는지 확인 (정교한 판정을 원할 때 사용)
            if (collision.contacts[0].normal.y < -0.5f)
            {
                StartCoroutine(ShakeAndFall());
            }
        }
    }

    private IEnumerator ShakeAndFall()
    {
        isTriggered = true;
        float elapsed = 0.0f;

        // 1. 흔들리는 단계
        while (elapsed < shakeDuration)
        {
            // 원래 위치 기준 매 프레임 랜덤한 미세 오프셋 계산
            float x = Random.Range(-1f, 1f) * shakeMagnitude;
            float y = Random.Range(-1f, 1f) * shakeMagnitude;

            transform.localPosition = new Vector3(originalPos.x + x, originalPos.y + y, originalPos.z);
            elapsed += Time.deltaTime;

            yield return null; // 다음 프레임까지 대기
        }

        // 흔들림이 끝나면 위치를 원래대로 살짝 맞추고 복귀
        transform.localPosition = originalPos;

        // 2. 떨어지는 단계 (Dynamic으로 변경하여 중력 적용)
        rb.bodyType = RigidbodyType2D.Dynamic;

        // 3. 메모리 관리를 위해 일정 시간 뒤 삭제
        Destroy(gameObject, destroyDelay);
    }
}
