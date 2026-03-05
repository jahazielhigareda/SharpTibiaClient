using OpenTibia.Common.Objects;
using OpenTibia.Common.Structures;
using OpenTibia.Game.Commands;
using OpenTibia.Game.Common;
using OpenTibia.Game.Common.ServerObjects;
using OpenTibia.Network.Packets.Outgoing;
using System;
using System.Collections.Generic;

namespace OpenTibia.Game.CommandHandlers
{
    public class ExcludeGuildHandler : CommandHandler<PlayerSayCommand>
    {
        public override Promise Handle(Func<Promise> next, PlayerSayCommand command)
        {
            if (command.Message.StartsWith("!excludeguild ") )
            {
                List<string> parameters = command.Parameters(14);

                if (parameters.Count == 1)
                {
                    string playerName = parameters[0];

                    Player observer = Context.Server.GameObjects.GetPlayerByName(playerName);

                    if (observer != null && observer != command.Player)
                    {
                        Guild guild = Context.Server.Guilds.GetGuildByLeader(command.Player);

                        if (guild != null)
                        {
                            if (guild.ContainsInvitation(observer, out _) )
                            {
                                guild.RemoveInvitation(observer);

                                Context.AddPacket(command.Player, new ShowWindowTextOutgoingPacket(MessageMode.Look, observer.Name + " has been excluded.") );

                                return Promise.Completed;
                            }
                            else if (guild.ContainsMember(observer, out _) )
                            {
                                guild.RemoveMember(observer);

                                Context.AddPacket(command.Player, new ShowWindowTextOutgoingPacket(MessageMode.Look, observer.Name + " has been excluded.") );

                                Context.AddPacket(observer, new ShowWindowTextOutgoingPacket(MessageMode.Look, "You have been excluded from the guild.") );

                                return Promise.Completed;
                            }
                        }
                    }
                }

                return Context.AddCommand(new ShowMagicEffectCommand(command.Player, MagicEffectType.Puff) );
            }

            return next();
        }
    }
}