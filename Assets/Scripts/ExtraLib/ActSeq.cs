#nullable enable
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 管理一个可配置的动作图，每个节点代表一个可执行片段（行动/分支/循环/等待）。
/// 通过 <see cref="Cursor"/> 向外提供安全的构建器接口，内部使用 <see cref="_INode"/> 抽象实现多态执行。
/// </summary>
public class ActSeq
{
    private readonly Dictionary<Guid, INode> nodes = new();
    private Guid startNodeId;
    private Guid endNodeId;
    private Guid currentTailId;

    private Coroutine? activeSequence;
    private MonoBehaviour? currentActor;

    /// <summary>
    /// 当前是否正在执行图中的动作序列。
    /// </summary>
    public bool IsPlaying => activeSequence != null;

    /// <summary>
    /// 图的起点游标，用它开始向外添加节点。
    /// </summary>
    public Cursor Start => new(this, startNodeId);

    /// <summary>
    /// 图的终点游标，可用于将分支/动作直接连接到末尾。
    /// </summary>
    public Cursor End => new(this, endNodeId);

    public ActSeq()
    {
        InitializeGraph();
    }

    /// <summary>
    /// 克隆当前图到 <paramref name="newActSeq"/>，包括所有节点与连线。
    /// </summary>
    public void CloneTo(ref ActSeq newActSeq)
    {
        if (newActSeq == null)
        {
            newActSeq = new ActSeq();
        }
        else
        {
            newActSeq.Clear();
        }

        var mapping = new Dictionary<Guid, Guid>
        {
            [startNodeId] = newActSeq.startNodeId,
            [endNodeId] = newActSeq.endNodeId
        };

        foreach (var node in nodes.Values)
        {
            var clone = CloneNode(node);
            newActSeq.nodes[clone.Id] = clone;
            mapping[node.Id] = clone.Id;
        }

        foreach (var kvp in nodes)
        {
            if (!mapping.TryGetValue(kvp.Key, out var cloneId))
            {
                continue;
            }

            var destination = newActSeq.nodes[cloneId];
            if (kvp.Value is NodeBase sourceBase && destination is NodeBase targetBase)
            {
                targetBase.Next = MapGuid(sourceBase.Next, mapping);
            }

            if (kvp.Value is ConditionalNode sourceConditional && destination is ConditionalNode targetConditional)
            {
                targetConditional.NextTrue = MapGuid(sourceConditional.NextTrue, mapping);
                targetConditional.NextFalse = MapGuid(sourceConditional.NextFalse, mapping);
            }
        }

        newActSeq.currentTailId = mapping.ContainsKey(currentTailId)
            ? mapping[currentTailId]
            : newActSeq.startNodeId;
    }

    /// <summary>
    /// 创建一个普通动作节点，执行传入协程后直接跳向下一节点。
    /// </summary>
    public Cursor CreateActionNode(Func<MonoBehaviour, IEnumerator> action)
    {
        if (action == null)
        {
            throw new ArgumentNullException(nameof(action));
        }

        var node = new ActionNode(action)
        {
            Next = endNodeId
        };
        AddNode(node);
        return new Cursor(this, node.Id);
    }

    /// <summary>
    /// 创建一个普通动作节点，执行传入协程后直接跳向下一节点。
    /// 不依赖 <see cref="MonoBehaviour"/> 参数的重载，适合动作与某个宿主绑定的场景。
    /// </summary>
    public Cursor CreateActionNode(Func<IEnumerator> action)
    {
        if (action == null)
        {
            throw new ArgumentNullException(nameof(action));
        }

        return CreateActionNode(_ => action());
    }

    /// <summary>
    /// 创建条件分支节点，根据条件判断结果选择 true/false 分支。
    /// </summary>
    public Cursor CreateConditionalNode(Func<MonoBehaviour, bool> predicate)
    {
        if (predicate == null)
        {
            throw new ArgumentNullException(nameof(predicate));
        }

        var node = new ConditionalNode(predicate);
        AddNode(node);
        return new Cursor(this, node.Id);
    }

    /// <summary>
    /// 创建条件分支节点，根据条件判断结果选择 true/false 分支。
    /// 不依赖 <see cref="MonoBehaviour"/> 参数的重载，适合动作与某个宿主绑定的场景。
    /// </summary>
    public Cursor CreateConditionalNode(Func<bool> predicate)
    {
        if (predicate == null)
        {
            throw new ArgumentNullException(nameof(predicate));
        }

        return CreateConditionalNode(_ => predicate());
    }

