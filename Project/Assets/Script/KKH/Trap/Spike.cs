using UnityEngine;

public class Spike : MonoBehaviour
{
    [Header("데미지 설정")]
    [SerializeField] private int damage;

    private void OnTriggerStay2D(Collider2D collision)
    {
        // 충돌한 오브젝트가 "Player" 태그를 가지고 있는지 확인
        if (collision.CompareTag("Player"))
        {
            // 충돌한 플레이어 오브젝트로부터 PlayerStatus 컴포넌트를 가져옴
            PlayerStatus player = collision.GetComponent<PlayerStatus>();

            if (player != null)
            {
                // PlayerStatus 스크립트에 데미지를 받는 메서드
                player.TakeDamage(damage);

                Debug.Log($"플레이어가 가시에 부딪혀 {damage}의 피해를 입었습니다.");
            }
        }
    }
}
