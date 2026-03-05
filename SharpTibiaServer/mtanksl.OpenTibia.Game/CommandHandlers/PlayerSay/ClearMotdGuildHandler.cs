using OpenTibia.Common.Structures;
using OpenTibia.Game.Commands;
using OpenTibia.Game.Common;
using OpenTibia.Game.Common.ServerObjects;
using OpenTibia.Network.Packets.Outgoing;
using System;

namespace OpenTibia.Game.CommandHandlers
{
    public class ClearMotdGuildHandler : CommandHandler<PlayerSayCommand>
    {
        public override Promise Handle(Func<Promise> next, PlayerSayCommand command)
        {
            if (command.Message.StartsWith("!clearmotdguild") )
            {
                Guild guild = Context.Server.Guilds.GetGuildByLeader(command.Player);

                if (guild != null)
                {
                    guild.MessageOfTheDay = null;

                    Context.AddPacket(command.Player, new ShowWindowTextOutgoingPacket(MessageMode.Look, "Message of the day has been cleared.") );

                    return Promise.Completed;
                }

                return Context.AddCommand(new ShowMagicEffectCommand(command.Player, MagicEffectType.Puff) );
            }

            return next();
        }
    }
}