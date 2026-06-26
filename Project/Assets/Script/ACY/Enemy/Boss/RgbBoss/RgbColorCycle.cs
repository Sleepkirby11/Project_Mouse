using System.Collections;
using UnityEngine;

public class RgbColorCycle : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private float changeInterval = 10f;

    [Header("Animator Overrides")]
    [SerializeField] private RuntimeAnimatorController baseController; //Red
    [SerializeField] private AnimatorOverrideController greenOverride;
    [SerializeField] private AnimatorOverrideController blueOverride;
    [SerializeField] private AnimatorOverrideController magentaOverride;

    private bool isFinalPhase = false;
    private EnemyStatus enemyStatus;
    private Animator animator;

    private void Awake()
    {
        enemyStatus = GetComponent<EnemyStatus>();
        animator = GetComponentInChildren<Animator>();
    }

    private void Start()
    {
        EnemyStatus.EnemyElement first = (EnemyStatus.EnemyElement)Random.Range(0, 3);

        enemyStatus.SetElement(first);
        ApplyController(GetController(first));
        StartCoroutine(ColorCycleRoutine());
    }

    private IEnumerator ColorCycleRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(changeInterval);

            if (isFinalPhase)
                continue;

            ChangeElementRandom();
        }
    }

    private void ChangeElementRandom()
    {
        EnemyStatus.EnemyElement current = enemyStatus.CurrentElement;
        EnemyStatus.EnemyElement next;

        do
        {
            next = (EnemyStatus.EnemyElement)Random.Range(0, 3);
        }
        while (next == current);

        enemyStatus.SetElement(next);
        ApplyController(GetController(next));

        Debug.Log($"RGB Boss Element & Animation Changed : {next}");
    }
    private RuntimeAnimatorController GetController(EnemyStatus.EnemyElement element)
    {
        return element switch
        {
            EnemyStatus.EnemyElement.Red => baseController,
            EnemyStatus.EnemyElement.Green => greenOverride,
            EnemyStatus.EnemyElement.Blue => blueOverride,
            EnemyStatus.EnemyElement.None => magentaOverride, // 발악 상태
            _ => baseController
        };
    }
    private void ApplyController(RuntimeAnimatorController targetController)
    {
        if (animator == null || targetController == null) return;

        AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
        float normalizedTime = stateInfo.normalizedTime;
        int stateHash = stateInfo.shortNameHash;

        animator.runtimeAnimatorController = targetController;
        animator.Play(stateHash, 0, normalizedTime);
    }
    public void EnterFinalPhase()
    {
        if (isFinalPhase)
            return;
        
        isFinalPhase = true;

        enemyStatus.SetElement(EnemyStatus.EnemyElement.None);
        ApplyController(GetController(EnemyStatus.EnemyElement.None));

        Debug.Log("발악 패턴 시작");
    }
    public void ExitFinalPhase()
    {
        if (isFinalPhase)
            return;

        isFinalPhase = false;

        ChangeElementRandom();   // 발악 끝나면 즉시 RGB 하나 선택
    }
}