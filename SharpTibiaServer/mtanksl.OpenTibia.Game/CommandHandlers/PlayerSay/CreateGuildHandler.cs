using OpenTibia.Common.Structures;
using OpenTibia.Game.Commands;
using OpenTibia.Game.Common;
using OpenTibia.Game.Common.ServerObjects;
using OpenTibia.Network.Packets.Outgoing;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace OpenTibia.Game.CommandHandlers
{
    public class CreateGuildHandler : CommandHandler<PlayerSayCommand>
    {
        public override Promise Handle(Func<Promise> next, PlayerSayCommand command)
        {
            if (command.Message.StartsWith("!createguild ") )
            {
                List<string> parameters = command.Parameters(13);

                if (parameters.Count == 1)
                {
                    string guildName = parameters[0];

                    if (guildName.Length >= 3 && guildName.Length <= 29 && Regex.IsMatch(guildName, "^[a-zA-Z]+(?:[ '][a-zA-Z]+)*$") )
                    {
                        Guild guild = Context.Server.Guilds.GetGuildByName(guildName);

                        if (guild == null)
                        {
                            guild = Context.Server.Guilds.GetGuildThatContainsMember(command.Player);

                            if (guild == null)
                            {
                                guild = new Guild()
                                {
                                    Name = guildName,

                                    Leader = command.Player.DatabasePlayerId
                                };

                                guild.AddMember(command.Player, "Leader");

                                Context.Server.Guilds.AddGuild(guild);

                                Context.AddPacket(command.Player, new ShowWindowTextOutgoingPacket(MessageMode.Look, "Guild " + guild.Name + " has been created. Invite players with !inviteguild <player_name> <rank_name> command. Disband with !leaveguild command.") );

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