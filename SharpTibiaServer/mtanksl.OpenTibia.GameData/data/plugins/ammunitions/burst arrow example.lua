function onusingammunition(player, target, weapon, ammunition)
	--TODO
	return true -- validated, continue process
end

function onuseammunition(player, target, weapon, ammunition)
	local area = {
		{-1, -1}, {0, -1}, {1, -1},
		{-1, 0},  {0, 0},  {1, 0},
		{-1, 1},  {0, 1},  {1, 1}
	}
	local min, max = formula.distance(player.Level, player.Skills.GetSkillLevel(skill.distance), ammunition.Metadata.Attack, cast("System.Int64", player.Client.FightMode) )
	command.creatureattackarea(player, false, target.Tile.Position, area, cast("System.Int64", ammunition.Metadata.ProjectileType), magiceffecttype.firearea, attack.simple(nil, nil, damagetype.fire, min, max), nil)
end