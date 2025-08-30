CREATE OR REPLACE VIEW vw_scoring_leaders AS
SELECT 
    p.id,
    p.first_name,
    p.last_name,
    t.abbreviation as team,
    ps.avg_points,
    ps.games_played
FROM players p
JOIN player_season_stats ps ON p.id = ps.player_id
LEFT JOIN teams t ON p.team_id = t.id
WHERE ps.season = YEAR(CURDATE())
    AND ps.games_played >= 10
ORDER BY ps.avg_points DESC
LIMIT 50;