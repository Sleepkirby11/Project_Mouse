using UnityEngine;

public class Spike : MonoBehaviour
{
    [Header("데미지 설정")]
    [SerializeField] private int damage;

    private PlayerStatus overlappingPlayer;

    private void FixedUpdate()
    {
        if (overlappingPlayer != null)
        {
            overlappingPlayer.TakeDamage(damage);
        }
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (!collision.CompareTag("Player")) return;

        PlayerStatus player = collision.GetComponent<PlayerStatus>();
        if (player != null)
        {
            overlappingPlayer = player;
            player.TakeDamage(damage);
        }
    }

    private void OnTriggerStay2D(Collider2D collision)
    {
        if (overlappingPlayer != null || !collision.CompareTag("Player")) return;

        PlayerStatus player = collision.GetComponent<PlayerStatus>();
        if (player != null)
        {
            overlappingPlayer = player;
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (!collision.CompareTag("Player")) return;

        PlayerStatus player = collision.GetComponent<PlayerStatus>();
        if (player != null && player == overlappingPlayer)
        {
            overlappingPlayer = null;
        }
    }

    private void OnDisable()
    {
        overlappingPlayer = null;
    }
}
