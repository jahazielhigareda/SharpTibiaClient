using OpenTibia.Common.Objects;
using OpenTibia.Common.Structures;
using OpenTibia.Game.Common;

namespace OpenTibia.Game.Components
{
    public class RandomWalkStrategy : IWalkStrategy
    {
        public static readonly RandomWalkStrategy Instance = new RandomWalkStrategy();

        private RandomWalkStrategy()
        {
            
        }

        public bool CanWalk(Creature attacker, Creature target, out Tile tile)
        {
            Direction[] randomDirections = new Direction[] { Direction.North, Direction.East, Direction.South, Direction.West };

            foreach (var direction in Context.Current.Server.Randomization.Shuffle(randomDirections) )
            {
                Tile toTile = Context.Current.Server.Map.GetTile(attacker.Tile.Position.Offset(direction) );

                if (toTile == null || toTile.Ground == null || toTile.NotWalkable || toTile.BlockPathFinding || toTile.Block || (attacker is Monster && toTile.ProtectionZone) )
                {

                }
                else
                {
                    tile = toTile;

                    return true;
                }
            }

            tile = null;

            return false;
        }
    }
}