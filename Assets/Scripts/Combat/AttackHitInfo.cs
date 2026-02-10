using System;
using System.Collections.Generic;
using UnityEngine;
using static AttackHitInfo;

public enum AttackGrade { Light, Heavy }
public enum Position { Friendly, Neutral, Hostile }
public enum HitResult { None, Hit, Blocked }

[RequireComponent(typeof(Collider2D))]
public class AttackHitInfo : MonoBehaviour
{
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

    public Dictionary<GameObject, HitResult> hitObjects = new();

    public Action<GameObject> OnHit;
    public Action<GameObject> OnBlocked;

    public HitInfo GetHitInfo()
    {
        return new HitInfo(this)
        {
            Damage = Damage,
            Grade = Grade,
            AttackPosition = AttackPosition,
            StunDuration = StunDuration,
            ParryWindow = ParryWindow,
            Source = Source
        };
    }

    public void RecordHitObject(GameObject obj, HitResult result = HitResult.Hit)
    {
        hitObjects[obj] = result;
        switch(result)
        {
            case HitResult.Hit:
                OnHit?.Invoke(obj);
                break;
            case HitResult.Blocked:
                OnBlocked?.Invoke(obj);
                break;
        }
    }

    public void ClearHitObjects()
    {
        hitObjects.Clear();
    }

    public HitResult GetHitResult(GameObject obj)
    {
        return hitObjects.TryGetValue(obj, out var result) ? result : HitResult.None;
    }
}

public sealed class HitInfo
{
    public bool IsValid => Source != null && Origin != null;
    public float Damage;
    public AttackGrade Grade;
    public Position AttackPosition;
    public float StunDuration;
    public float ParryWindow;
    public GameObject Source;
    public AttackHitInfo Origin { get; private set; }

    public HitInfo()
    {
    }

    public HitInfo(AttackHitInfo origin)
    {
        Origin = origin;
    }

    public HitInfo Clone()
    {
        return new HitInfo(Origin)
        {
            Damage = Damage,
            Grade = Grade,
            AttackPosition = AttackPosition,
            StunDuration = StunDuration,
            ParryWindow = ParryWindow,
            Source = Source
        };
    }

    public void SetOrigin(AttackHitInfo origin)
    {
        Origin = origin;
    }

    public void Clear()
    {
        Damage = 0f;
        Grade = AttackGrade.Light;
        AttackPosition = Position.Friendly;
        StunDuration = 0f;
        ParryWindow = 0f;
        Source = null;
        Origin = null;
    }
}
