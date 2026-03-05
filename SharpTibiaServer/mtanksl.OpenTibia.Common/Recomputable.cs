using System;

namespace OpenTibia.Common.Objects
{
    public class Recomputable
    {
        private Action callback;

        public Recomputable(IRecomputableSource source, Action callback)
        {
            source.Changed += (sender, e) =>
            {
                recompute = true;
            };

            this.callback = callback;
        }

        private bool recompute = true;

        public bool IsValueCreated
        {
            get
            {
                return !recompute;
            }
        }

        public void EnsureUpdated()
        {
            if (recompute)
            {
                callback();

                recompute = false;
            }
        }
    }
}