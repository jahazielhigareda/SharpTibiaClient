// Phase 7: DebugWindow (WinForms) will be removed and replaced with an
// in-game Raylib debug overlay. The stub below keeps compilation green
// while System.Windows.Forms is not referenced.
using System;

namespace CTC
{
    /// <summary>
    /// Debug log window stub. The real WinForms implementation is replaced
    /// in Phase 7 with a Raylib-based in-game debug panel.
    /// </summary>
    public partial class DebugWindow
    {
        public DebugWindow()
        {
            Log.Instance.OnLogMessage += OnLogMessage;
        }

        ~DebugWindow()
        {
            Log.Instance.OnLogMessage -= OnLogMessage;
        }

        public void Show() { }

        private void OnLogMessage(object sender, Log.Message message) { }
    }
}
