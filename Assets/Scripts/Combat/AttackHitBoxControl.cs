using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public abstract class AttackHitBoxControl : MonoBehaviour
{
    [Tooltip("攻击物体引用")]
    [SerializeField] private GameObject attack;

    private Collider2D attackHitbox;
    private AttackHitInfo attackHitInfo;

    protected void Awake()
    {
        attackHitbox = attack.GetComponent<Collider2D>();
        attackHitInfo = attack.GetComponent<AttackHitInfo>();

        attackHitbox.enabled = false;
        attackHitInfo.ClearHitObjects();
    }

    protected void EnableAttackHitbox()
    {
        var x = GetComponent<SpriteRenderer>().flipX ? -1 : 1;
        attack.transform.localScale = new Vector3(x, 1, 1);
        attackHitbox.enabled = true;
        attackHitInfo.ClearHitObjects();
    }

    protected void DisableAttackHitbox()
    {
        attackHitbox.enabled = false;
    }
}