    /// <summary>
    /// 创建重复动作节点，按最大次数循环执行指定协程。
    /// </summary>
    public Cursor CreateRepeatNode(
        Func<MonoBehaviour, IEnumerator> loopBody,
        int maxIterations = 1)
    {
        if (loopBody == null)
        {
            throw new ArgumentNullException(nameof(loopBody));
        }

        var node = new RepeatNode(loopBody, maxIterations)
        {
            Next = endNodeId
        };
        AddNode(node);
        return new Cursor(this, node.Id);
    }

    /// <summary>
    /// 创建重复动作节点，按最大次数循环执行指定协程。
    /// 不依赖 <see cref="MonoBehaviour"/> 参数的重载，适合动作与某个宿主绑定的场景。
    /// </summary>
    public Cursor CreateRepeatNode(
        Func<IEnumerator> loopBody,
        int maxIterations = 1)
    {
        if (loopBody == null)
        {
            throw new ArgumentNullException(nameof(loopBody));
        }

        return CreateRepeatNode(
            actor => loopBody(),
            maxIterations);
    }

    /// <summary>
    /// 创建 DoWhile 节点，循环执行动作直到条件不成立。
    /// </summary>
    public Cursor CreateDoWhileNode(
        Func<MonoBehaviour, IEnumerator> loopBody,
        Func<MonoBehaviour, bool>? condition = null)
    {
        if (loopBody == null)
        {
            throw new ArgumentNullException(nameof(loopBody));
        }

        var node = new DoWhileNode(loopBody, condition)
        {
            Next = endNodeId
        };
        AddNode(node);
        return new Cursor(this, node.Id);
    }

    /// <summary>
    /// 创建 DoWhile 节点，循环执行动作直到条件不成立。
    /// 不依赖 <see cref="MonoBehaviour"/> 参数的重载，适合动作与某个宿主绑定的场景。
    /// </summary>
    public Cursor CreateDoWhileNode(
        Func<IEnumerator> loopBody,
        Func<bool>? condition = null)
    {
        if (loopBody == null)
        {
            throw new ArgumentNullException(nameof(loopBody));
        }

        return CreateDoWhileNode(
            actor => loopBody(),
            condition == null ? null : (_ => condition()));
    }

    /// <summary>
    /// 清空当前图并重置运行状态。
    /// </summary>
    public void Clear()
    {
        Stop();
        InitializeGraph();
    }

    /// <summary>
    /// 使用指定 <see cref="MonoBehaviour"/> 启动图执行。
    /// </summary>
    public void Play(MonoBehaviour actor)
    {
        if (actor == null)
        {
            throw new ArgumentNullException(nameof(actor));
        }

        if (IsPlaying)
        {
            Stop();
        }

        ResetRuntimeState();
        currentActor = actor;
        activeSequence = actor.StartCoroutine(ActCoroutine(actor));
    }

    /// <summary>
    /// 停止当前正在执行的协程，并清理引用。
    /// </summary>
    public void Stop()
    {
        if (currentActor == null)
        {
            return;
        }

        if (activeSequence != null)
        {
            currentActor.StopCoroutineChecked(activeSequence);
            activeSequence = null;
        }

        currentActor = null;
    }

    private void AddNode(NodeBase node)
    {
        nodes[node.Id] = node;
    }

    /// <summary>
    /// 核心执行循环，按当前节点的逻辑逐步推进。
    /// </summary>
    private IEnumerator ActCoroutine(MonoBehaviour actor)
    {
        var currentId = startNodeId;
        while (currentId != Guid.Empty)
        {
            if (!nodes.TryGetValue(currentId, out var node))
            {
                break;
            }

            var routine = node.Execute(actor, this, out var nextId);
            if (routine != null)
            {
                yield return routine;
            }

            currentId = nextId;
        }

        activeSequence = null;
        currentActor = null;
    }

    private static Guid? MapGuid(Guid? source, IReadOnlyDictionary<Guid, Guid> mapping) =>
        source.HasValue && mapping.TryGetValue(source.Value, out var mapped) ? mapped : null;

    private void InitializeGraph()
    {
        nodes.Clear();
        var start = CreateNodeInternal(NodeType.Start);
        var end = CreateNodeInternal(NodeType.End);
        start.Next = end.Id;
        startNodeId = start.Id;
        endNodeId = end.Id;
        currentTailId = startNodeId;
    }

