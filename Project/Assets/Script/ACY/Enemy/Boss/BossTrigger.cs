using UnityEngine;

public class BossTrigger : MonoBehaviour
{
    [SerializeField] private GameObject boss;

    private bool activated = false;

    private void OnTriggerEnter2D(Collider2D other)
    {
        if (activated)
        {
            return;
        }

        if (other.CompareTag("Player"))
        {
            activated = true;
            boss.SetActive(true);
        }
    }
}