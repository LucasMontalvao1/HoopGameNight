-- ============================================
-- VIEW: Estatísticas por Temporada (Calculadas)
-- ============================================

USE hoop_game_night;

DROP VIEW IF EXISTS vw_player_season_stats_calculated;

CREATE VIEW vw_player_season_stats_calculated AS
SELECT 
    pgs.player_id,
    g.season,
    pgs.team_id,
    
    -- Contadores
    COUNT(*) AS GamesPlayed,
    SUM(CASE WHEN pgs.is_starter THEN 1 ELSE 0 END) AS GamesStarted,
    
    -- Totais
    SUM(pgs.points) AS TotalPoints,
    SUM(pgs.total_rebounds) AS TotalRebounds,
    SUM(pgs.assists) AS TotalAssists,
    SUM(pgs.steals) AS TotalSteals,
    SUM(pgs.blocks) AS TotalBlocks,
    SUM(pgs.turnovers) AS TotalTurnovers,
    SUM(pgs.personal_fouls) AS TotalPersonalFouls,
    
    -- Arremessos
    SUM(pgs.field_goals_made) AS FieldGoalsMade,
    SUM(pgs.field_goals_attempted) AS FieldGoalsAttempted,
    SUM(pgs.three_pointers_made) AS ThreePointersMade,
    SUM(pgs.three_pointers_attempted) AS ThreePointersAttempted,
    SUM(pgs.free_throws_made) AS FreeThrowsMade,
    SUM(pgs.free_throws_attempted) AS FreeThrowsAttempted,
    
    -- Rebotes
    SUM(pgs.offensive_rebounds) AS OffensiveRebounds,
    SUM(pgs.defensive_rebounds) AS DefensiveRebounds,
    
    -- Médias
    ROUND(AVG(pgs.points), 1) AS PPG,
    ROUND(AVG(pgs.total_rebounds), 1) AS RPG,
    ROUND(AVG(pgs.assists), 1) AS APG,
    ROUND(AVG(pgs.steals), 1) AS SPG,
    ROUND(AVG(pgs.blocks), 1) AS BPG,
    ROUND(AVG(pgs.turnovers), 1) AS TPG,
    ROUND(AVG(pgs.personal_fouls), 1) AS FPG,
    ROUND(AVG(pgs.minutes_played + pgs.seconds_played / 60.0), 1) AS MPG,
    
    -- Porcentagens
    CASE 
        WHEN SUM(pgs.field_goals_attempted) > 0 
        THEN ROUND(SUM(pgs.field_goals_made) * 1.0 / SUM(pgs.field_goals_attempted), 3)
        ELSE 0 
    END AS FGPercentage,
    
    CASE 
        WHEN SUM(pgs.three_pointers_attempted) > 0 
        THEN ROUND(SUM(pgs.three_pointers_made) * 1.0 / SUM(pgs.three_pointers_attempted), 3)
        ELSE 0 
    END AS ThreePointPercentage,
    
    CASE 
        WHEN SUM(pgs.free_throws_attempted) > 0 
        THEN ROUND(SUM(pgs.free_throws_made) * 1.0 / SUM(pgs.free_throws_attempted), 3)
        ELSE 0 
    END AS FTPercentage,
    
    -- Minutos
    ROUND(SUM(pgs.minutes_played * 60 + pgs.seconds_played) / 60.0, 1) AS MinutesPlayed,
    
    -- Plus/Minus
    ROUND(AVG(pgs.plus_minus), 1) AS AvgPlusMinus,
    
    -- Double-doubles e Triple-doubles
    SUM(CASE WHEN (
        (CASE WHEN pgs.points >= 10 THEN 1 ELSE 0 END) +
        (CASE WHEN pgs.total_rebounds >= 10 THEN 1 ELSE 0 END) +
        (CASE WHEN pgs.assists >= 10 THEN 1 ELSE 0 END) +
        (CASE WHEN pgs.steals >= 10 THEN 1 ELSE 0 END) +
        (CASE WHEN pgs.blocks >= 10 THEN 1 ELSE 0 END)
    ) >= 2 THEN 1 ELSE 0 END) AS DoubleDoubles,
    
    SUM(CASE WHEN (
        (CASE WHEN pgs.points >= 10 THEN 1 ELSE 0 END) +
        (CASE WHEN pgs.total_rebounds >= 10 THEN 1 ELSE 0 END) +
        (CASE WHEN pgs.assists >= 10 THEN 1 ELSE 0 END) +
        (CASE WHEN pgs.steals >= 10 THEN 1 ELSE 0 END) +
        (CASE WHEN pgs.blocks >= 10 THEN 1 ELSE 0 END)
    ) >= 3 THEN 1 ELSE 0 END) AS TripleDoubles,
    
    -- Datas
    MIN(g.date) AS FirstGameDate,
    MAX(g.date) AS LastGameDate

FROM player_game_stats pgs
INNER JOIN games g ON pgs.game_id = g.id
WHERE pgs.did_not_play = FALSE
GROUP BY pgs.player_id, g.season, pgs.team_id;

SELECT 'VIEW vw_player_season_stats_calculated criada com sucesso!' AS Status;
SELECT COUNT(*) AS TotalSeasonStats FROM vw_player_season_stats_calculated;

-- Exemplos de uso:
-- SELECT * FROM vw_player_season_stats_calculated WHERE player_id = 1 AND season = 2024;
-- SELECT * FROM vw_player_season_stats_calculated WHERE season = 2024 ORDER BY PPG DESC LIMIT 10;
-- SELECT * FROM vw_player_season_stats_calculated WHERE season = 2024 ORDER BY DoubleDoubles DESC LIMIT 10;