    private void ResetRuntimeState()
    {
        foreach (var node in nodes.Values)
        {
            if (node is RepeatNode repeat)
            {
                repeat.IterationCount = 0;
            }
        }
    }

    private NodeBase CreateNodeInternal(NodeType type)
    {
        NodeBase node = type switch
        {
            NodeType.Start => new StartNode(),
            NodeType.End => new EndNode(),
            NodeType.Action => new ActionNode(),
            NodeType.Conditional => new ConditionalNode(),
            NodeType.Repeat => new RepeatNode(),
            NodeType.DoWhile => new DoWhileNode(),
            _ => throw new ArgumentOutOfRangeException(nameof(type), type, "未知的节点类型。")
        };

        nodes[node.Id] = node;
        return node;
    }

    private NodeBase CloneNode(INode source)
    {
        return source switch
        {
            StartNode => new StartNode(),
            EndNode => new EndNode(),
            ActionNode action => new ActionNode(action.Action),
            ConditionalNode conditional => new ConditionalNode(conditional.Predicate),
            RepeatNode repeat => new RepeatNode(repeat.LoopBody, repeat.MaxIterations),
            DoWhileNode doWhile => new DoWhileNode(doWhile.LoopBody, doWhile.ContinueCondition),
            _ => throw new InvalidOperationException("无法克隆未知类型的节点。")
        };
    }

    private NodeBase GetNodeBase(Guid nodeId)
    {
        if (!nodes.TryGetValue(nodeId, out var node))
        {
            throw new InvalidOperationException("节点不存在。");
        }

        if (node is NodeBase baseNode)
        {
            return baseNode;
        }

        throw new InvalidOperationException("节点类型不支持 Next 操作。");
    }

    private void SetNext(Guid from, Guid to)
    {
        var node = GetNodeBase(from);
        if (!nodes.ContainsKey(to))
        {
            throw new InvalidOperationException("目标节点不存在。");
        }

        node.Next = to;
    }

    /// <summary>
    /// 仅能用于条件节点，设置 true/false 两条出边。
    /// </summary>
    private void SetBranches(Guid nodeId, Guid trueTarget, Guid falseTarget)
    {
        if (!nodes.TryGetValue(nodeId, out var node))
        {
            throw new InvalidOperationException("节点不存在。");
        }

        if (node is not ConditionalNode conditional)
        {
            throw new InvalidOperationException("SetBranches 只能用于条件分流节点。");
        }

        if (!nodes.ContainsKey(trueTarget) || !nodes.ContainsKey(falseTarget))
        {
            throw new InvalidOperationException("分支目标节点不存在。");
        }

        conditional.NextTrue = trueTarget;
        conditional.NextFalse = falseTarget;
    }

    /// <summary>
    /// 删除节点并将所有指向它的连线修正为终点，防止残留引用。
    /// 被删除的节点的后继节点将悬空，因此在删除节点之前请确保提前处理后继节点，或者持有该节点的后继节点的引用，以便重新连接。
    /// </summary>
    private void CutMapByNode(Guid nodeId)
    {
        if (nodeId == startNodeId || nodeId == endNodeId)
        {
            throw new InvalidOperationException("不能删除起点或终点节点。");
        }

        if (!nodes.ContainsKey(nodeId))
        {
            return;
        }

        foreach (var node in nodes.Values)
        {
            if (node is NodeBase baseNode && baseNode.Next == nodeId)
            {
                baseNode.Next = endNodeId;
            }

            if (node is ConditionalNode conditional)
            {
                if (conditional.NextTrue == nodeId)
                {
                    conditional.NextTrue = endNodeId;
                }
                if (conditional.NextFalse == nodeId)
                {
                    conditional.NextFalse = endNodeId;
                }
            }
        }

        nodes.Remove(nodeId);

        if (currentTailId == nodeId)
        {
            currentTailId = startNodeId;
            foreach (var entry in nodes)
            {
                if (entry.Value is NodeBase baseNode && baseNode.Next == endNodeId)
                {
                    currentTailId = entry.Key;
                }
            }
        }
    }

    private interface INode
    {
        Guid Id { get; }
        NodeType Type { get; }
        IEnumerator? Execute(MonoBehaviour actor, ActSeq owner, out Guid nextId);
    }

    internal abstract class NodeBase : INode
    {
        public Guid Id { get; } = Guid.NewGuid();
        public abstract NodeType Type { get; }
        public Guid? Next { get; set; }

