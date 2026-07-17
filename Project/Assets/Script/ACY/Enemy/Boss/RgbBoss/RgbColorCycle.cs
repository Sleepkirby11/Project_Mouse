using System.Collections;
using UnityEngine;

public class RgbColorCycle : MonoBehaviour
{
    #region Inspector Fields
    [Header("Animator Overrides")]
    [SerializeField] private RuntimeAnimatorController baseController; // Red
    [SerializeField] private AnimatorOverrideController greenOverride;
    [SerializeField] private AnimatorOverrideController blueOverride;
    [SerializeField] private AnimatorOverrideController magentaOverride;
    #endregion

    #region Private Fields
    private bool isFinalPhase = false;
    private EnemyStatus enemyStatus;
    private Animator animator;

    private static readonly int CastingTrigger = Animator.StringToHash("Casting");
    #endregion

    #region Unity Lifecycle
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
    }
    #endregion

    #region Public Methods
    public void ChangeElementRandom()
    {
        EnemyStatus.EnemyElement current = enemyStatus.CurrentElement;
        EnemyStatus.EnemyElement next;

        do
        {
            next = (EnemyStatus.EnemyElement)Random.Range(0, 3);
        }
        while (next == current);

        enemyStatus.SetElement(next);
        ApplyController(GetController(next), "Casting");
    }

    public void EnterFinalPhase()
    {
        if (isFinalPhase)
            return;
        
        isFinalPhase = true;

        enemyStatus.SetElement(EnemyStatus.EnemyElement.None);
        ApplyController(GetController(EnemyStatus.EnemyElement.None), "Casting");
    }

    public void ExitFinalPhase()
    {
        if (!isFinalPhase)
            return;

        isFinalPhase = false;

        // 발악 끝나면 즉시 RGB 하나 선택하며 사이클 재개
        ChangeElementRandom();
    }
    #endregion

    #region Private Methods
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

    private void ApplyController(RuntimeAnimatorController targetController, string stateName = "Idle")
    {
        if (animator == null || targetController == null) return;

        animator.runtimeAnimatorController = targetController;
        animator.Update(0f);
        animator.Play(stateName, 0, 0f);
    }
    #endregion
}