using OpenTibia.Common.Structures;
using OpenTibia.Game.Commands;
using OpenTibia.Game.Common;
using OpenTibia.Game.Common.ServerObjects;
using OpenTibia.Network.Packets.Outgoing;
using System;
using System.Collections.Generic;

namespace OpenTibia.Game.CommandHandlers
{
    public class SetMotdGuildHandler : CommandHandler<PlayerSayCommand>
    {
        public override Promise Handle(Func<Promise> next, PlayerSayCommand command)
        {
            if (command.Message.StartsWith("!setmotdguild ") )
            {
                List<string> parameters = command.Parameters(14);

                if (parameters.Count == 1)
                {
                    string messageOfTheDay = parameters[0];

                    if ( !string.IsNullOrEmpty(messageOfTheDay) && messageOfTheDay.Length < 255)
                    {
                        Guild guild = Context.Server.Guilds.GetGuildByLeader(command.Player);

                        if (guild != null)
                        {
                            guild.MessageOfTheDay = messageOfTheDay;

                            Context.AddPacket(command.Player, new ShowWindowTextOutgoingPacket(MessageMode.Look, "Message of the day has been set.") );

                            return Promise.Completed;
                        }

                        return Context.AddCommand(new ShowMagicEffectCommand(command.Player, MagicEffectType.Puff) );
                    }
                }
            }

            return next();
        }
    }
}