        public abstract IEnumerator? Execute(MonoBehaviour actor, ActSeq owner, out Guid nextId);
    }

    private sealed class StartNode : NodeBase
    {
        public override NodeType Type => NodeType.Start;

        public override IEnumerator? Execute(MonoBehaviour actor, ActSeq owner, out Guid nextId)
        {
            nextId = Next ?? owner.endNodeId;
            return null;
        }
    }

    private sealed class EndNode : NodeBase
    {
        public override NodeType Type => NodeType.End;

        public override IEnumerator? Execute(MonoBehaviour actor, ActSeq owner, out Guid nextId)
        {
            nextId = Guid.Empty;
            return null;
        }
    }

    /// <summary>
    /// 代表常规动作节点，执行完后沿 <see cref="Next"/> 前进。
    /// </summary>
    private sealed class ActionNode : NodeBase
    {
        public ActionNode(Func<MonoBehaviour, IEnumerator>? action = null)
        {
            Action = action;
        }

        public override NodeType Type => NodeType.Action;
        public Func<MonoBehaviour, IEnumerator>? Action { get; set; }

        public override IEnumerator? Execute(MonoBehaviour actor, ActSeq owner, out Guid nextId)
        {
            nextId = Next ?? owner.endNodeId;
            return Action?.Invoke(actor);
        }
    }

    /// <summary>
    /// 条件节点会通过 Predicate 决定下一步走哪条分支。
    /// </summary>
    private sealed class ConditionalNode : NodeBase
    {
        public ConditionalNode(Func<MonoBehaviour, bool>? predicate = null)
        {
            Predicate = predicate;
        }

        public override NodeType Type => NodeType.Conditional;
        public Func<MonoBehaviour, bool>? Predicate { get; set; }
        public Guid? NextTrue { get; set; }
        public Guid? NextFalse { get; set; }

        public override IEnumerator? Execute(MonoBehaviour actor, ActSeq owner, out Guid nextId)
        {
            var predicateResult = Predicate?.Invoke(actor) ?? false;
            nextId = (predicateResult ? NextTrue : NextFalse) ?? owner.endNodeId;
            return null;
        }
    }

    /// <summary>
    /// Repeat 节点根据最大次数重复执行循环体。
    /// </summary>
    private sealed class RepeatNode : NodeBase
    {
        public RepeatNode(
            Func<MonoBehaviour, IEnumerator>? loopBody = null,
            int maxIterations = 0)
        {
            LoopBody = loopBody;
            MaxIterations = Math.Max(0, maxIterations);
        }

        public override NodeType Type => NodeType.Repeat;
        public Func<MonoBehaviour, IEnumerator>? LoopBody { get; set; }
        public int MaxIterations { get; set; }
        public int IterationCount { get; set; }

        public override IEnumerator? Execute(MonoBehaviour actor, ActSeq owner, out Guid nextId)
        {
            if (MaxIterations > 0 && IterationCount >= MaxIterations)
            {
                IterationCount = 0;
                nextId = Next ?? owner.endNodeId;
                return null;
            }

            nextId = Id;
            IterationCount++;
            return LoopBody?.Invoke(actor);
        }
    }

    /// <summary>
    /// DoWhile 节点在执行动作后根据条件决定是否继续。
    /// </summary>
    private sealed class DoWhileNode : NodeBase
    {
        private bool exitRequested = false;

        public DoWhileNode(
            Func<MonoBehaviour, IEnumerator>? loopBody = null,
            Func<MonoBehaviour, bool>? continueCondition = null)
        {
            LoopBody = loopBody;
            ContinueCondition = continueCondition;
        }

        public override NodeType Type => NodeType.DoWhile;
        public Func<MonoBehaviour, IEnumerator>? LoopBody { get; set; }
        public Func<MonoBehaviour, bool>? ContinueCondition { get; set; }

        public override IEnumerator? Execute(MonoBehaviour actor, ActSeq owner, out Guid nextId)
        {
            if (exitRequested)
            {
                exitRequested = false;
                nextId = Next ?? owner.endNodeId;
                return null;
            }

            if (LoopBody == null)
            {
                exitRequested = false;
                nextId = Next ?? owner.endNodeId;
                return null;
            }

            nextId = Id;
            exitRequested = false;
            return RunMonitoredAction(actor);
        }

