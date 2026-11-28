using UnityEngine;

/// <summary>
/// Zone 프리팹 내 저장 대상 오브젝트(적, 트리거, 기믹 등)에 부여되는 결정론적 식별자 컴포넌트입니다.
/// </summary>
public class PersistentEntity : MonoBehaviour
{
    public int ZoneId { get; private set; } = -1;
    public string EntityId { get; private set; }
    public bool IsInitialized { get; private set; }

    public void Initialize(int zoneId, string entityId)
    {
        ZoneId = zoneId;
        EntityId = entityId;
        IsInitialized = true;
    }
}
