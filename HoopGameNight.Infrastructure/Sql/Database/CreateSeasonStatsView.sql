-- ============================================
-- VIEW: Estatísticas por Temporada (Calculadas)
-- Autor: HoopGameNight Team
-- Data: 2025-12-24
-- Descrição: VIEW que agrega player_game_stats para calcular
--            estatísticas por temporada automaticamente
-- ============================================

USE hoop_game_night;

-- Remover VIEW se já existir
DROP VIEW IF EXISTS vw_player_season_stats_calculated;

-- Criar VIEW
CREATE VIEW vw_player_season_stats_calculated AS
SELECT 
    pgs.player_id,
    g.season,
    pgs.team_id,
    
    -- ========== Contadores ==========
    COUNT(*) AS games_played,
    SUM(CASE WHEN pgs.is_starter THEN 1 ELSE 0 END) AS games_started,
    
    -- ========== Totais ==========
    SUM(pgs.points) AS total_points,
    SUM(pgs.total_rebounds) AS total_rebounds,
    SUM(pgs.assists) AS total_assists,
    SUM(pgs.steals) AS total_steals,
    SUM(pgs.blocks) AS total_blocks,
    SUM(pgs.turnovers) AS total_turnovers,
    SUM(pgs.personal_fouls) AS total_fouls,
    
    -- ========== Arremessos Totais ==========
    SUM(pgs.field_goals_made) AS fg_made,
    SUM(pgs.field_goals_attempted) AS fg_attempted,
    SUM(pgs.three_pointers_made) AS three_made,
    SUM(pgs.three_pointers_attempted) AS three_attempted,
    SUM(pgs.free_throws_made) AS ft_made,
    SUM(pgs.free_throws_attempted) AS ft_attempted,
    
    -- ========== Rebotes Detalhados ==========
    SUM(pgs.offensive_rebounds) AS offensive_rebounds,
    SUM(pgs.defensive_rebounds) AS defensive_rebounds,
    
    -- ========== Médias por Jogo (PPG, RPG, APG) ==========
    ROUND(AVG(pgs.points), 1) AS ppg,
    ROUND(AVG(pgs.total_rebounds), 1) AS rpg,
    ROUND(AVG(pgs.assists), 1) AS apg,
    ROUND(AVG(pgs.steals), 1) AS spg,
    ROUND(AVG(pgs.blocks), 1) AS bpg,
    ROUND(AVG(pgs.turnovers), 1) AS tpg,
    ROUND(AVG(pgs.personal_fouls), 1) AS fpg,
    
    -- ========== Porcentagens ==========
    CASE 
        WHEN SUM(pgs.field_goals_attempted) > 0 
        THEN ROUND((SUM(pgs.field_goals_made) / SUM(pgs.field_goals_attempted)) * 100, 1)
        ELSE 0 
    END AS fg_percentage,
    
    CASE 
        WHEN SUM(pgs.three_pointers_attempted) > 0 
        THEN ROUND((SUM(pgs.three_pointers_made) / SUM(pgs.three_pointers_attempted)) * 100, 1)
        ELSE 0 
    END AS three_percentage,
    
    CASE 
        WHEN SUM(pgs.free_throws_attempted) > 0 
        THEN ROUND((SUM(pgs.free_throws_made) / SUM(pgs.free_throws_attempted)) * 100, 1)
        ELSE 0 
    END AS ft_percentage,
    
    -- ========== Minutos ==========
    SUM(pgs.minutes_played * 60 + pgs.seconds_played) / 60.0 AS total_minutes,
    ROUND(AVG(pgs.minutes_played + pgs.seconds_played / 60.0), 1) AS mpg,
    
    -- ========== Plus/Minus ==========
    ROUND(AVG(pgs.plus_minus), 1) AS avg_plus_minus,
    
    -- ========== Indicadores de Performance ==========
    SUM(CASE WHEN (
        (CASE WHEN pgs.points >= 10 THEN 1 ELSE 0 END) +
        (CASE WHEN pgs.total_rebounds >= 10 THEN 1 ELSE 0 END) +
        (CASE WHEN pgs.assists >= 10 THEN 1 ELSE 0 END) +
        (CASE WHEN pgs.steals >= 10 THEN 1 ELSE 0 END) +
        (CASE WHEN pgs.blocks >= 10 THEN 1 ELSE 0 END)
    ) >= 2 THEN 1 ELSE 0 END) AS double_doubles,
    
    SUM(CASE WHEN (
        (CASE WHEN pgs.points >= 10 THEN 1 ELSE 0 END) +
        (CASE WHEN pgs.total_rebounds >= 10 THEN 1 ELSE 0 END) +
        (CASE WHEN pgs.assists >= 10 THEN 1 ELSE 0 END) +
        (CASE WHEN pgs.steals >= 10 THEN 1 ELSE 0 END) +
        (CASE WHEN pgs.blocks >= 10 THEN 1 ELSE 0 END)
    ) >= 3 THEN 1 ELSE 0 END) AS triple_doubles,
    
    -- ========== Timestamps ==========
    MIN(g.date) AS first_game_date,
    MAX(g.date) AS last_game_date

FROM player_game_stats pgs
INNER JOIN games g ON pgs.game_id = g.id
WHERE pgs.did_not_play = FALSE
GROUP BY pgs.player_id, g.season, pgs.team_id;

-- ============================================
-- Verificação
-- ============================================
SELECT 'VIEW vw_player_season_stats_calculated criada com sucesso!' AS Status;

-- Teste rápido
SELECT COUNT(*) AS total_season_stats FROM vw_player_season_stats_calculated;

-- ============================================
-- Queries de Exemplo
-- ============================================

-- Exemplo 1: Stats de um jogador em uma temporada
-- SELECT * FROM vw_player_season_stats_calculated WHERE player_id = 1 AND season = 2024;

-- Exemplo 2: Top 10 pontuadores da temporada
-- SELECT * FROM vw_player_season_stats_calculated WHERE season = 2024 ORDER BY ppg DESC LIMIT 10;

-- Exemplo 3: Jogadores com mais double-doubles
-- SELECT * FROM vw_player_season_stats_calculated WHERE season = 2024 ORDER BY double_doubles DESC LIMIT 10;