        private IEnumerator RunMonitoredAction(MonoBehaviour actor)
        {
            while (true)
            {
                var actionRoutine = LoopBody?.Invoke(actor);
                if (actionRoutine == null)
                {
                    exitRequested = true;
                    yield break;
                }

                while (actionRoutine.MoveNext())
                {
                    yield return actionRoutine.Current;
                    if (ContinueCondition != null && !ContinueCondition(actor))
                    {
                        exitRequested = true;
                        yield break;
                    }
                }

                var shouldContinue = ContinueCondition?.Invoke(actor) ?? false;
                if (!shouldContinue)
                {
                    exitRequested = true;
                    yield break;
                }
            }
        }
    }

    public enum NodeType
    {
        Start,
        Action,
        Conditional,
        Repeat,
        DoWhile,
        End
    }

    /// <summary>
    /// 公开游标用于搭建图链。提供节点工厂、连接设置与访问当前节点类型。
    /// </summary>
    public readonly struct Cursor
    {
        private readonly ActSeq owner;

        internal Cursor(ActSeq owner, Guid nodeId)
        {
            this.owner = owner;
            NodeId = nodeId;
        }

        public Guid NodeId { get; }
        public bool IsValid => owner != null && owner.nodes.ContainsKey(NodeId);
        public NodeType NodeType => GetNode().Type;
        public Guid? Next
        {
            get
            {
                var node = GetNode();
                return node is NodeBase baseNode ? baseNode.Next : null;
            }
        }

        public Guid? NextTrue
        {
            get
            {
                if (GetNode() is ConditionalNode conditional)
                {
                    return conditional.NextTrue;
                }

                return null;
            }
        }

        public Guid? NextFalse
        {
            get
            {
                if (GetNode() is ConditionalNode conditional)
                {
                    return conditional.NextFalse;
                }

                return null;
            }
        }

        public Cursor CreateActionNode(Func<MonoBehaviour, IEnumerator> action)
        {
            EnsureIsBound();
            return owner.CreateActionNode(action);
        }

        public Cursor CreateActionNode(Func<IEnumerator> action)
        {
            EnsureIsBound();
            return owner.CreateActionNode(action);
        }

        public Cursor CreateConditionalNode(Func<MonoBehaviour, bool> predicate)
        {
            EnsureIsBound();
            return owner.CreateConditionalNode(predicate);
        }

        public Cursor CreateConditionalNode(Func<bool> predicate)
        {
            EnsureIsBound();
            return owner.CreateConditionalNode(predicate);
        }

        public Cursor CreateRepeatNode(
            Func<MonoBehaviour, IEnumerator> loopBody,
            int maxIterations = 1)
        {
            EnsureIsBound();
            return owner.CreateRepeatNode(loopBody, maxIterations);
        }

        public Cursor CreateRepeatNode(
            Func<IEnumerator> loopBody,
            int maxIterations = 1)
        {
            EnsureIsBound();
            return owner.CreateRepeatNode(loopBody, maxIterations);
        }

        public Cursor CreateDoWhileNode(
            Func<MonoBehaviour, IEnumerator> loopBody,
            Func<MonoBehaviour, bool>? condition = null)
        {
            EnsureIsBound();
            return owner.CreateDoWhileNode(loopBody, condition);
        }

        public Cursor CreateDoWhileNode(
            Func<IEnumerator> loopBody,
            Func<bool>? condition = null)
        {
            EnsureIsBound();
            return owner.CreateDoWhileNode(loopBody, condition);
        }

        public void SetNext(Cursor target)
        {
            EnsureIsBound();
            owner.SetNext(NodeId, target.NodeId);
        }

        public void SetBranches(Cursor trueTarget, Cursor falseTarget)
        {
            EnsureIsBound();
            owner.SetBranches(NodeId, trueTarget.NodeId, falseTarget.NodeId);
        }

        /// <summary>
        /// 删除节点并将所有指向它的连线修正为终点，防止残留引用。
        /// 被删除的节点的后继节点将悬空，因此在删除节点之前请确保提前处理后继节点，或者持有该节点的后继节点的引用，以便重新连接。
        /// </summary>
        public void Remove()
        {
            EnsureIsBound();
            owner.CutMapByNode(NodeId);
        }

        private INode GetNode()
        {
            return owner.GetNodeBase(NodeId);
        }

        private void EnsureIsBound()
        {
            if (owner == null)
            {
                throw new InvalidOperationException("Cursor 未绑定到 ActSeq 实例。");
            }
        }
    }
}
