using System;

namespace OpenTibia.Game.Common
{
    public class PromiseCanceledException : Exception
    {
        public static readonly PromiseCanceledException Instance = new PromiseCanceledException();

        private PromiseCanceledException()
        { 
        
        }
    }
}