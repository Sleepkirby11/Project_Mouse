using UnityEngine;

public class Wall : MonoBehaviour
{
    private MagicBarrierVFX barrierVfx;

    private void Awake()
    {
        barrierVfx = MagicBarrierVFX.Attach(gameObject);
    }

    public void Collapse()
    {
        Debug.Log($"{gameObject.name} 벽이 파괴되었습니다.");

        if (barrierVfx == null)
        {
            barrierVfx = GetComponent<MagicBarrierVFX>();
        }

        if (barrierVfx != null)
        {
            barrierVfx.PlayCollapseAndDestroy(gameObject);
            return;
        }

        Destroy(gameObject);
    }

    // 벽을 다시 활성화하는 함수
    public void Rebuild()
    {
        Debug.Log($"{gameObject.name} 벽이 복구(활성화)되었습니다.");

        // 오브젝트를 다시 활성화합니다.
        gameObject.SetActive(true);
        if (barrierVfx == null)
        {
            barrierVfx = MagicBarrierVFX.Attach(gameObject);
        }
        barrierVfx?.OnRebuild();
    }
}
