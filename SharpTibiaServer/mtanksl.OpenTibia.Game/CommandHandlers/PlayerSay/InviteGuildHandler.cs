using OpenTibia.Common.Objects;
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
    public class InviteGuildHandler : CommandHandler<PlayerSayCommand>
    {
        public override Promise Handle(Func<Promise> next, PlayerSayCommand command)
        {
            if (command.Message.StartsWith("!inviteguild ") )
            {
                List<string> parameters = command.Parameters(13);

                if (parameters.Count == 2)
                {
                    string playerName = parameters[0];

                    Player observer = Context.Server.GameObjects.GetPlayerByName(playerName);

                    if (observer != null && observer != command.Player)
                    {
                        string rankName = parameters[1];

                        if (rankName.Length >= 3 && rankName.Length <= 29 && !string.Equals(rankName, "Leader", StringComparison.OrdinalIgnoreCase) && Regex.IsMatch(rankName, "^[a-zA-Z]+(?:[ '][a-zA-Z]+)*$") )
                        {
                            Guild guild = Context.Server.Guilds.GetGuildByLeader(command.Player);

                            if (guild != null)
                            {
                                if ( !guild.ContainsMember(observer, out _) && !guild.ContainsInvitation(observer, out _) )
                                {
                                    guild.AddInvitation(observer, rankName);

                                    Context.AddPacket(command.Player, new ShowWindowTextOutgoingPacket(MessageMode.Look, observer.Name + " has been invited. Exclude players with !excludeguild <player_name> command.") );

                                    Context.AddPacket(observer, new ShowWindowTextOutgoingPacket(MessageMode.Look, command.Player.Name + " has invited you to " + (command.Player.Gender == Gender.Male ? "his" : "her") + " guild. Join with !joinguild \"" + guild.Name + "\" command.") );
                                
                                    return Promise.Completed;
                                }
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