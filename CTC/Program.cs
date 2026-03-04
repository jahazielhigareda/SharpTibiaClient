using System;

namespace CTC
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// Phase 3 will launch the Raylib window from here.
        /// </summary>
        static void Main(string[] args)
        {
            using (Game game = new Game())
            {
                game.Run();
            }
        }
    }
}

