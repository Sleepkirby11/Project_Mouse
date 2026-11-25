using UnityEngine;

public class BlueBossFlip : MonoBehaviour
{
    public enum FacingMode { Player, Movement }

    public FacingMode facingMode = FacingMode.Player;
    public Vector2 moveDirection = Vector2.zero;

    [Header("타겟 설정")]
    [SerializeField] private Transform playerTransform;
    public bool isFacingRight = true;

    void Start()
    {
        if (playerTransform == null)
        {
            GameObject player = GameObject.FindWithTag("Player");
            if (player != null)
            {
                playerTransform = player.transform;
            }
        }
    }

    void Update()
    {
        if (BossIntroCutscene.IsPlaying)
        {
            return;
        }

        FlipToTarget();
    }

    public void FacePlayer(Transform target = null)
    {
        if (target != null)
        {
            playerTransform = target;
        }
        else if (playerTransform == null)
        {
            GameObject player = GameObject.FindWithTag("Player");
            if (player != null)
            {
                playerTransform = player.transform;
            }
        }

        if (playerTransform != null)
        {
            float direction = playerTransform.position.x - transform.position.x;
            if (direction > 0 && !isFacingRight)
            {
                Flip();
            }
            else if (direction < 0 && isFacingRight)
            {
                Flip();
            }
        }
        else if (isFacingRight)
        {
            Flip();
        }
    }

    private void FlipToTarget()
    {
        if (facingMode == FacingMode.Player)
        {
            if (playerTransform == null)
            {
                return;
            }
            float direction = playerTransform.position.x - transform.position.x;
            if (direction > 0 && !isFacingRight)
            {
                Flip();
            }
            else if (direction < 0 && isFacingRight)
            {
                Flip();
            }
        }
        else
        {
            if (moveDirection.x > 0 && !isFacingRight)
            {
                Flip();
            }
            else if (moveDirection.x < 0 && isFacingRight)
            {
                Flip();
            }
        }
    }

    private void Flip()
    {
        isFacingRight = !isFacingRight;

        Vector3 scale = transform.localScale;
        scale.x *= -1;
        transform.localScale = scale;
    }
}
