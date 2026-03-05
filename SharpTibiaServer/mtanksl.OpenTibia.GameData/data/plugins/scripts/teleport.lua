local checkpointposition = nil

-- /cp - Set a checkpoint

registertalkactionsplayersay("/cp", function(player, message)
	if cast("System.Int64", player.Rank) == rank.gamemaster then
		checkpointposition = player.Tile.Position
		command.showmagiceffect(player, magiceffecttype.blueshimmer)
		return true -- handled, stop process
	end
	return false
end)

-- /gc [player_name] - Return to a checkpoint

registertalkactionsplayersay("/gc", function(player, message)
	if cast("System.Int64", player.Rank) == rank.gamemaster then
		if checkpointposition then
			local space = string.find(message, " ")
			if space then
				local observer = command.gameobjectsgetplayerbyname(string.sub(message, space + 1))			
				if observer then
					local tile = command.mapgettile(checkpointposition)
					command.showmagiceffect(observer, magiceffecttype.puff)
					command.creaturemove(observer, tile)
					command.showmagiceffect(observer, magiceffecttype.teleport)
				else
					command.showmagiceffect(player, magiceffecttype.puff)
				end
			else
				local tile = command.mapgettile(checkpointposition)
				command.showmagiceffect(player, magiceffecttype.puff)
				command.creaturemove(player, tile)
				command.showmagiceffect(player, magiceffecttype.teleport)
			end
		else
			command.showwindowtext(player, messagemode.failure, "Use '/cp' to set a checkpoint, then '/gc [player_name]' to return to a checkpoint.")
			command.showmagiceffect(player, magiceffecttype.puff)
		end
		return true -- handled, stop process
	end
	return false
end)

-- /tp [seconds] - Create a teleport that will disappear after n seconds

registertalkactionsplayersay("/tp", function(player, message)
	if cast("System.Int64", player.Rank) == rank.gamemaster then
		if checkpointposition then
			local position = player.Tile.Position:Offset(player.Direction)
			local tile = command.mapgettile(position)
			if tile then
				local item = command.tilecreateitem(tile, 1387, 0)
				item.Position = checkpointposition
				command.showmagiceffect(position, magiceffecttype.blueshimmer)
				local space = string.find(message, " ")
				if space then
					local seconds = tonumber(string.sub(message, space + 1))
					if seconds and seconds > 0 then
						local function loop(seconds)
							if seconds > 0 then						
								command.showanimatedtext(position, animatedtextcolor.blue, seconds)
								command.delay(item, 1 * 1000, function()
									loop(seconds - 1)
								end)
							else
								command.showmagiceffect(player, magiceffecttype.puff)
								command.itemdestroy(item)
							end
						end
						loop(seconds)
					end
				end
			else
				command.showmagiceffect(player, magiceffecttype.puff)
			end
		else
			command.showwindowtext(player, messagemode.failure, "Use '/cp' to set a checkpoint, then '/tp [seconds]' to create a teleport.")
			command.showmagiceffect(player, magiceffecttype.puff)
		end
		return true -- handled, stop process
	end
	return false
end)