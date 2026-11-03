using System;
using System.Collections.Generic;

namespace GreatVoyage.Domain
{
    public sealed class PauseReasons : IDisposable
    {
        private readonly HashSet<int> tokens = new HashSet<int>();
        private int nextId;
        private bool disposed;
        public bool IsPaused => tokens.Count != 0;
        public int Count => tokens.Count;
        public event Action<bool> Changed;

        public IDisposable Acquire(string reason)
        {
            if (disposed) throw new ObjectDisposedException(nameof(PauseReasons));
            if (string.IsNullOrWhiteSpace(reason)) throw new ArgumentException("Pause reason is required.");
            var id = ++nextId;
            var wasPaused = IsPaused;
            tokens.Add(id);
            if (!wasPaused) Changed?.Invoke(true);
            return new Token(this, id);
        }

        private void Release(int id)
        {
            if (disposed || !tokens.Remove(id)) return;
            if (!IsPaused) Changed?.Invoke(false);
        }

        public void Dispose()
        {
            if (disposed) return;
            disposed = true;
            tokens.Clear();
            Changed = null;
        }

        private sealed class Token : IDisposable
        {
            private PauseReasons owner;
            private readonly int id;
            public Token(PauseReasons owner, int id) { this.owner = owner; this.id = id; }
            public void Dispose()
            {
                var previous = owner;
                owner = null;
                previous?.Release(id);
            }
        }
    }
}
