using System;
using System.Collections.Generic;
using System.Numerics;
using Raylib_cs;
using Color = Raylib_cs.Color;

namespace CTC
{
    /// <summary>
    /// Phase 10: NPC buy/sell dialog.
    /// Corresponds to the otclientv8 <c>modules/game_shop</c> Lua module.
    ///
    /// Layout:
    ///   [Buy] [Sell] tab buttons
    ///   Scrollable item list (name, weight, price)
    ///   Amount input  / total price label
    ///   [OK] button — sends the purchase/sale request
    /// </summary>
    public class ShopPanel : UIFrame
    {
        private ClientViewport?  _viewport;
        private GameConnection?  _connection;

        // Tab state
        private bool _buyTab = true;

        // Widgets
        private UIButton       _buyTabBtn;
        private UIButton       _sellTabBtn;
        private UIVirtualFrame _offerList;
        private UITextbox      _amountBox;
        private UILabel        _totalLabel;
        private UIButton       _okButton;

        // Currently selected offer
        private ClientShopOffer? _selected;

        // -------------------------------------------------------------------------
        // Construction
        // -------------------------------------------------------------------------

        public ShopPanel()
        {
            Name = "Shop";

            Bounds.Width  = 340;
            Bounds.Height = 400;

            BuildUI();
        }

        // -------------------------------------------------------------------------
        // Public API
        // -------------------------------------------------------------------------

        /// <summary>Attach the panel to a viewport + connection and populate the list.</summary>
        public void SetViewport(ClientViewport viewport, GameConnection? connection = null)
        {
            if (_viewport != null)
            {
                _viewport.ShopOpened -= OnShopOpened;
                _viewport.ShopClosed -= OnShopClosed;
            }

            _viewport   = viewport;
            _connection = connection;

            if (_viewport != null)
            {
                _viewport.ShopOpened += OnShopOpened;
                _viewport.ShopClosed += OnShopClosed;
            }

            RefreshOfferList();
        }

        // -------------------------------------------------------------------------
        // Event handlers from ClientViewport
        // -------------------------------------------------------------------------

        private void OnShopOpened(ClientViewport _)  => RefreshOfferList();
        private void OnShopClosed(ClientViewport _)  => RefreshOfferList();

        // -------------------------------------------------------------------------
        // UI construction
        // -------------------------------------------------------------------------

        private void BuildUI()
        {
            // Remove default close / minimise frame buttons; the window is always shown
            // while the shop is open and hidden otherwise.
            // (Parent calls RemoveFromSuperview on ShopClosed.)

            // Tab buttons
            _buyTabBtn = new UIButton("Buy")
            {
                Bounds = new Rectangle(0, -SkinPadding.Top, 60, 16),
                ZOrder = 1
            };
            _buyTabBtn.ButtonReleasedInside += (_, __) => { _buyTab = true;  RefreshOfferList(); };
            AddFrameButton(_buyTabBtn);

            _sellTabBtn = new UIButton("Sell")
            {
                Bounds = new Rectangle(64, -SkinPadding.Top, 60, 16),
                ZOrder = 1
            };
            _sellTabBtn.ButtonReleasedInside += (_, __) => { _buyTab = false; RefreshOfferList(); };
            AddFrameButton(_sellTabBtn);

            // Offer list
            _offerList = new UIVirtualFrame();
            _offerList.ElementType = UIElementType.None;
            _offerList.Bounds = new Rectangle(0, 0, 320, 280);
            ContentView.AddSubview(_offerList);

            // Amount row
            UIView amountRow = new UIView { Bounds = new Rectangle(0, 0, 320, 24) };

            UILabel amountLabel = new UILabel("Amount:")
            {
                Bounds = new Rectangle(0, 4, 70, 18)
            };
            amountRow.AddSubview(amountLabel);

            _amountBox = new UITextbox("1")
            {
                Bounds = new Rectangle(72, 2, 60, 20),
                MaxLength = 5
            };
            _amountBox.TextChanged += _ => UpdateTotal();
            amountRow.AddSubview(_amountBox);

            _totalLabel = new UILabel("Total: 0 gp")
            {
                Bounds = new Rectangle(138, 4, 180, 18),
                TextAlignment = UITextAlignment.Right
            };
            amountRow.AddSubview(_totalLabel);

            ContentView.AddSubview(amountRow);

            // OK button
            _okButton = new UIButton("OK")
            {
                Bounds = new Rectangle(0, 0, 80, 24)
            };
            _okButton.ButtonReleasedInside += OnOkClicked;
            ContentView.AddSubview(_okButton);
        }

