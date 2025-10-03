SELECT 
    p.id,
    p.first_name,
    p.last_name,
    t.abbreviation as team_abbreviation,
    ps.avg_assists as value,
    ps.games_played
FROM player_season_stats ps
INNER JOIN players p ON ps.player_id = p.id
LEFT JOIN teams t ON ps.team_id = t.id
WHERE ps.season = @Season
  AND ps.games_played >= @MinGames
ORDER BY ps.avg_assists DESC
LIMIT @Limit;