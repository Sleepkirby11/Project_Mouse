using UnityEngine;

public class Explosion : MonoBehaviour
{
    [Header("데미지 설정")]
    [SerializeField] private int damage = 2; // 폭발 데미지
    [SerializeField] private string poolKey = "Explosion"; // 풀링 키

    private Collider2D damageCollider;
    private AudioSource audioSource;
    private static float lastPlayTime = 0f;
    private const float SOUND_COOLDOWN = 0.15f;

    private void Awake()
    {
        damageCollider = GetComponent<Collider2D>();

        // 초기 설정: 콜라이더는 꺼둔 상태로 대기
        if (damageCollider != null)
        {
            damageCollider.enabled = false;
        }

        audioSource = gameObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.loop = false;
    }

    private void OnEnable()
    {
        // 풀에서 다시 꺼내질 때 콜라이더가 켜져있는 버그 방지
        if (damageCollider != null)
        {
            damageCollider.enabled = false;
        }

        // 오브젝트 풀 생성 시(초기화) 사운드 재생 방지
        if (transform.parent != null && transform.parent.GetComponent<PoolingManager>() != null)
        {
            return;
        }

        if (Time.time - lastPlayTime >= SOUND_COOLDOWN)
        {
            lastPlayTime = Time.time;
            PlayExplosionSound();
        }
    }

    // ─── 애니메이션 이벤트 연동 함수 ──────────────────────────────

    public void EnableHitbox()
    {
        if (damageCollider != null)
        {
            damageCollider.enabled = true;
        }
    }

    public void DisableHitbox()
    {
        if (damageCollider != null)
        {
            damageCollider.enabled = false;
        }
    }

    public void ReturnToPool()
    {
        PoolingManager.Instance.Return(poolKey, gameObject);
    }

    // ─── 데미지 처리 ──────────────────────────────────────────

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            PlayerStatus playerStatus = collision.GetComponent<PlayerStatus>();
            if (playerStatus != null)
            {
                playerStatus.TakeDamage(damage);
            }

            DisableHitbox();
        }
    }

    #region Audio Management
    private void PlayExplosionSound()
    {
        if (AudioManager.instance == null || audioSource == null) return;

        int sfxIndex = (int)AudioManager.SFX.RGB_BlackHole_Explosion;
        if (AudioManager.instance.sfxClips == null || sfxIndex < 0 || sfxIndex >= AudioManager.instance.sfxClips.Length)
        {
            return;
        }

        var sfxData = AudioManager.instance.sfxClips[sfxIndex];
        audioSource.clip = sfxData.clip;
        float globalVol = GameManager.instance != null ? GameManager.instance.sfxVolume : AudioManager.instance.sfxVolume;
        audioSource.volume = globalVol * sfxData.volumeScale;
        audioSource.pitch = UnityEngine.Random.Range(0.85f, 1.15f);
        audioSource.Play();
    }
    #endregion
}