using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// Boss1 :电锯+磨刀石之门
/// 
///电锯：玩家格挡&击中即回复锋利。有韧性，破韧后僵直，可随意击打。
///门：玩家的攻击目标，血量归零时电锯同时停止运动和伤害。玩家击中时少量回复锋利。
///
///每个动作仅会对玩家产生一次伤害/被格挡效果。
///
///嵌入状态：电锯不在场上。嵌入状态下仅会释放动作1、动作2。
/// 动作1：跃锯
///	    在玩家附近的地板位置冒火星，然后电锯竖直跃起一个比较矮的高度，玩家可跳起击中。然后电锯掉落回地板，继续嵌入状态。（轻攻击）
/// 动作2：磨砺
///	    在地板与门的交汇处冒火星，然后电锯从这一位置出现并向左突进，途径上冒火星。然后电锯进入出现状态。（轻攻击）
///
///出现状态：玩家能看见场上的电据，电锯旋转并不断冒火星。出现状态下仅会释放动作3~5。
/// 动作3：锻击（释放条件：电锯在玩家左方）
///	    电锯移动到玩家左方一段距离（在屏幕看得见的情况下取尽量远的距离），然后向玩家突进。（轻攻击）
///	    突进期间被玩家格挡成功时，后退一段距离并中断突进。继续出现状态。
///	    突进进行完毕（即向右突进到了门内）后则进入嵌入状态。
/// 动作4：沉重锻击（释放条件：电锯在玩家左方）
///	    电锯移动到玩家左方一段距离（在屏幕看得见的情况下取尽量远的距离），开始变红，冒更多火星，然后向玩家突进。（重攻击）
///	    突进期间被玩家抵御成功，且玩家 主动退出僵持/打出弹反/弹反超时 时，后退一大段距离并中断突进。继续出现状态，但下一个动作必定是嵌入。
/// 动作5：嵌入
///	    电锯进入地板，期间没有碰撞伤害。然后进入嵌入状态。
///
///僵直状态：电锯露在地板外且不转，没有碰撞伤害。
/// 动作6：僵直 
///     电锯露在地板外且不转，没有碰撞伤害。结束后，下一个动作必定是嵌入。
///
///待机状态：玩家不在场地内时为此状态。电锯不在场上。
///
/// </summary>

public class Boss1 : BaseEnemy
{
    private enum SawState
    {
        embed,
        displayed,
        stunned,
        idle
    }

    private enum ActionType
    {
        leapSaw,
        grind,
        strike,
        heavyStrike,
        embed,
        stun
    }

    [Tooltip("电锯子物体引用")]
    [SerializeField] private Saw sawObject;

    [Header("Boss战区域")]
    [Tooltip("Boss战区域左下角")]
    [SerializeField] private Vector2 arenaMin = new Vector2(-10f, -5f);
    [Tooltip("Boss战区域右上角")]
    [SerializeField] private Vector2 arenaMax = new Vector2(10f, 5f);

    private SawState currentSawState = SawState.embed;
    private bool forceNextEmbed;

    // 行为名称常量
    private const string LeapSawName = "LeapSaw";
    private const string GrindName = "Grind";
    private const string StrikeName = "Strike";
    private const string HeavyStrikeName = "HeavyStrike";
    private const string EmbedName = "Embed";
    private const string WaitIdleName = "WaitIdle";

    // ---------------初始化---------------

    protected override void EnemyInit()
    {
        RegisterAllBehaviours();
        currentSawState = SawState.embed;

        // 初始化时隐藏电锯贴图，确保嵌入状态下电锯不可见
        if (sawObject != null)
        {
            var sr = sawObject.GetComponent<SpriteRenderer>();
            if (sr != null)
            {
                sr.enabled = false;
            }
        }
    }

    private void RegisterAllBehaviours()
    {
        RegisterSawBehaviour(LeapSawName, 0f, () => Behav_LeapSaw());
        RegisterSawBehaviour(GrindName, 0f, () => Behav_Grind());
        RegisterSawBehaviour(StrikeName, 0f, () => Behav_Strike());
        RegisterSawBehaviour(HeavyStrikeName, 0f, () => Behav_HeavyStrike());
        RegisterSawBehaviour(EmbedName, 0f, () => Behav_Embed());
        RegisterSawBehaviour(WaitIdleName, 0f, () => Behav_WaitIdle());
    }

    /// <summary>
    /// 用单个 ActionNode 创建 ActSeq 并注册为行为。
    /// </summary>
    private void RegisterSawBehaviour(string name, float preDelay, System.Func<IEnumerator> action)
    {
        var seq = new ActSeq();
        var node = seq.CreateActionNode(action);
        seq.Start.SetNext(node);
        node.SetNext(seq.End);
        AddBehaviour(name, preDelay, seq);
    }

    // ---------------决策逻辑---------------

    protected override string DecideNextBehaviour()
    {
        // 玩家不在Boss战范围内时，不发出动作指令
        if (!IsPlayerInArena())
        {
            // 电锯仍在场上时先执行嵌入，将其收回
            if (currentSawState == SawState.displayed)
            {
                return EmbedName;
            }

            currentSawState = SawState.idle;
            forceNextEmbed = false;
            return WaitIdleName;
        }

        // 玩家回到场地时，从嵌入状态恢复
        if (currentSawState == SawState.idle)
        {
            currentSawState = SawState.embed;
        }

        // 沉重锻击被格挡 或 破韧僵直结束后，下一动作必定是嵌入
        if (forceNextEmbed && currentSawState == SawState.displayed)
        {
            forceNextEmbed = false;
            return EmbedName;
        }

        switch (currentSawState)
        {
            case SawState.embed:
                // 嵌入状态：随机选择 动作1(跃锯) 或 动作2(磨砺)
                return Random.value < 0.5f ? LeapSawName : GrindName;

            case SawState.displayed:
                // 出现状态：随机选择 动作3(锻击) / 动作4(沉重锻击) / 动作5(嵌入)
                float roll = Random.value;
                if (roll < 0.33f) return StrikeName;
                else if (roll < 0.66f) return HeavyStrikeName;
                else return EmbedName;

            default:
                return WaitIdleName;
        }
    }

