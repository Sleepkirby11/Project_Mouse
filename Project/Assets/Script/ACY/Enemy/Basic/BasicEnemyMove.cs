using System.Collections;
using UnityEngine;

/*
 �ൿ����: �¿츦 ��ȸ (�ν����Ϳ��� ��ġ ���� ����)
 �÷��̾� ���� �� �÷��̾ ����
 */
public class BasicEnemyMove : MonoBehaviour
{
    public enum EnemyState { Patrol, Chase } // ��ȸ����, ��������

    [Header("���� ����")]
    public EnemyState currentState = EnemyState.Patrol; // �ʱ� ���´� ��ȸ

    public bool isFacingRight = true; // �ʱ� ���� ���� (������)

    [Header("������ ����")]
    public float moveSpeed = 1f; // �ӵ�
    public float patrolRange = 5f; // �¿� ��ȸ �ݰ�

    [Header("���� ����")]
    public float stopDistance = 0.8f;   // �÷��̾�� �ּ� ���� �Ÿ�

    [Header("���� ����")]
    public float detectionRadius = 6f; // ���� ����
    public LayerMask targetLayer;      // Ÿ�� ���̾�

    private Transform myTransform;
    private Transform targetTransform;
    private Vector3 startPosition;
    private Vector3 patrolTarget;
    private float detectionRadiusSqr;

    private Animator animator; // �߰�
    private static readonly int IsMoving = Animator.StringToHash("IsMoving");   // �߰�
    private static readonly int IsChasing = Animator.StringToHash("IsChasing"); // �߰�

    private WaitForSeconds scanIntervalWFS;

    private void Awake()
    {
        myTransform = transform;
        startPosition = myTransform.position;

        detectionRadiusSqr = detectionRadius * detectionRadius;

        scanIntervalWFS = new WaitForSeconds(0.2f); // 0.2�� �ֱ� ��ĵ
        animator = GetComponentInChildren<Animator>();
    }

    private void Start()
    {
        // ���� ��ȸ ��ǥ ���� ����
        UpdatePatrolTarget();
        StartCoroutine(EnvironmentScanRoutine()); // ���� �ڷ�ƾ ����
    }

    private void Update()
    {
        switch (currentState) // ���¿� ���� �ൿ �б�
        {
            case EnemyState.Patrol: // ��ȸ ���¿����� �¿�� �̵�
                PatrolMovement();
                break;
            case EnemyState.Chase: // ���� ���¿����� �÷��̾ ���� �̵�
                ChaseMovement();
                break;
        }
    }
    private void SetState(EnemyState newState)
    {
        currentState = newState;

        bool isChasing = newState == EnemyState.Chase;
        animator.SetBool(IsMoving, !isChasing);   // Patrol�� ���� IsMoving
        animator.SetBool(IsChasing, isChasing);    // Chase�� ���� IsChasing
    }

    private IEnumerator EnvironmentScanRoutine()
    {
        while (true)
        {
            // 0.2�� ���
            yield return scanIntervalWFS;

            if (targetTransform == null)
            {
                // �÷��̾ Ÿ���õ��� ���� ������ ���� �ֺ� �ݰ� ���̾� �˻�
                Collider2D hit = Physics2D.OverlapCircle(myTransform.position, detectionRadius, targetLayer); 
                if (hit != null) // �����Ǹ� ����
                {
                    targetTransform = hit.transform;
                    SetState(EnemyState.Chase);
                }
            }
            else
            {
                float sqrDistance = (targetTransform.position - myTransform.position).sqrMagnitude; // Ÿ�ٰ��� �Ÿ� ���

                if (sqrDistance > detectionRadiusSqr) // ���� ������ ����� ��ȸ ���·�
                {
                    targetTransform = null;
                    SetState(EnemyState.Patrol); 
                    UpdatePatrolTarget();
                }
            }
        }
    }

    private void PatrolMovement()
    {
        float direction = patrolTarget.x - myTransform.position.x;

        if (direction > 0 && !isFacingRight)
        {
            Flip();
        }
        else if (direction < 0 && isFacingRight)
        {
            Flip();
        }
        myTransform.position = Vector3.MoveTowards(myTransform.position, patrolTarget, moveSpeed * Time.deltaTime); // ��ȸ ��ǥ �������� �̵�

        // ��ǥ ������ �����ߴ��� Ȯ��
        if ((patrolTarget - myTransform.position).sqrMagnitude < 0.01f)
        {
            UpdatePatrolTarget(); // ���ο� ��ȸ ��ǥ ���� ����
        }
    }

    private void ChaseMovement()
    {
        if (targetTransform == null) // Ÿ���� ������ ���� ����
        {
            return;
        }

        // ���� ��ȯ
        FlipToTarget();

        // X�� �Ÿ� ���
        float xDistance = Mathf.Abs(targetTransform.position.x - myTransform.position.x);

        // �ʹ� ������ ����
        if (xDistance <= stopDistance)
        {
            return;
        }

        Vector3 targetPos = new Vector3(targetTransform.position.x, myTransform.position.y, myTransform.position.z); //x �����θ� �̵�

        myTransform.position = Vector3.MoveTowards // Ÿ���� ���� �̵�
        ( 
            myTransform.position,
            targetPos,
            moveSpeed * Time.deltaTime
        );
    }

    private void UpdatePatrolTarget() // ��ȸ ��ǥ ���� ����
    {
        float randomX = Random.Range(-patrolRange, patrolRange); 
        patrolTarget = new Vector3(startPosition.x + randomX, myTransform.position.y, myTransform.position.z); 
    }
    private void FlipToTarget() // Ÿ���� ���� ���� ��ȯ
    {
        if (targetTransform == null)
        {
            return;
        }

        float direction = targetTransform.position.x - myTransform.position.x;

        if (direction > 0 && !isFacingRight)
        {
            Flip();
        }
        else if (direction < 0 && isFacingRight)
        {
            Flip();
        }
    }

    private void Flip()
    {
        isFacingRight = !isFacingRight;

        Vector3 scale = myTransform.localScale;
        scale.x *= -1;
        myTransform.localScale = scale;
    }
    // ���� ���� �ð�ȭ
    private void OnDrawGizmosSelected()
    {
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, detectionRadius);
    }
}