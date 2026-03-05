using OpenTibia.Common.Structures;
using OpenTibia.Game.Commands;
using OpenTibia.Game.Common;
using OpenTibia.Game.Common.ServerObjects;
using OpenTibia.Network.Packets.Outgoing;
using System;

namespace OpenTibia.Game.CommandHandlers
{
    public class LeaveGuildHandler : CommandHandler<PlayerSayCommand>
    {
        public override Promise Handle(Func<Promise> next, PlayerSayCommand command)
        {
            if (command.Message.StartsWith("!leaveguild") )
            {
                Guild guild = Context.Server.Guilds.GetGuildThatContainsMember(command.Player);

                if (guild != null)
                {
                    if (guild.IsLeader(command.Player) )
                    {
                        Context.AddPacket(command.Player, new ShowWindowTextOutgoingPacket(MessageMode.Look, "The guild has been disbanded.") );

                        Context.Server.Guilds.RemoveGuild(guild);
                    }
                    else
                    {
                        guild.RemoveMember(command.Player);

                        Context.AddPacket(command.Player, new ShowWindowTextOutgoingPacket(MessageMode.Look, "You have left the guild.") );

                        return Promise.Completed;
                    }
                }

                return Context.AddCommand(new ShowMagicEffectCommand(command.Player, MagicEffectType.Puff) );
            }

            return next();
        }
    }
}