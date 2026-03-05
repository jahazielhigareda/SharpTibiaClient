using OpenTibia.Game.Common;
using System;
using System.Diagnostics;

namespace OpenTibia.Game.EventHandlers
{
    public class InlineEventHandler : EventHandler
    {
        private Func<Context, object, Promise> execute;

        public InlineEventHandler(Func<Context, object, Promise> execute)
        {
            this.execute = execute;
        }

        [DebuggerStepThrough]
        public override Promise Handle(object e)
        {
            return execute(Context, e);
        }
    }
}