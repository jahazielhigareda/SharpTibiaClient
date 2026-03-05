using OpenTibia.Common.Objects;
using OpenTibia.Common.Structures;
using OpenTibia.Game.Commands;
using OpenTibia.Game.Common;
using System;
using System.Collections.Generic;

namespace OpenTibia.Game.CommandHandlers
{
    public class CreateNpcHandler : CommandHandler<PlayerSayCommand>
    {
        public override Promise Handle(Func<Promise> next, PlayerSayCommand command)
        {
            if (command.Message.StartsWith("/n ") )
            {
                List<string> parameters = command.Parameters(3);

                if (parameters.Count == 1)
                {
                    string name = parameters[0];

                    Tile toTile = Context.Server.Map.GetTile(command.Player.Tile.Position.Offset(command.Player.Direction) );

                    if (toTile != null)
                    {
                        return Context.AddCommand(new TileCreateNpcCommand(toTile, name) ).Then( (npc) =>
                        {
                            if (npc != null)
                            {
                                return Context.AddCommand(new ShowMagicEffectCommand(toTile.Position, MagicEffectType.BlueShimmer) );
                            }

                            return Context.AddCommand(new ShowMagicEffectCommand(command.Player, MagicEffectType.Puff) );
                        } );
                    }
                }

                return Context.AddCommand(new ShowMagicEffectCommand(command.Player, MagicEffectType.Puff) );
            }

            return next();
        }
    }
}