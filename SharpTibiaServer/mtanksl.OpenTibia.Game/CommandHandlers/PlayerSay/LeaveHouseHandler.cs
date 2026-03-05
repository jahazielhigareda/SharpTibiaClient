using OpenTibia.Common.Objects;
using OpenTibia.Common.Structures;
using OpenTibia.Game.Commands;
using OpenTibia.Game.Common;
using OpenTibia.Network.Packets.Outgoing;
using System;

namespace OpenTibia.Game.CommandHandlers
{
    public class LeaveHouseHandler : CommandHandler<PlayerSayCommand>
    {
        public override async Promise Handle(Func<Promise> next, PlayerSayCommand command)
        {         
            if (command.Message.StartsWith("!leavehouse") )
            {
                Tile fromTile = command.Player.Tile;

                if (fromTile is HouseTile houseTile && houseTile.House.IsOwner(command.Player.Name) )
                {
                    bool hasItems = false;

                    foreach (var tile in houseTile.House.GetTiles() )
                    {
                        foreach (var item in tile.GetItems() )
                        {
                            if ( !item.Metadata.Flags.Is(ItemMetadataFlags.NotMoveable) )
                            {
                                hasItems = true;

                                break;
                            }
                        }

                        if (hasItems)
                        {
                            break;
                        }
                    }

                    if ( !hasItems)
                    {
                        Tile toTile = Context.Server.Map.GetTile(houseTile.House.Entry);

                        if (toTile != null)
                        {
                            houseTile.House.OwnerId = null;

                            houseTile.House.Owner = null;

                            Context.AddPacket(command.Player, new ShowWindowTextOutgoingPacket(MessageMode.Look, "You left the house.") );

                            await Context.AddCommand(new CreatureMoveCommand(command.Player, toTile) );

                            await Context.AddCommand(new ShowMagicEffectCommand(fromTile.Position, MagicEffectType.Puff) );

                            await Context.AddCommand(new ShowMagicEffectCommand(toTile.Position, MagicEffectType.Teleport) );
                        }
                        else
                        {
                            await Context.AddCommand(new ShowMagicEffectCommand(command.Player, MagicEffectType.Puff) );

                            await Promise.Break;
                        }
                    }
                    else
                    {
                        Context.AddPacket(command.Player, new ShowWindowTextOutgoingPacket(MessageMode.Failure, "There are still items in your house.") );

                        await Context.AddCommand(new ShowMagicEffectCommand(command.Player, MagicEffectType.Puff) );

                        await Promise.Break;
                    }
                }
                else
                {
                    Context.AddPacket(command.Player, new ShowWindowTextOutgoingPacket(MessageMode.Failure, "You are not in your house.") );

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