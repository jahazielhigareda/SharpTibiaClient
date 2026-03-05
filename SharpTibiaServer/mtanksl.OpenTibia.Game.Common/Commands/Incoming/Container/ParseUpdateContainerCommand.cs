using OpenTibia.Common.Objects;
using OpenTibia.Game.Common;
using OpenTibia.Network.Packets.Outgoing;
using System.Linq;

namespace OpenTibia.Game.Commands
{
    public class ParseUpdateContainerCommand : IncomingCommand
    {
        public ParseUpdateContainerCommand(Player player, byte containerId)
        {
            Player = player;

            ContainerId = containerId;
        }

        public Player Player { get; set; }

        public byte ContainerId { get; set; }

        public override Promise Execute()
        {
            Container container = Player.Client.Containers.GetContainer(ContainerId);

            if (container != null)
            {
                //TODO: FeatureFlag.ContainerPagination

                Context.AddPacket(Player, new OpenContainerOutgoingPacket(ContainerId, container, container.Metadata.Name, container.Metadata.Capacity.Value, container.Parent is Container, true, false, 0, container.GetItems().ToList() ) );

                return Promise.Completed;
            }

            return Promise.Break;
        }
    }
}