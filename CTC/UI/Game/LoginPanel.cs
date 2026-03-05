using System;
using System.Collections.Generic;
using System.Numerics;
using System.Threading.Tasks;
using Raylib_cs;
using Color = Raylib_cs.Color;

namespace CTC
{
    /// <summary>
    /// Phase 10: Full-screen login panel presented before game connection.
    /// Corresponds to the otclientv8 <c>modules/client</c> Lua module.
    ///
    /// Layout (top to bottom):
    ///   Title label
    ///   Server host  / port   (two textboxes on one row)
    ///   Account name textbox
    ///   Password textbox
    ///   [Connect] button  — calls LoginConnection.ConnectAsync
    ///   Character list    — shown after a successful login handshake
    ///   [Enter Game] button — calls GameConnection and transitions to GameDesktop
    /// </summary>
    public class LoginPanel : UIView
    {
        // -------------------------------------------------------------------------
        // Widgets
        // -------------------------------------------------------------------------
        private UITextbox _hostBox      = null!;
        private UITextbox _portBox      = null!;
        private UITextbox _accountBox   = null!;
        private UITextbox _passwordBox  = null!;
        private UIButton  _connectButton = null!;
        private UIButton  _enterButton  = null!;
        private UILabel   _statusLabel  = null!;

        // Character list rows (recreated after successful login)
        private readonly List<CharacterRowButton> _charRows = new List<CharacterRowButton>();
        private int _selectedChar = -1;

        // Data from a successful login handshake
        private List<CharacterEntry>? _chars;
        private uint[]?               _xteaKey;

        // Fired when the player chooses a character and presses Enter Game.
        // Subscribers receive the selected CharacterEntry and the XTEA session key.
        public event Action<CharacterEntry, uint[]>? CharacterSelected;

        // -------------------------------------------------------------------------
        // Construction
        // -------------------------------------------------------------------------

        public LoginPanel()
            : base(null, UIElementType.Window)
        {
            Bounds = new Rectangle(0, 0, 340, 460);

            BuildForm();
        }

        private void BuildForm()
        {
            int y = 10;
            int labelW = 90;
            int fieldW = 220;
            int rowH   = 24;
            int gap    = 8;

            // Title
            UILabel title = new UILabel("SharpTibia Login")
            {
                Bounds = new Rectangle(0, y, Bounds.Width, 22),
                TextAlignment = UITextAlignment.Center,
                TextColor = Color.Gold
            };
            AddSubview(title);
            y += 28;

            // Server host + port row
            AddSubview(new UILabel("Server:")  { Bounds = new Rectangle(10, y, labelW, rowH) });
            _hostBox = new UITextbox("e.g. 127.0.0.1") { Bounds = new Rectangle(labelW + 14, y, 148, rowH) };
            AddSubview(_hostBox);
            _portBox = new UITextbox("7171") { Bounds = new Rectangle(labelW + 14 + 152, y, 60, rowH) };
            AddSubview(_portBox);
            y += rowH + gap;

            // Account
            AddSubview(new UILabel("Account:") { Bounds = new Rectangle(10, y, labelW, rowH) });
            _accountBox = new UITextbox("Account name") { Bounds = new Rectangle(labelW + 14, y, fieldW, rowH) };
            AddSubview(_accountBox);
            y += rowH + gap;

            // Password
            AddSubview(new UILabel("Password:") { Bounds = new Rectangle(10, y, labelW, rowH) });
            _passwordBox = new UITextbox("Password")
            {
                Bounds = new Rectangle(labelW + 14, y, fieldW, rowH),
                IsPassword = true
            };
            AddSubview(_passwordBox);
            y += rowH + gap + 4;

            // Connect button
            _connectButton = new UIButton("Connect")
            {
                Bounds = new Rectangle((Bounds.Width - 100) / 2, y, 100, 24)
            };
            _connectButton.ButtonReleasedInside += OnConnectClicked;
            AddSubview(_connectButton);
            y += 32;

            // Status label (errors / "Connecting…" feedback)
            _statusLabel = new UILabel("")
            {
                Bounds = new Rectangle(10, y, Bounds.Width - 20, 18),
                TextAlignment = UITextAlignment.Center,
                TextColor = Color.Red
            };
            AddSubview(_statusLabel);
            y += 24;

            // Character list area header
            AddSubview(new UILabel("Characters:")
            {
                Bounds = new Rectangle(10, y, Bounds.Width - 20, 18),
                TextColor = Color.LightGray
            });
            y += 20;

            // Placeholder rows (filled after successful connect)
            for (int i = 0; i < 5; i++)
            {
                var row = new CharacterRowButton(i)
                {
                    Bounds = new Rectangle(14, y, Bounds.Width - 28, 22),
                    Visible = false
                };
                int capturedI = i;
                row.ButtonReleasedInside += (_, __) => SelectCharacter(capturedI);
                AddSubview(row);
                _charRows.Add(row);
                y += 24;
            }

            // Enter Game button (hidden until a char is selected)
            y = Bounds.Height - 36;
            _enterButton = new UIButton("Enter Game")
            {
                Bounds = new Rectangle((Bounds.Width - 120) / 2, y, 120, 26),
                Visible = false
            };
            _enterButton.ButtonReleasedInside += OnEnterGameClicked;
            AddSubview(_enterButton);
        }

