using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Numerics;
using Raylib_cs;
using Color = Raylib_cs.Color;

namespace CTC
{
    public class ChatPanel : UITabFrame
    {
        UIVirtualFrame ChatLog;

        public ChatPanel()
        {
            AddTab("Default");
            AddTab("Game-Chat");
            AddTab("Hemmd");

            ChatLog = new UIVirtualFrame();
            ChatLog.ElementType = UIElementType.None;
            ChatLog.ContentView.ElementType = UIElementType.Window;
            AddSubview(ChatLog);
        }

        #region Data Members

        ClientViewport Viewport;

        #endregion

        public void OnNewState(ClientViewport NewViewport)
        {
            Viewport = NewViewport;
        }

        public override void LayoutSubviews()
        {
            ChatLog.Bounds = ClientBounds;

            base.LayoutSubviews();
        }
    }
}
