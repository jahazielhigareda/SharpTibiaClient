namespace CTC
{
    /// <summary>
    /// Phase 10: Represents one item in an NPC shop offer list.
    /// Populated by the server packet that opens the shop window.
    /// </summary>
    public class ClientShopOffer
    {
        /// <summary>Server-side item ID.</summary>
        public int ItemId;

        /// <summary>Item sub-type / count (0 = N/A).</summary>
        public int SubType;

        /// <summary>Display name sent by the server.</summary>
        public string Name = "";

        /// <summary>Weight in oz.</summary>
        public double Weight;

        /// <summary>Price the NPC charges to sell this item to the player (buy price for player).</summary>
        public int BuyPrice;

        /// <summary>Price the NPC pays when the player sells this item (sell price for player).</summary>
        public int SellPrice;
    }
}
