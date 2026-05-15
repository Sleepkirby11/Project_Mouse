using UnityEngine;

public interface IHittable
{
    void TakeHit(Vector2 knockbackForce);
}
