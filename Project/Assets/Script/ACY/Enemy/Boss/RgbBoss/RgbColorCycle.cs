using System.Collections;
using UnityEngine;

public class RgbColorCycle : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private float changeInterval = 10f;

    [Header("Animator Overrides")]
    [SerializeField] private RuntimeAnimatorController baseController;
    [SerializeField] private AnimatorOverrideController greenOverride;
    [SerializeField] private AnimatorOverrideController blueOverride;

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
        ApplyControllerDirect(first); // Play() 없이 컨트롤러만 교체
        StartCoroutine(ColorCycleRoutine());
    }

    private IEnumerator ColorCycleRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(changeInterval);
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

        // 색에 따른 애니메이터 컨트롤러 교체
        switch (next)
        {
            case EnemyStatus.EnemyElement.Red:
                ApplyController(baseController);  
                break;
            case EnemyStatus.EnemyElement.Green:
                ApplyController(greenOverride);
                break;
            case EnemyStatus.EnemyElement.Blue:
                ApplyController(blueOverride);
                break;
        }

        Debug.Log($"RGB Boss Element & Animation Changed : {next}");
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
    private void ApplyControllerDirect(EnemyStatus.EnemyElement element)
    {
        RuntimeAnimatorController controller = element switch
        {
            EnemyStatus.EnemyElement.Green => greenOverride,
            EnemyStatus.EnemyElement.Blue => blueOverride,
            _ => baseController,
        };

        if (animator == null || controller == null) return;
        animator.runtimeAnimatorController = controller;
    }
}