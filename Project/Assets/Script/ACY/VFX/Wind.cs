using UnityEngine;

public class Wind : MonoBehaviour
{
    [Header("�ٶ� ���� ����")]
    [SerializeField] private float speed = 5f;        
    [SerializeField] private float lifeTime = 1.5f;    

    private const string POOL_KEY = "BossWindBlast";
    private float currentLifeTime;

    private void OnEnable()
    {
        currentLifeTime = lifeTime;
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