using System;
using System.Collections.Generic;
using UnityEngine;

namespace Project.Core.StateMachine
{
    public abstract class State
    {
        public virtual void Enter() { }
        public virtual void Exit() { }
        public virtual void Tick(float deltaTime) { }
    }

    // Transitions requested inside Enter/Exit are queued so the order stays predictable.
    public sealed class StateMachine<TKey> where TKey : struct, Enum
    {
        private readonly Dictionary<TKey, State> _states = new();
        private State _current;
        private bool _transitioning;
        private TKey? _pending;

        public TKey? CurrentKey { get; private set; }

        public StateMachine<TKey> Add(TKey key, State state)
        {
            _states[key] = state;
            return this;
        }

        public void ChangeState(TKey key)
        {
            if (_transitioning)
            {
                _pending = key;
                return;
            }

            if (!_states.TryGetValue(key, out var next))
            {
                Debug.LogError($"[StateMachine] No state registered for {key}.");
                return;
            }

            _transitioning = true;
            _current?.Exit();
            _current = next;
            CurrentKey = key;
            _current.Enter();
            _transitioning = false;

            if (_pending is { } queued)
            {
                _pending = null;
                ChangeState(queued);
            }
        }

        public void Tick(float deltaTime) => _current?.Tick(deltaTime);
    }
}
