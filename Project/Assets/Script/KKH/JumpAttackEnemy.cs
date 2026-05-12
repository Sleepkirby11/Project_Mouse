using UnityEngine;


public class JumpAttackEnemy : EnemyManager
{
    [Header("Enemy Status")]
    [SerializeField] private float maxHP;   //최대체력
    [SerializeField] private float currentHP;   //현재체력
    [SerializeField] private float attackDamege;    //공격력  
    [SerializeField] private float moveSpeed;   //이동속도
    [SerializeField] private float jumpHeight;   //도약 최고 높이 
    [SerializeField] private float jumpDuration;   //도약 소요 시간

    [Header("Enemy Settings")]
    [SerializeField] private float findPlayerRange; //플레이어를 인식하는 범위
    [SerializeField] private float attackRange; //공격을 시전하기 시작하는 공격 범위
    [SerializeField] private float attackCooldown;  //공격 - 공격 사이의 쿨타임

    [Header("Detection Settings")]
    [SerializeField] private LayerMask enemyLayer; //플레이어를 인식하는 범위

    private Rigidbody2D rb;
    private Transform enemyTransform;

    private bool foundPlayer = false; //한 번 플레이어를 인식하면 끝까지 추적하기 위한 bool
    private bool isDead = false;    //적 오브젝트가 살아있음을 알리기 위해 false로 초기화
    private bool isAttacking = false;
    private bool isJumping = false;

    //private Animator anim; //애니메이터 참조

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        currentHP = maxHP; //스크립트가 실행되면 현재체력을 최대체력으로 초기화
        //anim = GetComponentInChildren<Animator>();
    }

    private void Update()
    {
        if (isDead) return;

        FindPlayer();

        if(foundPlayer && enemyTransform != null)
        {
            MoveToPlayer();
        }
        else
        {
            //anim.SetBool
        }
    }

    public override void TakeDamage(float damage)
    {
        if (isDead) return;

        currentHP -= damage;

        if (currentHP <= 0) Die();
    }

    private void FindPlayer()
    {
        if (foundPlayer) return;
        Collider2D playerCollider = 
            Physics2D.OverlapCircle(transform.position, findPlayerRange, enemyLayer);
        if (playerCollider != null)
        {
            foundPlayer = true;
            enemyTransform = playerCollider.transform;
        }
    }

    private void MoveToPlayer()
    {
        float direction = 
            enemyTransform.position.x > transform.position.x ? 1f : -1f; //적이 뒤로 걷는 현상이 생기면 부호 서로 바꿔주기
        float distanceToPlayer = Mathf.Abs(enemyTransform.position.x - transform.position.x);
        if (distanceToPlayer > attackRange * 0.8f)
        {
            rb.linearVelocity = new Vector2(direction * moveSpeed, rb.linearVelocity.y);
            transform.localScale = new Vector3(direction, 1f, 1f);
            //anim.SetBool("isMoving", true); // 이동 애니메이션 ON
        }
        else
        {
            rb.linearVelocity = new Vector2(0, rb.linearVelocity.y);
            //anim.SetBool("isMoving", false); // 정지 시 OFF
        }
    }

    private void Attack()
    {

    }

    private void Die()
    {
        isDead = true;
        rb.linearVelocity = Vector2.zero;
        Debug.Log($"적 {gameObject}가 사망함");
        //anim.SetBool("isDead", true); //사망 애니메이션 출력

        Destroy(gameObject);    //오브젝트 파괴, 애니메이션 넣은 후 수정 필요함
    }

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        
    }

}
