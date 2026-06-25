using System.Collections;
using UnityEngine;
using UnityEngine.UI;

/*
 * 유령 공격 스크립트
 * 1. 플레이어와 유령이 겹치게 되면 "빙의" 상태이상 부여 (플레이어 조작 불가 및 강제 이동 + HP 1 감소)
 * 2. 빙의 시간이 지나면 유령은 실체화 + 플레이어로부터 멀어짐 (빙의 쿨타임)
 * 3. 빙의 쿨타임이 끝날 시 '행동패턴'의 1번으로 이동
 * 
 * 특이사항: 빙의 성공 시 플레이어 머리 위엔 빙의 게이지가 나타남 (슬라이더). 
 * 4초가 지나면 자동으로 해제되며, 좌우 방향키를 누를 시 단축 가능
 */
public class GhostEnemyAttack : MonoBehaviour
{
    #region Settings & Variables

    [Header("빙의 설정")]
    public float possessionDuration = 4f;   // 빙의 지속 시간 
    public float possessionMoveSpeed = 2.5f; // 강제 이동 속도
    public int possessionDamage = 1;         // 대미지
    public float damageInterval = 0.5f;      // 대미지 간격

    [Header("저항 수치")]
    public float keyReduceTime = 0.35f;      // 방향키 입력 시 빙의 시간 감소량

    [Header("UI")]
    public GameObject possessionGaugeObject; // 게이지 캔버스 오브젝트
    public Slider possessionSlider;          // 게이지 슬라이더

    private GhostEnemyMove ghostMove; 

    private Transform possessedPlayer;
    private Rigidbody2D playerRb;
    private PlayerStatus playerStatus;

    private Vector2 forceMoveDirection;

    private float possessionTimer;
    private bool isPossessing;

    private Coroutine possessionCoroutine;

    #endregion

    #region Unity Lifecycle

    private void Awake()
    {
        ghostMove = GetComponent<GhostEnemyMove>();
    }

    private void Update()
    {
        if (!isPossessing) // 빙의 중이 아닐 때는 연산 안 함
        {
            return;
        }

        HandleEscapeInput();
        UpdateGauge();
    }

    private void FixedUpdate()
    {
        if (!isPossessing)
        {
            return;
        }

        ForceMovePlayer(); // 플레이어 강제 이동
    }

    #endregion

    #region Possession Setup & Control

    public void StartPossession(Transform player)
    {
        if (player == null)
        {
            return;
        }

        if (possessionCoroutine != null)
        {
            StopCoroutine(possessionCoroutine);
        }

        possessedPlayer = player;
        playerRb = possessedPlayer.GetComponent<Rigidbody2D>();
        playerStatus = possessedPlayer.GetComponent<PlayerStatus>();

        if (playerStatus == null)
        {
            return;
        }

        FindPossessionUI();
        isPossessing = true;
        possessionTimer = possessionDuration;

        // 플레이어가 유령보다 오른쪽이면 오른쪽으로, 왼쪽이면 왼쪽으로 이동시킴
        float dirX = possessedPlayer.position.x > transform.position.x ? 1f : -1f;
        forceMoveDirection = new Vector2(dirX, 0f);

        if (forceMoveDirection == Vector2.zero) // 플레이어가 유령과 같은 x축에 있을 때 오른쪽으로 이동
        {
            forceMoveDirection = Vector2.right;
        }

        playerStatus.SetPossessed(true); // 빙의 상태이상 켬

        SetGauge(true); // 게이지 UI 표시
        UpdateGauge();

        possessionCoroutine = StartCoroutine(PossessionRoutine());
    }
    private void FindPossessionUI()
    {
        // 플레이어 오브젝트의 자식 중에서 빙의UI 캔버스 가져오기
        if (possessionGaugeObject == null && possessedPlayer != null)
        {
            Transform canvasTransform = possessedPlayer.Find("PossessionGaugeCanvas");
            if (canvasTransform != null)
            {
                possessionGaugeObject = canvasTransform.gameObject;
            }
            else
            {
                Debug.LogWarning($"[GhostEnemyAttack] {possessedPlayer.name}의 자식에서 'PossessionGaugeCanvas'를 찾을 수 없음");
            }
        }

        // 슬라이더까지 연결
        if (possessionGaugeObject != null && possessionSlider == null)
        {
            possessionSlider = possessionGaugeObject.GetComponentInChildren<Slider>(true);
        }

        if (possessionSlider == null)
        {
            Debug.LogWarning("[GhostEnemyAttack] PossessionGaugeCanvas의 자식에서 Slider(GaugeBar) 컴포넌트를 찾을 수 없습니다.");
        }
    }
    private IEnumerator PossessionRoutine()
    {
        float damageTimer = 0f;

        if (playerStatus != null)
        {
            playerStatus.TakeDamage(possessionDamage);
        }

        while (possessionTimer > 0f) // 빙의 지속 시간 동안 반복
        {
            possessionTimer -= Time.deltaTime; // 전체 타이머 감소
            damageTimer += Time.deltaTime;     // 지속 대미지 타이머 증가

            if (damageTimer >= damageInterval)
            {
                if (playerStatus != null)
                {
                    playerStatus.SetInvincible(false);
                    playerStatus.TakeDamage(possessionDamage);
                }

                damageTimer -= damageInterval;
            }

            yield return null; // 다음 프레임까지 대기
        }

        EndPossession(); // 빙의 종료
    }

    private void ForceMovePlayer()
    {
        if (playerRb == null)
        {
            return;
        }

        playerRb.linearVelocity = new Vector2(forceMoveDirection.x * possessionMoveSpeed, playerRb.linearVelocity.y);
    }

    private void EndPossession() // 빙의 종료
    {
        if (!isPossessing) 
        {
            return;
        }

        isPossessing = false;

        if (possessionCoroutine != null)
        {
            StopCoroutine(possessionCoroutine);
            possessionCoroutine = null;
        }

        if (playerStatus != null)
        {
            playerStatus.SetPossessed(false);
        }

        if (playerRb != null)
        {
            playerRb.linearVelocity = Vector2.zero;
        }

        SetGauge(false); // 게이지 숨김

        if (ghostMove != null) // 빙의 종료 후 유령이 플레이어에게서 도망침
        {
            ghostMove.EndPossessionAndRetreat();
        }

        // 캐싱 정보 초기화
        possessedPlayer = null;
        playerRb = null;
        playerStatus = null;
    }

    #endregion

    #region Input Handling & Gauge UI

    private void HandleEscapeInput()
    {
        // 좌우 키 입력 시 게이지 대폭 감소 (탈출 저항 메커니즘)
        if (Input.GetKeyDown(KeyCode.A) || Input.GetKeyDown(KeyCode.D))
        {
            possessionTimer -= keyReduceTime;
            possessionTimer = Mathf.Max(0f, possessionTimer);

            if (possessionTimer <= 0f)
            {
                EndPossession();
            }
        }
    }

    private void SetGauge(bool value)
    {
        if (possessionGaugeObject != null)
        {
            possessionGaugeObject.SetActive(value);
        }

        if (possessionSlider != null)
        {
            possessionSlider.maxValue = possessionDuration;
            possessionSlider.value = possessionTimer;
        }
    }

    private void UpdateGauge()
    {
        if (possessionSlider != null)
        {
            possessionSlider.value = possessionTimer;
        }
    }

    #endregion
}