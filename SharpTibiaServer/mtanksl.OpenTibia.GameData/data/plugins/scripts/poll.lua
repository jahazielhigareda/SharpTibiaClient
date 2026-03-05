local poll = nil

-- /poll <question> - Start the poll with a yes-no question

registertalkactionsplayersay("/poll", function(player, message)
	if cast("System.Int64", player.Rank) == rank.gamemaster then
		if not poll then
			local space = string.find(message, " ")
			if space then
				local question = string.sub(message, space + 1)
				if question and #question > 0 then
					poll = {
						question = question,
						voters = {},
						options = { yes = 0, no = 0 }
					}
					for _, player in pairs(command.gameobjectsgetplayers() ) do
						command.showwindowtext(player, messagemode.warning, "Poll '" .. poll.question .. "' started. Cast your vote with the command '!vote yes' or '!vote no'.")
					end
				else
					command.showwindowtext(player, messagemode.failure, "Use '/poll <question>' to start the poll.")
					command.showmagiceffect(player, magiceffecttype.puff)
				end
			else
				command.showwindowtext(player, messagemode.failure, "Use '/poll <question>' to start the poll.")
				command.showmagiceffect(player, magiceffecttype.puff)
			end
		else
			command.showwindowtext(player, messagemode.failure, "Poll is already running.")
			command.showmagiceffect(player, magiceffecttype.puff)
		end
		return true -- handled, stop process
	end
	return false
end)

-- !poll - Display the current poll

registertalkactionsplayersay("!poll", function(player, message)
	if poll then
		if cast("System.Int64", player.Rank) == rank.gamemaster then
			command.showwindowtext(player, messagemode.warning, "Poll '" .. poll.question .. "' is running with '" .. poll.options.yes .. " votes yes' and '" .. poll.options.no .. " votes no'. Cast your vote with the command '!vote yes' or '!vote no'.")
		else
			command.showwindowtext(player, messagemode.warning, "Poll '" .. poll.question .. "' is running. Cast your vote with the command '!vote yes' or '!vote no'.")
		end
	else
		command.showwindowtext(player, messagemode.failure, "Poll is not running.")
		command.showmagiceffect(player, magiceffecttype.puff)		
	end
	return true -- handled, stop process
end)

-- !vote yes - Cast your yes vote
-- !vote no - Cast your no vote

registertalkactionsplayersay("!vote", function(player, message)
	if poll then
		local space = string.find(message, " ")
		if space then
			local response = string.sub(message, space + 1)
			if response == "yes" or response == "no" then
				if not poll.voters[player.DatabasePlayerId] then
					poll.voters[player.DatabasePlayerId] = true
					if response == "yes" then
						poll.options.yes = poll.options.yes + 1
					elseif response == "no" then
						poll.options.no = poll.options.no + 1
					end
					command.showwindowtext(player, messagemode.look, "Your vote has been cast.")
				else
					command.showwindowtext(player, messagemode.failure, "Your vote has already been cast.")
					command.showmagiceffect(player, magiceffecttype.puff)
				end
			else
				command.showwindowtext(player, messagemode.failure, "Use '!vote yes' or '!vote no' to cast yout vote.")
				command.showmagiceffect(player, magiceffecttype.puff)
			end
		else 
			command.showwindowtext(player, messagemode.failure, "Use '!vote yes' or '!vote no' to cast yout vote.")
			command.showmagiceffect(player, magiceffecttype.puff)
		end
	else
		command.showwindowtext(player, messagemode.failure, "Poll is not running.")
		command.showmagiceffect(player, magiceffecttype.puff)
	end
	return true -- handled, stop process
end)

-- /endpoll - End the poll

registertalkactionsplayersay("/endpoll", function(player, message)
	if cast("System.Int64", player.Rank) == rank.gamemaster then
		if poll then
			for _, player in pairs(command.gameobjectsgetplayers() ) do
				command.showwindowtext(player, messagemode.warning, "Poll '" .. poll.question .. "' ended with '" .. poll.options.yes .. " votes yes' and '" .. poll.options.no .. " votes no'.'")
			end
			poll = nil
		else
			command.showwindowtext(player, messagemode.failure, "Use '/poll <question>' to start the poll, then '/endpoll' to end the poll.")
			command.showmagiceffect(player, magiceffecttype.puff)			
		end
		return true -- handled, stop process
	end
	return false
end)