        // -------------------------------------------------------------------------
        // Event handlers
        // -------------------------------------------------------------------------

        private async void OnConnectClicked(UIButton btn, MouseState mouse)
        {
            string host    = _hostBox.Text.Trim();

            if (host.Length == 0) host = "127.0.0.1";

            string account  = _accountBox.Text.Trim();
            string password = _passwordBox.Text;

            if (account.Length == 0 || password.Length == 0)
            {
                SetStatus("Please enter account name and password.", Color.Red);
                return;
            }

            SetStatus("Connecting…", Color.Yellow);
            _connectButton.InteractionEnabled = false;

            try
            {
                var result = await LoginConnection.ConnectAsync(host, account, password);
                _xteaKey = result.conn.XteaKey;
                _chars   = result.chars;
                result.conn.Dispose();
                PopulateCharacterList();
                SetStatus("Select a character and click Enter Game.", Color.Lime);
            }
            catch (Exception ex)
            {
                SetStatus("Connection failed: " + ex.Message, Color.Red);
            }
            finally
            {
                _connectButton.InteractionEnabled = true;
            }
        }

        private void OnEnterGameClicked(UIButton btn, MouseState mouse)
        {
            if (_selectedChar < 0 || _chars == null || _selectedChar >= _chars.Count)
                return;

            CharacterEntry entry = _chars[_selectedChar];
            CharacterSelected?.Invoke(entry, _xteaKey!);
        }

        // -------------------------------------------------------------------------
        // Helpers
        // -------------------------------------------------------------------------

        private void SetStatus(string message, Color color)
        {
            _statusLabel.Text = message;
            _statusLabel.TextColor = color;
        }

        private void SelectCharacter(int index)
        {
            if (_chars == null || index >= _chars.Count) return;

            _selectedChar = index;

            // Highlight selected row
            for (int i = 0; i < _charRows.Count; i++)
                _charRows[i].IsSelected = (i == index);

            _enterButton.Visible = true;
        }

        private void PopulateCharacterList()
        {
            if (_chars == null) return;

            for (int i = 0; i < _charRows.Count; i++)
            {
                if (i < _chars.Count)
                {
                    _charRows[i].SetCharacter(_chars[i]);
                    _charRows[i].Visible = true;
                }
                else
                {
                    _charRows[i].Visible = false;
                }
            }
        }

        // -------------------------------------------------------------------------
        // Layout
        // -------------------------------------------------------------------------

        public override void LayoutSubviews()
        {
            // Center this panel in the game window
            Rectangle win = UIContext.GameWindowSize;
            Bounds.X = (win.Width  - Bounds.Width)  / 2;
            Bounds.Y = (win.Height - Bounds.Height) / 2;
            base.LayoutSubviews();
        }

        // -------------------------------------------------------------------------
        // Inner widget: character row button
        // -------------------------------------------------------------------------

        private sealed class CharacterRowButton : UIButton
        {
            public bool IsSelected = false;
            private int  _index;
            private string _name  = "";
            private string _world = "";

            public CharacterRowButton(int index)
            {
                _index = index;
                NormalType    = UIElementType.Button;
                HighlightType = UIElementType.ButtonHighlight;
            }

            public void SetCharacter(CharacterEntry entry)
            {
                _name  = entry.Name;
                _world = entry.World;
                Label  = "";   // we draw our own content
            }

            protected override void DrawContent()
            {
                Color bg = IsSelected
                    ? new Color(60, 100, 140, 200)
                    : new Color(30, 30, 50, 180);
                Raylib.DrawRectangle(ScreenBounds.X, ScreenBounds.Y,
                                     ScreenBounds.Width, ScreenBounds.Height, bg);

                Raylib.DrawTextEx(UIContext.StandardFont, _name,
                    ScreenCoordinate(4, 4), UIContext.StandardFontSize, 1f, Color.White);

                Vector2 worldSize = Raylib.MeasureTextEx(UIContext.StandardFont, _world,
                    UIContext.StandardFontSize, 1f);
                Raylib.DrawTextEx(UIContext.StandardFont, _world,
                    ScreenCoordinate((int)(ClientBounds.Width - worldSize.X - 4), 4),
                    UIContext.StandardFontSize, 1f, Color.LightGray);
            }
        }
    }
}
