using UnityEngine;

[RequireComponent(typeof(Collider2D))]
public class AttackHitInfo : MonoBehaviour
{
    public enum AttackGrade { Light, Heavy }

    [Tooltip("伤害值")]
    public float Damage = 1f;

    [Tooltip("攻击分级，用于区分轻重攻击")]
    public AttackGrade Grade = AttackGrade.Light;

    [Tooltip("（仅重攻击使用）僵持时间")]
    public float StunDuration = 0.5f;

    [Tooltip("（仅重攻击使用）弹反判定窗口")]
    public float ParryWindow = 0.2f;

    public GameObject Source => gameObject;
}