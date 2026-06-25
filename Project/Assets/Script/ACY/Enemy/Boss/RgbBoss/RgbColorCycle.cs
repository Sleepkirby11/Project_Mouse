using System.Collections;
using UnityEngine;

public class RgbColorCycle : MonoBehaviour
{
    [SerializeField] private float changeInterval = 10f;

    private EnemyStatus enemyStatus;
    private SpriteRenderer spriteRenderer;

    private void Awake()
    {
        enemyStatus = GetComponent<EnemyStatus>();
        spriteRenderer = GetComponentInChildren<SpriteRenderer>();
    }

    private void Start()
    {
        ChangeElementRandom();
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

        // 임시 색상 변경
        if (spriteRenderer != null)
        {
            switch (next)
            {
                case EnemyStatus.EnemyElement.Red:
                    spriteRenderer.color = Color.red;
                    break;

                case EnemyStatus.EnemyElement.Green:
                    spriteRenderer.color = Color.green;
                    break;

                case EnemyStatus.EnemyElement.Blue:
                    spriteRenderer.color = Color.blue;
                    break;
            }
        }

        Debug.Log($"RGB Boss Color Change : {next}");
    }
}