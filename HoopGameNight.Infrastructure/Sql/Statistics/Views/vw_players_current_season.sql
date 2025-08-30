CREATE OR REPLACE VIEW vw_players_current_season AS
SELECT 
    p.*,
    t.name as team_name,
    t.abbreviation as team_abbreviation,
    ps.games_played,
    ps.avg_points,
    ps.avg_rebounds,
    ps.avg_assists,
    ps.field_goal_percentage
FROM players p
LEFT JOIN teams t ON p.team_id = t.id
LEFT JOIN player_season_stats ps ON p.id = ps.player_id 
    AND ps.season = YEAR(CURDATE())
WHERE p.is_active = TRUE;