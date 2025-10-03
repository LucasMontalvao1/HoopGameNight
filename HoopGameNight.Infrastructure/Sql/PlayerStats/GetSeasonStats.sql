SELECT 
    ps.*,
    t.name as TeamName,
    t.abbreviation as TeamAbbreviation
FROM player_season_stats ps
LEFT JOIN teams t ON ps.team_id = t.id
WHERE ps.player_id = @PlayerId 
  AND ps.season = @Season;