using OpenTibia.Common.Objects;
using OpenTibia.Common.Structures;
using OpenTibia.Data.Models;
using OpenTibia.Game.Common;
using OpenTibia.Game.Common.ServerObjects;
using OpenTibia.Network.Packets.Incoming;
using OpenTibia.Network.Packets.Outgoing;
using System;

namespace OpenTibia.Game.Commands
{
    public class ParseBanishmentsAndNamelocksCommand : IncomingCommand // = RuleViolationCommand
    {
        public ParseBanishmentsAndNamelocksCommand(Player player, BanishmentsAndNamelocksIncomingPacket packet)
        {
            Player = player;

            Packet = packet;
        }

        public Player Player { get; set; }

        public BanishmentsAndNamelocksIncomingPacket Packet { get; set; }

        public override async Promise Execute()
        {
            // ctrl + y

            if (Player.Rank == Rank.Gamemaster)
            {
                using (var database = Context.Server.DatabaseFactory.Create() )
                {
                    Statement statment = Context.Server.Channels.GetStatement(Packet.StatmentId);

                    database.RuleViolationRepository.AddRuleViolation(new DbRuleViolation()
                    {
                        PlayerId = Player.DatabasePlayerId,
                        Name = Packet.Name,
                        Reason = Packet.Reason,
                        Action = Packet.Action,
                        Comment = Packet.Comment,
                        StatmentPlayerId = statment?.DatabasePlayerId,
                        Statment = statment?.Message,
                        StatmentDate = statment?.CreationDate,
                        StatmentIPAddress = statment?.IPAddress ?? "",
                        IPAddressBanishment = Packet.IPAddressBanishment,
                        CreationDate = DateTime.UtcNow
                    } );

                    await database.Commit();
                }

                Context.AddPacket(Player, new ShowWindowTextOutgoingPacket(MessageMode.Look, "Your report has been sent.") );
            }

            await Promise.Break; return;
        }
    }
}