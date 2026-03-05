using OpenTibia.Common.Objects;
using OpenTibia.Common.Structures;
using OpenTibia.Game.Commands;
using OpenTibia.Game.Common;
using OpenTibia.Network.Packets.Outgoing;
using System;
using System.Linq;

namespace OpenTibia.Game.CommandHandlers
{
    public class BuyHouseHandler : CommandHandler<PlayerSayCommand>
    {
        public override async Promise Handle(Func<Promise> next, PlayerSayCommand command)
        {         
            if (command.Message.StartsWith("!buyhouse") )
            {                        
                Tile toTile = Context.Server.Map.GetTile(command.Player.Tile.Position.Offset(command.Player.Direction) );

                if (toTile != null && toTile is HouseTile houseTile && houseTile.TopItem != null && houseTile.TopItem is DoorItem doorItem)
                {
                    if (houseTile.House.OwnerId == null)
                    {
                        if ( !Context.Server.Map.GetHouses().Any(h => h.IsOwner(command.Player.Name) ) )
                        {
                            bool success = await Context.AddCommand(new PlayerDestroyMoneyCommand(command.Player, (int)houseTile.House.Rent));
                        
                            if (success)
                            {
                                houseTile.House.OwnerId = command.Player.DatabasePlayerId;

                                houseTile.House.Owner = command.Player.Name;

                                Context.AddPacket(command.Player, new ShowWindowTextOutgoingPacket(MessageMode.Look, "You bought this house.") );
                            }
                            else
                            {
                                Context.AddPacket(command.Player, new ShowWindowTextOutgoingPacket(MessageMode.Failure, "You don't have enough money.") );

                                await Context.AddCommand(new ShowMagicEffectCommand(command.Player, MagicEffectType.Puff) );
                        
                                await Promise.Break;
                            }
                        }
                        else
                        {
                            Context.AddPacket(command.Player, new ShowWindowTextOutgoingPacket(MessageMode.Failure, "You already own a house.") );

                            await Context.AddCommand(new ShowMagicEffectCommand(command.Player, MagicEffectType.Puff) );

                            await Promise.Break;
                        }
                    }
                    else
                    {
                        Context.AddPacket(command.Player, new ShowWindowTextOutgoingPacket(MessageMode.Failure, "This house already has an owner.") );

                        await Context.AddCommand(new ShowMagicEffectCommand(command.Player, MagicEffectType.Puff) );

                        await Promise.Break;
                    }
                }
                else
                {
                    Context.AddPacket(command.Player, new ShowWindowTextOutgoingPacket(MessageMode.Failure, "You need to face a house door.") );

                    await Context.AddCommand(new ShowMagicEffectCommand(command.Player, MagicEffectType.Puff) );

                    await Promise.Break;
                }
            }
            else
            {
                await next();
            }
        }
    }
}