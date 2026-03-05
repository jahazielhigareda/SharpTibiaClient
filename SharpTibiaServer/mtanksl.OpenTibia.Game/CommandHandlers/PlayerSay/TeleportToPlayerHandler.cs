using OpenTibia.Common.Objects;
using OpenTibia.Common.Structures;
using OpenTibia.Game.Commands;
using OpenTibia.Game.Common;
using System;
using System.Collections.Generic;

namespace OpenTibia.Game.CommandHandlers
{
    public class TeleportToPlayerHandler : CommandHandler<PlayerSayCommand>
    {
        public override Promise Handle(Func<Promise> next, PlayerSayCommand command)
        {
            if (command.Message.StartsWith("/goto ") )
            {
                List<string> parameters = command.Parameters(6);

                if (parameters.Count == 1)
                {
                    string name = parameters[0];

                    Player observer = Context.Server.GameObjects.GetPlayerByName(name);

                    if (observer != null && observer != command.Player)
                    {
                        Tile toTile = observer.Tile;

                        if (toTile != null)
                        {
                            Tile fromTile = command.Player.Tile;

                            return Context.AddCommand(new CreatureMoveCommand(command.Player, toTile) ).Then( () =>
                            {
                                return Context.AddCommand(new ShowMagicEffectCommand(fromTile.Position, MagicEffectType.Puff) );

                            } ).Then( () =>
                            {
                                return Context.AddCommand(new ShowMagicEffectCommand(toTile.Position, MagicEffectType.Teleport) );
                            } );
                        }
                    }
                }

                return Context.AddCommand(new ShowMagicEffectCommand(command.Player, MagicEffectType.Puff) );
            }

            return next();
        }
    }
}