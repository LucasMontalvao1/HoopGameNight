-- ============================================
-- VIEW: Estatísticas por Temporada (Calculadas)
-- ============================================

DROP VIEW IF EXISTS vw_player_season_stats_calculated;

CREATE VIEW vw_player_season_stats_calculated AS
WITH game_agg AS (
    SELECT 
        pgs.player_id,
        g.season,
        pgs.team_id,
        COUNT(*) AS GamesPlayed,
        SUM(CASE WHEN pgs.is_starter THEN 1 ELSE 0 END) AS GamesStarted,
        SUM(pgs.points) AS TotalPoints,
        SUM(pgs.total_rebounds) AS TotalRebounds,
        SUM(pgs.assists) AS TotalAssists,
        SUM(pgs.steals) AS TotalSteals,
        SUM(pgs.blocks) AS TotalBlocks,
        SUM(pgs.turnovers) AS TotalTurnovers,
        SUM(pgs.personal_fouls) AS TotalPersonalFouls,
        SUM(pgs.field_goals_made) AS FieldGoalsMade,
        SUM(pgs.field_goals_attempted) AS FieldGoalsAttempted,
        SUM(pgs.three_pointers_made) AS ThreePointersMade,
        SUM(pgs.three_pointers_attempted) AS ThreePointersAttempted,
        SUM(pgs.free_throws_made) AS FreeThrowsMade,
        SUM(pgs.free_throws_attempted) AS FreeThrowsAttempted,
        SUM(pgs.offensive_rebounds) AS OffensiveRebounds,
        SUM(pgs.defensive_rebounds) AS DefensiveRebounds,
        SUM(pgs.minutes_played * 60 + pgs.seconds_played) / 60.0 AS MinutesPlayed
    FROM player_game_stats pgs
    INNER JOIN games g ON pgs.game_id = g.id
    WHERE pgs.did_not_play = FALSE
    GROUP BY pgs.player_id, g.season, pgs.team_id
)
SELECT 
    k.player_id,
    k.season,
    k.team_id,
    
    COALESCE(ps.games_played, g.GamesPlayed) AS GamesPlayed,
    COALESCE(ps.games_started, g.GamesStarted) AS GamesStarted,
    COALESCE(ps.points, g.TotalPoints) AS TotalPoints,
    COALESCE(ps.total_rebounds, g.TotalRebounds) AS TotalRebounds,
    COALESCE(ps.assists, g.TotalAssists) AS TotalAssists,
    COALESCE(ps.field_goals_made, g.FieldGoalsMade) AS FieldGoalsMade,
    COALESCE(ps.field_goals_attempted, g.FieldGoalsAttempted) AS FieldGoalsAttempted,
    COALESCE(ps.three_pointers_made, g.ThreePointersMade) AS ThreePointersMade,
    COALESCE(ps.three_pointers_attempted, g.ThreePointersAttempted) AS ThreePointersAttempted,
    COALESCE(ps.free_throws_made, g.FreeThrowsMade) AS FreeThrowsMade,
    COALESCE(ps.free_throws_attempted, g.FreeThrowsAttempted) AS FreeThrowsAttempted,
    COALESCE(ps.offensive_rebounds, g.OffensiveRebounds) AS OffensiveRebounds,
    COALESCE(ps.defensive_rebounds, g.DefensiveRebounds) AS DefensiveRebounds,
    COALESCE(ps.steals, g.TotalSteals) AS TotalSteals,
    COALESCE(ps.blocks, g.TotalBlocks) AS TotalBlocks,
    COALESCE(ps.turnovers, g.TotalTurnovers) AS TotalTurnovers,
    COALESCE(ps.personal_fouls, g.TotalPersonalFouls) AS TotalPersonalFouls,
    COALESCE(ps.minutes_played, g.MinutesPlayed) AS MinutesPlayed,
    
    -- Aliases para compatibilidade com DTO
    COALESCE(ps.points, g.TotalPoints) AS Points,
    
    -- Médias (Prioriza campos físicos ps.avg_*, senão calcula da View)
    ROUND(COALESCE(ps.avg_points, COALESCE(ps.points, g.TotalPoints) / NULLIF(COALESCE(ps.games_played, g.GamesPlayed), 0)), 1) AS PPG,
    ROUND(COALESCE(ps.avg_rebounds, COALESCE(ps.total_rebounds, g.TotalRebounds) / NULLIF(COALESCE(ps.games_played, g.GamesPlayed), 0)), 1) AS RPG,
    ROUND(COALESCE(ps.avg_assists, COALESCE(ps.assists, g.TotalAssists) / NULLIF(COALESCE(ps.games_played, g.GamesPlayed), 0)), 1) AS APG,
    ROUND(COALESCE(ps.avg_steals, COALESCE(ps.steals, g.TotalSteals) / NULLIF(COALESCE(ps.games_played, g.GamesPlayed), 0)), 1) AS SPG,
    ROUND(COALESCE(ps.avg_blocks, COALESCE(ps.blocks, g.TotalBlocks) / NULLIF(COALESCE(ps.games_played, g.GamesPlayed), 0)), 1) AS BPG,
    ROUND(COALESCE(ps.avg_turnovers, COALESCE(ps.turnovers, g.TotalTurnovers) / NULLIF(COALESCE(ps.games_played, g.GamesPlayed), 0)), 1) AS TPG,
    ROUND(COALESCE(ps.avg_fouls, COALESCE(ps.personal_fouls, g.TotalPersonalFouls) / NULLIF(COALESCE(ps.games_played, g.GamesPlayed), 0)), 1) AS FPG,
    ROUND(COALESCE(ps.avg_minutes, g.MinutesPlayed / NULLIF(COALESCE(ps.games_played, g.GamesPlayed), 0)), 1) AS MPG,
    
    -- Porcentagens (Prioriza ps.percentage_*, senão calcula)
    COALESCE(ps.field_goal_percentage, 
        CASE WHEN g.FieldGoalsAttempted > 0 THEN ROUND(g.FieldGoalsMade * 1.0 / g.FieldGoalsAttempted, 3) ELSE 0 END) AS FGPercentage,
    COALESCE(ps.three_point_percentage, 
        CASE WHEN g.ThreePointersAttempted > 0 THEN ROUND(g.ThreePointersMade * 1.0 / g.ThreePointersAttempted, 3) ELSE 0 END) AS ThreePointPercentage,
    COALESCE(ps.free_throw_percentage, 
        CASE WHEN g.FreeThrowsAttempted > 0 THEN ROUND(g.FreeThrowsMade * 1.0 / g.FreeThrowsAttempted, 3) ELSE 0 END) AS FTPercentage

FROM (
    -- Uniao de todas as chaves possiveis para garantir que nenhuma temporada suma
    SELECT player_id, season, team_id FROM player_game_stats pgs
    INNER JOIN games g ON pgs.game_id = g.id
    GROUP BY player_id, season, team_id
    
    UNION
    
    SELECT player_id, season, team_id FROM player_season_stats
) k
LEFT JOIN player_season_stats ps ON k.player_id = ps.player_id AND k.season = ps.season AND k.team_id = ps.team_id
LEFT JOIN game_agg g ON k.player_id = g.player_id AND k.season = g.season AND k.team_id = g.team_id;

