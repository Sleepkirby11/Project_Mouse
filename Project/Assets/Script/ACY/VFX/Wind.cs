using UnityEngine;

public class Wind : MonoBehaviour
{
    [Header("바람 연출 설정")]
    [SerializeField] private float speed = 5f;        
    [SerializeField] private float lifeTime = 1.5f;    

    private const string POOL_KEY = "BossWindBlast";
    private float currentLifeTime;
    private BossProjectileTrail trailVfx;

    private void Awake()
    {
        trailVfx = BossProjectileTrail.Attach(gameObject, BossProjectileTrail.TrailStyle.IceWind, 99);
    }

    private void OnEnable()
    {
        currentLifeTime = lifeTime;

        if (transform.parent != null && transform.parent.GetComponent<PoolingManager>() != null)
        {
            return;
        }

        trailVfx?.SetActive(true, true, clearBefore: true);
    }

    private void Update()
    {
        transform.Translate(Vector3.left * speed * Time.deltaTime);

        currentLifeTime -= Time.deltaTime;
        if (currentLifeTime <= 0f)
        {
            PoolingManager.Instance.Return(POOL_KEY, gameObject);
        }
    }
}