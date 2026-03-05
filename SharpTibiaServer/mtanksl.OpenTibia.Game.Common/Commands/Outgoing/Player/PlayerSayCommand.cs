using OpenTibia.Common.Objects;
using OpenTibia.Common.Structures;
using OpenTibia.Game.Common;
using OpenTibia.Game.Components;
using OpenTibia.Game.Events;
using OpenTibia.Network.Packets.Outgoing;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace OpenTibia.Game.Commands
{
    public class PlayerSayCommand : Command
    {
        public PlayerSayCommand(Player player, string message)
        {
            Player = player;

            Message = message;
        }

        public Player Player { get; set; }

        public string Message { get; set; }

        private List<string> parameters;

        public List<string> Parameters(int skip)
        {
            if (parameters == null)
            {
                parameters = new List<string>();

                StringBuilder token = new StringBuilder();

                bool quoted = false;

                foreach (var character in Message.Skip(skip) )
                {
                    if (character == '\"')
                    {
                        quoted = !quoted;
                    }
                    else if (character == ' ' && !quoted)
                    {
                        if (token.Length > 0)
                        {
                            parameters.Add(token.ToString() );

                            token.Clear();
                        }
                    }
                    else
                    {
                        token.Append(character);
                    }
                }

                if (token.Length > 0)
                {
                    parameters.Add(token.ToString() );
                }
            }

            return parameters;
        }

        public override Promise Execute()
        {
            PlayerMuteBehaviour playerChannelMuteBehaviour = Context.Server.GameObjectComponents.GetComponent<PlayerMuteBehaviour>(Player);

            if (playerChannelMuteBehaviour != null)
            {
                string message;

                if (playerChannelMuteBehaviour.IsMuted(out message) )
                {
                    Context.AddPacket(Player, new ShowWindowTextOutgoingPacket(MessageMode.Failure, message) );

                    return Promise.Break;
                }      
            }

            ShowTextOutgoingPacket showTextOutgoingPacket = new ShowTextOutgoingPacket(Context.Server.Channels.GenerateStatementId(Player.DatabasePlayerId, Message, Player.Client.Connection.IpAddress), Player.Name, Player.Level, MessageMode.Say, Player.Tile.Position, Message);

            foreach (var observer in Context.Server.Map.GetObserversOfTypePlayer(Player.Tile.Position) )
            {
                if (observer.Tile.Position.CanHearSay(Player.Tile.Position) )
                {
                    Context.AddPacket(observer, showTextOutgoingPacket);                    
                }
            }

            PlayerSayEventArgs e = new PlayerSayEventArgs(Player, Message);

            foreach (var npc in Context.Server.Map.GetObserversOfTypeNpc(Player.Tile.Position) )
            {
                if (npc.Tile.Position.CanSee(Player.Tile.Position) )
                {
                    Context.AddEvent(npc, ObserveEventArgs.Create(npc, e) );
                }
            }
              
            Context.AddEvent(Player, e);

            return Promise.Completed;
        }
    }
}