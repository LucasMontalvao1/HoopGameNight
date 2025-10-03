SELECT 
    pgs.*,
    g.date as GameDate,
    g.home_team_id as HomeTeamId,
    g.visitor_team_id as VisitorTeamId,
    g.home_team_score as HomeScore,
    g.visitor_team_score as VisitorScore
FROM player_game_stats pgs
INNER JOIN games g ON pgs.game_id = g.id
WHERE pgs.player_id = @PlayerId 
  AND pgs.game_id = @GameId;