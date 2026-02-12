using UnityEngine;

public class SceneThingsOutPut : MonoBehaviour
{
    [Header("血量")]
    [Tooltip("当前生命值")]
    public int currentHP;

    [Header("状态")]
    [Tooltip("是否存活（血量>0时为true）")]
    public bool isAlive = true;

    private void HandleIncomingAttack(GameObject other)
    {
        if (other.TryGetComponent<AttackHitInfo>(out var hitInfo))
        {
            if (hitInfo.GetHitResult(gameObject) != HitResult.None)
                return;

            var incoming = hitInfo.GetHitInfo();

            if (incoming.IsValid)
            {
                currentHP -= Mathf.RoundToInt(incoming.Damage);
                hitInfo.RecordHitObject(gameObject);
                if (currentHP <= 0 && isAlive)
                {
                    isAlive = false;
                }
            }
        }
    }

    private void OnCollisionEnter2D(Collision2D collision)
    {
        HandleIncomingAttack(collision.gameObject);
    }

    private void OnTriggerEnter2D(Collider2D collider)
    {
        HandleIncomingAttack(collider.gameObject);
    }
}