SELECT 
    pgs.*,
    g.date as GameDate,
    g.home_team_id as HomeTeamId,
    g.visitor_team_id as VisitorTeamId,
    g.home_team_score as HomeScore,
    g.visitor_team_score as VisitorScore,
    CASE 
        WHEN pgs.team_id = g.home_team_id THEN vt.abbreviation
        ELSE ht.abbreviation
    END as Opponent,
    CASE 
        WHEN pgs.team_id = g.home_team_id THEN 1
        ELSE 0
    END as IsHome
FROM player_game_stats pgs
INNER JOIN games g ON pgs.game_id = g.id
INNER JOIN teams ht ON g.home_team_id = ht.id
INNER JOIN teams vt ON g.visitor_team_id = vt.id
WHERE pgs.player_id = @PlayerId
  AND g.status = 'Final'
ORDER BY g.date DESC
LIMIT @Limit;