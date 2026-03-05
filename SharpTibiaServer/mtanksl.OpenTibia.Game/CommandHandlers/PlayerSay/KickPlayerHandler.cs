using OpenTibia.Common.Objects;
using OpenTibia.Common.Structures;
using OpenTibia.Game.Commands;
using OpenTibia.Game.Common;
using System;
using System.Collections.Generic;

namespace OpenTibia.Game.CommandHandlers
{
    public class KickPlayerHandler : CommandHandler<PlayerSayCommand>
    {
        public override Promise Handle(Func<Promise> next, PlayerSayCommand command)
        {
            if (command.Message.StartsWith("/kick ") )
            {
                List<string> parameters = command.Parameters(6);

                if (parameters.Count == 1)
                {
                    string name = parameters[0];

                    Player observer = Context.Server.GameObjects.GetPlayerByName(name);

                    if (observer != null && observer != command.Player)
                    {
                        return Context.AddCommand(new ShowMagicEffectCommand(observer, MagicEffectType.Puff) ).Then( () =>
                        {
                            return Context.AddCommand(new CreatureDestroyCommand(observer) );
                        } );
                    }
                }

                return Context.AddCommand(new ShowMagicEffectCommand(command.Player, MagicEffectType.Puff) );
            }

            return next();
        }
    }
}