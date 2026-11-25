using UnityEngine;

public class DashVFX : MonoBehaviour
{
    [Header("참조 컴포넌트")]
    [SerializeField] private ParticleSystem windRingParticle;
    [SerializeField] private TrailRenderer dashTrail;

    [Header("방향 회전 설정")]
    [Tooltip("이 속도 이하일 때는 풍압 링 회전을 유지합니다.")]
    [SerializeField] private float minVelocityThreshold = 0.2f;

    private Rigidbody2D parentRb;
    private Vector3 lastWorldPosition;
    private bool hasLastPosition;

    private void Awake()
    {
        if (windRingParticle == null)
        {
            windRingParticle = GetComponent<ParticleSystem>();
        }

        if (dashTrail == null)
        {
            dashTrail = GetComponentInChildren<TrailRenderer>();
        }

        parentRb = GetComponentInParent<Rigidbody2D>();
    }

    private void OnEnable()
    {
        if (parentRb == null)
        {
            parentRb = GetComponentInParent<Rigidbody2D>();
        }
        dashTrail.gameObject.SetActive(true);

        lastWorldPosition = transform.position;
        hasLastPosition = true;
        if(TryGetComponent<ParticleSystem>(out ParticleSystem particleSystem))
        {
            var main = particleSystem.main;
            main.loop = true;
        }

        UpdateRingRotation();

        if (dashTrail != null)
        {
            dashTrail.Clear();
            dashTrail.emitting = true;
        }

        if (windRingParticle != null)
        {
            windRingParticle.Play();
        }
    }

    private void OnDisable()
    {
        hasLastPosition = false;

        if (dashTrail != null)
        {
            dashTrail.emitting = false;
            dashTrail.Clear();
        }
    }

    private void LateUpdate()
    {
        UpdateRingRotation();
        lastWorldPosition = transform.position;
        hasLastPosition = true;
    }

    public void StartDash()
    {
        if (!gameObject.activeSelf)
        {
            gameObject.SetActive(true);
            return;
        }

        UpdateRingRotation();

        if (dashTrail != null)
        {
            dashTrail.Clear();
            dashTrail.emitting = true;
        }

        if (windRingParticle != null)
        {
            windRingParticle.Play();
        }
    }

    public void StopDash()
    {
        if (dashTrail != null)
        {
            dashTrail.emitting = false;
        }

        if (windRingParticle != null)
        {
            windRingParticle.Stop(true, ParticleSystemStopBehavior.StopEmitting);
        }
    }

    private void UpdateRingRotation()
    {
        Vector2 moveDir = Vector2.zero;

        if (parentRb != null)
        {
            moveDir = parentRb.linearVelocity;
        }
        else if (hasLastPosition && Time.deltaTime > 0f)
        {
            Vector3 delta = transform.position - lastWorldPosition;
            moveDir = new Vector2(delta.x, delta.y) / Time.deltaTime;
        }

        if (moveDir.sqrMagnitude >= minVelocityThreshold * minVelocityThreshold)
        {
            float angle = Mathf.Atan2(moveDir.y, moveDir.x) * Mathf.Rad2Deg;
            Transform target = windRingParticle != null ? windRingParticle.transform : transform;
            target.rotation = Quaternion.Euler(0f, 0f, angle);
        }
    }
}