    // ---------------玩家检测---------------

    private bool IsPlayerInArena()
    {
        if (GlobalPlayer.Instance == null || GlobalPlayer.Instance.Player == null)
        {
            return false;
        }

        Vector3 pos = GlobalPlayer.Instance.Player.transform.position;
        return pos.x >= arenaMin.x && pos.x <= arenaMax.x &&
               pos.y >= arenaMin.y && pos.y <= arenaMax.y;
    }

    // ---------------行为协程---------------

    /// <summary>
    /// 动作1：跃锯。embed → embed。
    /// </summary>
    private IEnumerator Behav_LeapSaw()
    {
        if (sawObject == null) yield break;

        if (CheckAndWaitStagger())
        {
            yield return WaitForStagger();
            yield break;
        }

        sawObject.PlayLeapSaw();
        yield return null;

        while (sawObject.IsLeapSawPlaying)
        {
            yield return null;
        }

        if (sawObject.IsToughnessBreaking)
        {
            yield return WaitForStagger();
            yield break;
        }

        currentSawState = SawState.embed;
    }

    /// <summary>
    /// 动作2：磨砺。embed → displayed。
    /// </summary>
    private IEnumerator Behav_Grind()
    {
        if (sawObject == null) yield break;

        if (CheckAndWaitStagger())
        {
            yield return WaitForStagger();
            yield break;
        }

        sawObject.PlayGrindSaw();
        yield return null;

        while (sawObject.IsGrindSawPlaying)
        {
            yield return null;
        }

        if (sawObject.IsToughnessBreaking)
        {
            yield return WaitForStagger();
            yield break;
        }

        currentSawState = SawState.displayed;
    }

    /// <summary>
    /// 动作3：锻击。displayed → 被格挡击退则 displayed，否则 embed。
    /// </summary>
    private IEnumerator Behav_Strike()
    {
        if (sawObject == null) yield break;

        if (CheckAndWaitStagger())
        {
            yield return WaitForStagger();
            yield break;
        }

        sawObject.PlayForgeSaw();
        yield return null;

        while (sawObject.IsForgeSawPlaying)
        {
            yield return null;
        }

        if (sawObject.IsToughnessBreaking)
        {
            yield return WaitForStagger();
            yield break;
        }

        currentSawState = sawObject.WasForgeBlocked ? SawState.displayed : SawState.embed;
    }

    /// <summary>
    /// 动作4：沉重锻击。displayed → 被格挡击退则 displayed 且下一动作必为嵌入，否则 embed。
    /// </summary>
    private IEnumerator Behav_HeavyStrike()
    {
        if (sawObject == null) yield break;

        if (CheckAndWaitStagger())
        {
            yield return WaitForStagger();
            yield break;
        }

        sawObject.PlayHeavyForgeSaw();
        yield return null;

        while (sawObject.IsHeavyForgeSawPlaying)
        {
            yield return null;
        }

        if (sawObject.IsToughnessBreaking)
        {
            yield return WaitForStagger();
            yield break;
        }

        if (sawObject.WasHeavyForgeBlocked)
        {
            currentSawState = SawState.displayed;
            forceNextEmbed = true;
        }
        else
        {
            currentSawState = SawState.embed;
        }
    }

    /// <summary>
    /// 动作5：嵌入。displayed → embed。
    /// </summary>
    private IEnumerator Behav_Embed()
    {
        if (sawObject == null) yield break;

        if (CheckAndWaitStagger())
        {
            yield return WaitForStagger();
            yield break;
        }

        sawObject.PlayEmbedSaw();
        yield return null;

        while (sawObject.IsEmbedSawPlaying)
        {
            yield return null;
        }

        if (sawObject.IsToughnessBreaking)
        {
            yield return WaitForStagger();
            yield break;
        }

        currentSawState = SawState.embed;
    }

    /// <summary>
    /// 待机行为：玩家不在场地内时空转等待。
    /// </summary>
    private IEnumerator Behav_WaitIdle()
    {
        yield return new WaitForSeconds(1f);
    }

    // ---------------僵直等待---------------

    /// <summary>
    /// 检查电锯是否正处于破韧僵直中（用于行为开始前的边界检查）。
    /// </summary>
    private bool CheckAndWaitStagger()
    {
        return sawObject != null &&
               (sawObject.IsToughnessBreaking || sawObject.IsStaggerSawPlaying);
    }

    /// <summary>
    /// 等待电锯僵直动作完成。结束后标记为 displayed，并强制下一动作为嵌入。
    /// </summary>
    private IEnumerator WaitForStagger()
    {
        currentSawState = SawState.stunned;

        while (sawObject != null && sawObject.IsStaggerSawPlaying)
        {
            yield return null;
        }

        // 僵直结束后，电锯露在地面可见，状态为 displayed
        // 下一个动作必定是嵌入
        currentSawState = SawState.displayed;
        forceNextEmbed = true;
    }
}
