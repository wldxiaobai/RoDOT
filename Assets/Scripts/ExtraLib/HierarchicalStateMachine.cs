using System;
using System.Collections.Generic;
using UnityEngine;

public interface IState
{
    string Name { get; }
    void Enter();
    void Stay();
    void Exit();
}

public sealed class SimpleState : IState
{
    public string Name { get; }

    private readonly Action _enter;
    private readonly Action _stay;
    private readonly Action _exit;

    public SimpleState(string name, Action enter = null, Action stay = null, Action exit = null)
    {
        Name = name;
        _enter = enter ?? (() => { });
        _stay = stay ?? (() => { });
        _exit = exit ?? (() => { });
    }

    public void Enter() => _enter();
    public void Stay() => _stay();
    public void Exit() => _exit();
}

public class HierarchicalStateMachine : IState
{
    private readonly Dictionary<string, IState> _states = new();
    private readonly List<Transition> _transitions = new();
    private IState _currentState;
    private string _initialStateName;

    public string Name { get; }
    public string CurrentStateName => _currentState?.Name ?? "";

    public HierarchicalStateMachine(string name)
    {
        Name = name;
    }

    public HierarchicalStateMachine RegisterState(IState state, bool isInitial = false)
    {
        if (state == null)
            throw new ArgumentNullException(nameof(state));

        _states[state.Name] = state;

        if (isInitial || _initialStateName == null)
            _initialStateName = state.Name;

        return this;
    }

    public HierarchicalStateMachine RegisterTransition(string trigger, string fromStateName, string toStateName)
    {
        if (string.IsNullOrWhiteSpace(trigger))
            throw new ArgumentException("Trigger cannot be null or whitespace.", nameof(trigger));
        if (string.IsNullOrWhiteSpace(toStateName))
            throw new ArgumentException("Target state name is required.", nameof(toStateName));

        _transitions.Add(new Transition(trigger, fromStateName, toStateName));
        return this;
    }

    public void Enter()
    {
        if (_currentState != null)
            return;

        if (!string.IsNullOrEmpty(_initialStateName) && _states.TryGetValue(_initialStateName, out var initial))
            SwitchState(initial);
    }

    public void Stay()
    {
        _currentState?.Stay();
    }

    public void Exit()
    {
        _currentState?.Exit();
        _currentState = null;
    }

    private Transition? FindMatchedTransition(string trigger)
    {
        foreach (var transition in _transitions)
        {
            if (transition.Trigger == trigger &&
                (transition.FromState == null || transition.FromState == _currentState?.Name))
            {
                return transition;
            }
        }

        return null;
    }

    public bool Trigger(string transition)
    {
        if (string.IsNullOrWhiteSpace(transition))
            return false;

        var match = FindMatchedTransition(transition);
        if (match == null)
            return false;

        if (!_states.TryGetValue(match.Value.ToState, out var next))
        {
            Debug.LogWarning($"[{Name}] State '{match.Value.ToState}' not registered.");
            return false;
        }

        SwitchState(next);
        return true;
    }

    public bool TransitionTo(string stateName)
    {
        if (string.IsNullOrWhiteSpace(stateName))
            return false;

        if (!_states.TryGetValue(stateName, out var next))
            return false;

        SwitchState(next);
        return true;
    }

    private void SwitchState(IState nextState)
    {
        if (nextState == null || _currentState == nextState)
            return;

        _currentState?.Exit();
        _currentState = nextState;
        _currentState.Enter();
    }

    private readonly struct Transition
    {
        public Transition(string trigger, string fromState, string toState)
        {
            Trigger = trigger;
            FromState = fromState;
            ToState = toState;
        }

        public string Trigger { get; }
        public string FromState { get; }
        public string ToState { get; }
    }
}