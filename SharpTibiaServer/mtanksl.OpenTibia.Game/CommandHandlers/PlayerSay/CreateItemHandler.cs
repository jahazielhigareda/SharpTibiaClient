using OpenTibia.Common.Objects;
using OpenTibia.Common.Structures;
using OpenTibia.Game.Commands;
using OpenTibia.Game.Common;
using System;
using System.Collections.Generic;

namespace OpenTibia.Game.CommandHandlers
{
    public class CreateItemHandler : CommandHandler<PlayerSayCommand>
    {
        public override Promise Handle(Func<Promise> next, PlayerSayCommand command)
        {
            if (command.Message.StartsWith("/i ") )
            {
                List<string> parameters = command.Parameters(3);

                if (parameters.Count == 1)
                {
                    ushort toOpenTibiaId;

                    if ( !ushort.TryParse(parameters[0], out toOpenTibiaId) )
                    {
                        ItemMetadata itemMetadata = Context.Server.ItemFactory.GetItemMetadataByName(parameters[0] );

                        if (itemMetadata != null)
                        {
                            toOpenTibiaId = itemMetadata.OpenTibiaId;
                        }
                    }

                    if (toOpenTibiaId > 0)
                    {
                        Tile toTile = Context.Server.Map.GetTile(command.Player.Tile.Position.Offset(command.Player.Direction) );

                        if (toTile != null)
                        {
                            return Context.AddCommand(new TileCreateItemOrIncrementCommand(toTile, toOpenTibiaId, 1) ).Then( () =>
                            {
                                return Context.AddCommand(new ShowMagicEffectCommand(toTile.Position, MagicEffectType.BlueShimmer) );
                            } );
                        }
                    }
                }
                else if (parameters.Count == 2)
                {
                    ushort toOpenTibiaId;

                    if ( !ushort.TryParse(parameters[0], out toOpenTibiaId) )
                    {
                        ItemMetadata itemMetadata = Context.Server.ItemFactory.GetItemMetadataByName(parameters[0] );

                        if (itemMetadata != null)
                        {
                            toOpenTibiaId = itemMetadata.OpenTibiaId;
                        }
                    }

                    if (toOpenTibiaId > 0)
                    {
                        byte count;

                        if (byte.TryParse(parameters[1], out count) && count >= 1 && count <= 100)
                        {
                            Tile toTile = Context.Server.Map.GetTile(command.Player.Tile.Position.Offset(command.Player.Direction) );

                            if (toTile != null)
                            {
                                return Context.AddCommand(new TileCreateItemOrIncrementCommand(toTile, toOpenTibiaId, count) ).Then( () =>
                                {
                                    return Context.AddCommand(new ShowMagicEffectCommand(toTile.Position, MagicEffectType.BlueShimmer) );
                                } );
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