        // -------------------------------------------------------------------------
        // List refresh
        // -------------------------------------------------------------------------

        private void RefreshOfferList()
        {
            _offerList.ContentView.RemoveAllSubviews();
            _selected = null;
            UpdateTotal();

            if (_viewport == null)
                return;

            List<ClientShopOffer> offers = _viewport.ShopOffers;

            foreach (ClientShopOffer offer in offers)
            {
                ShopRow row = new ShopRow(offer, _buyTab);
                row.Bounds.Width  = _offerList.ClientBounds.Width;
                row.Bounds.Height = 22;
                ClientShopOffer captured = offer;
                row.ButtonReleasedInside += (_, __) =>
                {
                    _selected = captured;
                    HighlightSelected(row);
                    UpdateTotal();
                };
                _offerList.ContentView.AddSubview(row);
            }

            NeedsLayout = true;
        }

        private void HighlightSelected(ShopRow selected)
        {
            foreach (ShopRow row in _offerList.ContentView.SubviewsOfType<ShopRow>())
                row.IsSelected = (row == selected);
        }

        // -------------------------------------------------------------------------
        // Amount / total calculation
        // -------------------------------------------------------------------------

        /// <summary>
        /// Parses the amount textbox into a positive integer.
        /// Returns false if the text is empty, non-numeric, or ≤ 0.
        /// </summary>
        private bool TryGetValidQuantity(out int qty)
        {
            return int.TryParse(_amountBox.Text.Trim(), out qty) && qty > 0;
        }

        private void UpdateTotal()
        {
            if (_selected == null || !TryGetValidQuantity(out int qty))
            {
                _totalLabel.Text = "Total: — gp";
                return;
            }

            int price       = _buyTab ? _selected.BuyPrice : _selected.SellPrice;
            int total       = price * qty;
            bool canAfford  = !_buyTab || (_viewport != null && total <= _viewport.ShopPlayerMoney);

            _totalLabel.Text      = $"Total: {total:N0} gp";
            _totalLabel.TextColor = canAfford ? Color.Lime : Color.Red;
        }

        // -------------------------------------------------------------------------
        // OK button — send buy or sell request
        // -------------------------------------------------------------------------

        private void OnOkClicked(UIButton _, MouseState __)
        {
            if (_selected == null || _connection == null)
                return;

            if (!TryGetValidQuantity(out int qty))
                return;

            // NOTE: Tibia 8.6 buy/sell packets (0xCA/0xCB) are not yet fully implemented
            // in GameConnection.  This call-site is ready for when they are.
            // _connection.BuyItemAsync(_selected.ItemId, (byte)_selected.SubType, qty, false);
        }

        // -------------------------------------------------------------------------
        // Inner widget: one shop offer row
        // -------------------------------------------------------------------------

        private sealed class ShopRow : UIButton
        {
            public bool IsSelected = false;
            private readonly ClientShopOffer _offer;
            private readonly bool            _buyMode;

            public ShopRow(ClientShopOffer offer, bool buyMode)
            {
                _offer   = offer;
                _buyMode = buyMode;
                NormalType    = UIElementType.None;
                HighlightType = UIElementType.ButtonHighlight;
            }

            protected override void DrawContent()
            {
                Color bg = IsSelected
                    ? new Color(60, 100, 140, 200)
                    : new Color(0, 0, 0, 0);
                Raylib.DrawRectangle(ScreenBounds.X, ScreenBounds.Y,
                                     ScreenBounds.Width, ScreenBounds.Height, bg);

                // Name
                Raylib.DrawTextEx(UIContext.StandardFont, _offer.Name,
                    ScreenCoordinate(4, 4), UIContext.StandardFontSize, 1f, Color.White);

                // Price (right-aligned)
                int price     = _buyMode ? _offer.BuyPrice : _offer.SellPrice;
                string priceText = string.Format("{0:N0} gp", price);
                Vector2 priceSize = Raylib.MeasureTextEx(UIContext.StandardFont, priceText,
                    UIContext.StandardFontSize, 1f);
                Raylib.DrawTextEx(UIContext.StandardFont, priceText,
                    ScreenCoordinate((int)(ClientBounds.Width - priceSize.X - 4), 4),
                    UIContext.StandardFontSize, 1f, Color.Gold);
            }
        }
    }
}
