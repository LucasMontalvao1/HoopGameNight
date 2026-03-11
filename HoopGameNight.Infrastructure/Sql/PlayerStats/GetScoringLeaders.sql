SELECT 
    p.id AS PlayerId,
    p.external_id AS ExternalId,
    CONCAT(p.first_name, ' ', p.last_name) AS PlayerName,
    MAX(t.abbreviation) AS TeamAbbreviation,
    MAX(ps.avg_points) AS AverageValue,
    MAX(ps.games_played) AS GamesPlayed
FROM player_season_stats ps
INNER JOIN players p ON ps.player_id = p.id
LEFT JOIN teams t ON ps.team_id = t.id
WHERE ps.season = @Season
  AND ps.games_played >= @MinGames
GROUP BY p.id, p.external_id, p.first_name, p.last_name
ORDER BY AverageValue DESC
LIMIT @Limit;