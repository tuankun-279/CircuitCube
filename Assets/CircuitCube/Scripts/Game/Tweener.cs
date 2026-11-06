using System;
using System.Collections.Generic;
using UnityEngine;

namespace CircuitCube.Game
{
    /// <summary>Tiny tween runner driven by the app's Update, so nothing depends on coroutines or packages.</summary>
    public sealed class Tweener
    {
        sealed class Item { public float T, Dur; public Action<float> Step; public Action Done; public object Owner; }
        readonly List<Item> _items = new List<Item>();

        public static float Ease(float p) => p < 0.5f ? 2f * p * p : 1f - Mathf.Pow(-2f * p + 2f, 2f) * 0.5f;

        public void Run(float duration, Action<float> step, Action done = null, object owner = null)
        {
            if (owner != null) Cancel(owner);
            var it = new Item { Dur = Mathf.Max(0.0001f, duration), Step = step, Done = done, Owner = owner };
            _items.Add(it);
            step(0f);
        }

        public void Cancel(object owner)
        {
            for (int i = _items.Count - 1; i >= 0; i--)
                if (_items[i].Owner == owner) { _items[i].Step(1f); _items.RemoveAt(i); }
        }

        public void Clear() => _items.Clear();

        public void Tick(float dt)
        {
            for (int i = 0; i < _items.Count; i++)
            {
                var it = _items[i];
                it.T += dt;
                float p = Mathf.Clamp01(it.T / it.Dur);
                it.Step(Ease(p));
                if (p >= 1f)
                {
                    _items.RemoveAt(i--);
                    it.Done?.Invoke();
                }
            }
        }
    }
}
