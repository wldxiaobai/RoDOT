using UnityEngine;
using static AttackHitInfo;

[RequireComponent(typeof(Collider2D))]
public class AttackHitInfo : MonoBehaviour
{
    public enum AttackGrade { Light, Heavy }
    public enum Position { Friendly, Neutral, Hostile }

    [Tooltip("伤害值")]
    public float Damage = 1f;

    [Tooltip("攻击分级，用于区分轻重攻击")]
    public AttackGrade Grade = AttackGrade.Light;

    [Tooltip("攻击立场")]
    public Position AttackPosition = Position.Friendly;

    [Tooltip("（仅重攻击使用）僵持时间")]
    public float StunDuration = 0.5f;

    [Tooltip("（仅重攻击使用）弹反判定窗口")]
    public float ParryWindow = 0.2f;

    public GameObject Source => gameObject;

    public bool used = false;

    public HitInfo GetHitInfo()
    {
        return new HitInfo
        {
            Damage = Damage,
            Grade = Grade,
            AttackPosition = AttackPosition,
            StunDuration = StunDuration,
            ParryWindow = ParryWindow,
            Source = Source,
            used = used
        };
    }
}

public struct HitInfo
{
    public bool IsValid => Source != null && !used;
    public float Damage;
    public AttackGrade Grade;
    public Position AttackPosition;
    public float StunDuration;
    public float ParryWindow;
    public GameObject Source;
    public bool used;

    public void Clear()
    {
        Damage = 0f;
        Grade = AttackGrade.Light;
        AttackPosition = Position.Friendly;
        StunDuration = 0f;
        ParryWindow = 0f;
        Source = null;
        used = false;
    }
}