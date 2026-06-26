using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PoisonArea : MonoBehaviour
{
    [Header("Pool Key")]
    [SerializeField] private string areaKey = "PoisonArea";

    [Header("Settings")]
    [SerializeField] private float duration = 5f;
    [SerializeField] private int damagePerTick = 5;
    [SerializeField] private float tickInterval = 0.5f;

    private List<PlayerStatus> playersInArea = new List<PlayerStatus>();
    private float tickTimer = 0f;

    private void OnEnable()
    {
        playersInArea.Clear();
        tickTimer = 0f;
        StartCoroutine(AutoReturnRoutine());
    }

    private void Update()
    {
        if (playersInArea.Count == 0) return;

        tickTimer += Time.deltaTime;
        if (tickTimer >= tickInterval)
        {
            tickTimer = 0f;
            foreach (var player in playersInArea)
            {
                if (player != null)
                {
                    // 수정 없이 기존 플레이어의 TakeDamage 그대로 사용
                    player.TakeDamage(damagePerTick);
                }
            }
        }
    }

    private IEnumerator AutoReturnRoutine()
    {
        yield return new WaitForSeconds(duration - 0.3f);

        Animator anim = GetComponentInChildren<Animator>();

        yield return new WaitForSeconds(0.3f);

        PoolingManager.Instance.Return(areaKey, gameObject);
    }

    private void OnTriggerEnter2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            PlayerStatus player = collision.GetComponent<PlayerStatus>();
            if (player != null && !playersInArea.Contains(player))
            {
                playersInArea.Add(player);
            }
        }
    }

    private void OnTriggerExit2D(Collider2D collision)
    {
        if (collision.CompareTag("Player"))
        {
            PlayerStatus player = collision.GetComponent<PlayerStatus>();
            if (player != null && playersInArea.Contains(player))
            {
                playersInArea.Remove(player);
            }
        }
    }
}