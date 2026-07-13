using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public class TutorialManager : MonoBehaviour
{
    public static TutorialManager Instance { get; private set; }

    public enum TutorialCondition
    {
        None,            // 대화 상자 클릭/엔터 등으로 넘어감
        MoveHorizontal,  // A/D 키를 눌러 좌우로 이동
        Jump,            // Space 키 등으로 점프
        Dash,            // 마우스 방향 대시 (Shift 등)
        Attack,          // 마우스 드래그 일반 공격 선 그리기 완료
        StanceChange,    // 스탠스 변경 (Tab 등)
        MakeGround,      // 발판 그리기 완료
        Skill            // 스킬 사용 (W 등)
    }

    [System.Serializable]
    public class TutorialStep
    {
        public string stepName; // 인스펙터 식별용
        [TextArea(3, 5)]
        public string dialogueText; // 표시될 텍스트
        public TutorialCondition condition; // 넘어가는 조건
        public float delayBeforeNext = 0.5f; // 조건 충족 후 대기 시간
        public bool closeOnComplete = false; // 이 단계 완료 후 대화창을 닫을지 여부
    }

    [Header("튜토리얼 단계 정의")]
    public List<TutorialStep> steps = new List<TutorialStep>();

    private int currentStepIndex = -1;
    private bool isStepActive = false;
    private bool isStepCompleted = false;

    // 완료된 튜토리얼 단계 이름을 기록하여 세션 동안 유지
    private HashSet<string> completedStepNames = new HashSet<string>();

    // 조건 감지를 위한 상태 저장 변수들
    private PlayerInput playerInput;
    private PlayerStatus.Stance initialStance;
    private bool stanceChanged = false;
    private bool attackDrawingStarted = false;
    private bool makeGroundDrawingStarted = false;

    private void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
        }
        else
        {
            // 이미 존재하는 인스턴스에 현재 씬의 튜토리얼 단계를 병합하고, 자신은 파괴함
            Instance.MergeSteps(this.steps);
            Destroy(gameObject);
        }
    }

    private void OnEnable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded += OnSceneLoaded;
    }

    private void OnDisable()
    {
        UnityEngine.SceneManagement.SceneManager.sceneLoaded -= OnSceneLoaded;
    }

    private void OnSceneLoaded(UnityEngine.SceneManagement.Scene scene, UnityEngine.SceneManagement.LoadSceneMode mode)
    {
        // 씬 전환 시 진행 중인 튜토리얼 코루틴 및 상태 리셋
        StopAllCoroutines();
        isStepActive = false;
        isStepCompleted = false;
        currentStepIndex = -1;

        if (scene.name == "Tutorial")
        {
            completedStepNames.Clear();
        }
    }

    private void Start()
    {
        // 첫 진입 시 대화창을 기본적으로 닫아둡니다.
        if (UI.Instance != null)
        {
            UI.Instance.HideDialogue();
        }
    }

    private void Update()
    {
        if (!isStepActive || isStepCompleted || currentStepIndex < 0 || currentStepIndex >= steps.Count) 
            return;

        TutorialStep currentStep = steps[currentStepIndex];

        // 플레이어 레퍼런스 및 PlayerInput 캐싱
        if (playerInput == null && Player.instance != null)
        {
            playerInput = Player.instance.GetComponent<PlayerInput>();
        }

        if (CheckCondition(currentStep.condition))
        {
            StartCoroutine(CompleteStepRoutine(currentStep));
        }
        else if (currentStep.condition == TutorialCondition.None)
        {
            // None인 경우, 클릭 또는 일반 대화 진행 키(Enter, Space 등) 입력 시 진행
            if (Input.GetMouseButtonDown(0) || Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.Space))
            {
                StartCoroutine(CompleteStepRoutine(currentStep));
            }
        }
    }

    /// <summary>
    /// 다른 씬에서 튜토리얼 매니저의 단계들을 병합할 때 사용합니다.
    /// </summary>
    public void MergeSteps(List<TutorialStep> newSteps)
    {
        if (newSteps == null) return;
        foreach (var newStep in newSteps)
        {
            if (newStep == null || string.IsNullOrEmpty(newStep.stepName)) continue;
            if (!steps.Exists(s => s.stepName == newStep.stepName))
            {
                steps.Add(newStep);
            }
        }
    }

    /// <summary>
    /// 특정 이름의 튜토리얼이 이미 완료되었는지 확인합니다.
    /// </summary>
    public bool IsStepCompleted(string stepName)
    {
        if (string.IsNullOrEmpty(stepName)) return false;
        return completedStepNames.Contains(stepName);
    }

    /// <summary>
    /// 특정 인덱스의 튜토리얼이 이미 완료되었는지 확인합니다.
    /// </summary>
    public bool IsStepCompleted(int index)
    {
        if (index < 0 || index >= steps.Count) return false;
        string name = steps[index].stepName;
        return !string.IsNullOrEmpty(name) && completedStepNames.Contains(name);
    }

    /// <summary>
    /// 특정 이름으로 튜토리얼 단계를 실행합니다.
    /// </summary>
    public void TriggerTutorialStep(string stepName)
    {
        if (string.IsNullOrEmpty(stepName)) return;

        if (completedStepNames.Contains(stepName))
        {
            return;
        }

        int index = steps.FindIndex(s => s.stepName == stepName);
        if (index >= 0)
        {
            TriggerTutorialStep(index);
        }
        else
        {
            Debug.LogWarning($"[TutorialManager] Step with name '{stepName}' not found.");
        }
    }

    /// <summary>
    /// 특정 인덱스의 튜토리얼 단계를 강제로 실행합니다. (트리거 존 등에서 호출)
    /// </summary>
    public void TriggerTutorialStep(int stepIndex)
    {
        if (stepIndex < 0 || stepIndex >= steps.Count)
        {
            Debug.LogWarning($"[TutorialManager] Invalid step index: {stepIndex}");
            return;
        }

        // 이미 완료된 단계면 무시
        string stepName = steps[stepIndex].stepName;
        if (!string.IsNullOrEmpty(stepName) && completedStepNames.Contains(stepName))
        {
            return;
        }

        // 현재 이미 작동 중인 단계와 동일하면 중복 작동하지 않음
        if (isStepActive && currentStepIndex == stepIndex)
            return;

        StopAllCoroutines();
        StartStep(stepIndex);
    }

    private void StartStep(int index)
    {
        currentStepIndex = index;
        isStepActive = true;
        isStepCompleted = false;

        TutorialStep step = steps[index];

        // UI에 대화창 텍스트 표시
        if (UI.Instance != null)
        {
            UI.Instance.ShowDialogue(step.dialogueText);
        }

        // 조건 감지를 위한 상태 초기화
        if (Player.instance != null)
        {
            if (Player.instance.status != null)
            {
                initialStance = Player.instance.status.currentStance;
            }
        }
        stanceChanged = false;
        attackDrawingStarted = false;
        makeGroundDrawingStarted = false;

        Debug.Log($"[TutorialManager] Step {index} 시작: {step.stepName} (조건: {step.condition})");
    }

    private bool CheckCondition(TutorialCondition condition)
    {
        if (Player.instance == null) return false;

        switch (condition)
        {
            case TutorialCondition.MoveHorizontal:
                // 이동 입력 축이 활성화되었거나 실제 속도가 있을 때
                if (playerInput != null)
                {
                    var moveAction = playerInput.actions.FindAction("Move");
                    if (moveAction != null)
                    {
                        Vector2 moveVal = moveAction.ReadValue<Vector2>();
                        if (Mathf.Abs(moveVal.x) > 0.1f) return true;
                    }
                }
                return Mathf.Abs(Player.instance.inputVec.x) > 0.1f || Mathf.Abs(Player.instance.rigid.linearVelocityX) > 0.1f;

            case TutorialCondition.Jump:
                // 점프 액션이 트리거되었거나 jumpCount가 줄어든 경우
                if (playerInput != null)
                {
                    var jumpAction = playerInput.actions.FindAction("Jump");
                    if (jumpAction != null && jumpAction.triggered) return true;
                }
                return Player.instance.jumpCount == 1 && Player.instance.rigid.linearVelocityY > 0.1f;

            case TutorialCondition.Dash:
                // 대시 액션이 트리거되었거나 대시로 인해 jumpCount가 0이 된 경우
                if (playerInput != null)
                {
                    // 현재 프로젝트의 InputActionAsset에서는 대시가 'Sprint'로 명명되어 있습니다.
                    var dashAction = playerInput.actions.FindAction("Sprint") ?? playerInput.actions.FindAction("Dash");
                    if (dashAction != null && dashAction.triggered) return true;
                }
                return Player.instance.jumpCount == 0;

            case TutorialCondition.Attack:
                // 일반 공격 드래그 시작 감지
                if (Player.instance.cursor != null)
                {
                    if (Player.instance.cursor.isMove)
                    {
                        attackDrawingStarted = true;
                    }
                    // 그리기 시작한 후, 마우스를 떼어서 isMove가 다시 false가 되었을 때 완료로 판정
                    if (attackDrawingStarted && !Player.instance.cursor.isMove)
                    {
                        return true;
                    }
                }
                return false;

            case TutorialCondition.StanceChange:
                if (playerInput != null)
                {
                    var stanceAction = playerInput.actions.FindAction("Stance");
                    if (stanceAction != null && stanceAction.triggered) return true;
                }
                // 스탠스가 초기값에서 변경되었는지 체크
                if (Player.instance.status != null && Player.instance.status.currentStance != initialStance)
                {
                    stanceChanged = true;
                }
                return stanceChanged;

            case TutorialCondition.MakeGround:
                // 발판 그리기 드래그 시작 감지
                if (Player.instance.groundCursor != null)
                {
                    if (Player.instance.groundCursor.isMove)
                    {
                        makeGroundDrawingStarted = true;
                    }
                    // 발판 그리기 완료 감지
                    if (makeGroundDrawingStarted && !Player.instance.groundCursor.isMove)
                    {
                        return true;
                    }
                }
                return false;

            case TutorialCondition.Skill:
                if (playerInput != null)
                {
                    var skillAction = playerInput.actions.FindAction("Skill");
                    if (skillAction != null && skillAction.triggered) return true;
                }
                // 플레이어 스킬 활성화 상태 확인
                return Player.instance.isSkill;

            default:
                return false;
        }
    }

    private IEnumerator CompleteStepRoutine(TutorialStep step)
    {
        isStepCompleted = true;
        Debug.Log($"[TutorialManager] Step {currentStepIndex} 조건 충족. 대기 시간: {step.delayBeforeNext}초");

        yield return new WaitForSeconds(step.delayBeforeNext);

        // 완료 기록 추가
        if (!string.IsNullOrEmpty(step.stepName))
        {
            completedStepNames.Add(step.stepName);
        }

        if (step.closeOnComplete)
        {
            isStepActive = false;
            if (UI.Instance != null)
            {
                UI.Instance.HideDialogue();
            }
            Debug.Log($"[TutorialManager] Step {currentStepIndex} 완료 및 대화창 닫힘.");
        }
        else
        {
            // 다음 단계로 바로 진행
            StartStep(currentStepIndex + 1);
        }
    }
}
