-- ============================================
-- VIEW: Estatísticas Detalhadas de Jogadores por Jogo
-- Autor: HoopGameNight Team
-- Data: 2025-12-24
-- Descrição: VIEW otimizada para consultar estatísticas de jogadores
--            com informações do jogo, times e adversários
-- ============================================

USE hoop_game_night;

-- Remover VIEW se já existir
DROP VIEW IF EXISTS vw_player_game_stats_detailed;

-- Criar VIEW
CREATE VIEW vw_player_game_stats_detailed AS
SELECT 
    -- ========== IDs ==========
    pgs.id,
    pgs.player_id,
    pgs.game_id,
    pgs.team_id,
    
    -- ========== Informações do Jogador ==========
    p.first_name AS player_first_name,
    p.last_name AS player_last_name,
    CONCAT(p.first_name, ' ', p.last_name) AS player_full_name,
    p.jersey_number,
    p.position,
    
    -- ========== Informações do Jogo ==========
    g.date AS game_date,
    g.home_team_id,
    g.visitor_team_id,
    g.home_team_score,
    g.visitor_team_score,
    g.status AS game_status,
    
    -- ========== Time do Jogador ==========
    t.abbreviation AS team_abbreviation,
    t.full_name AS team_name,
    
    -- ========== Time Adversário ==========
    CASE 
        WHEN pgs.team_id = g.home_team_id THEN vt.abbreviation
        ELSE ht.abbreviation
    END AS opponent_abbreviation,
    CASE 
        WHEN pgs.team_id = g.home_team_id THEN vt.full_name
        ELSE ht.full_name
    END AS opponent_name,
    
    -- ========== Resultado do Jogo ==========
    CASE 
        WHEN pgs.team_id = g.home_team_id THEN
            CASE 
                WHEN g.home_team_score > g.visitor_team_score THEN 'W'
                WHEN g.home_team_score < g.visitor_team_score THEN 'L'
                ELSE 'T'
            END
        ELSE
            CASE 
                WHEN g.visitor_team_score > g.home_team_score THEN 'W'
                WHEN g.visitor_team_score < g.home_team_score THEN 'L'
                ELSE 'T'
            END
    END AS result,
    
    -- ========== Indicadores ==========
    pgs.did_not_play,
    pgs.is_starter,
    (pgs.team_id = g.home_team_id) AS is_home,
    
    -- ========== Tempo de Jogo ==========
    pgs.minutes_played,
    pgs.seconds_played,
    CONCAT(pgs.minutes_played, ':', LPAD(pgs.seconds_played, 2, '0')) AS minutes_formatted,
    
    -- ========== Pontuação ==========
    pgs.points,
    pgs.field_goals_made,
    pgs.field_goals_attempted,
    CONCAT(pgs.field_goals_made, '/', pgs.field_goals_attempted) AS field_goals_formatted,
    CASE 
        WHEN pgs.field_goals_attempted > 0 
        THEN ROUND((pgs.field_goals_made / pgs.field_goals_attempted) * 100, 1)
        ELSE 0 
    END AS field_goal_percentage,
    
    -- ========== Três Pontos ==========
    pgs.three_pointers_made,
    pgs.three_pointers_attempted,
    CONCAT(pgs.three_pointers_made, '/', pgs.three_pointers_attempted) AS three_pointers_formatted,
    CASE 
        WHEN pgs.three_pointers_attempted > 0 
        THEN ROUND((pgs.three_pointers_made / pgs.three_pointers_attempted) * 100, 1)
        ELSE 0 
    END AS three_point_percentage,
    
    -- ========== Lances Livres ==========
    pgs.free_throws_made,
    pgs.free_throws_attempted,
    CONCAT(pgs.free_throws_made, '/', pgs.free_throws_attempted) AS free_throws_formatted,
    CASE 
        WHEN pgs.free_throws_attempted > 0 
        THEN ROUND((pgs.free_throws_made / pgs.free_throws_attempted) * 100, 1)
        ELSE 0 
    END AS free_throw_percentage,
    
    -- ========== Rebotes ==========
    pgs.offensive_rebounds,
    pgs.defensive_rebounds,
    pgs.total_rebounds,
    
    -- ========== Outras Estatísticas ==========
    pgs.assists,
    pgs.steals,
    pgs.blocks,
    pgs.turnovers,
    pgs.personal_fouls,
    pgs.plus_minus,
    
    -- ========== Indicadores de Performance ==========
    (
        (CASE WHEN pgs.points >= 10 THEN 1 ELSE 0 END) +
        (CASE WHEN pgs.total_rebounds >= 10 THEN 1 ELSE 0 END) +
        (CASE WHEN pgs.assists >= 10 THEN 1 ELSE 0 END) +
        (CASE WHEN pgs.steals >= 10 THEN 1 ELSE 0 END) +
        (CASE WHEN pgs.blocks >= 10 THEN 1 ELSE 0 END)
    ) >= 2 AS double_double,
    
    (
        (CASE WHEN pgs.points >= 10 THEN 1 ELSE 0 END) +
        (CASE WHEN pgs.total_rebounds >= 10 THEN 1 ELSE 0 END) +
        (CASE WHEN pgs.assists >= 10 THEN 1 ELSE 0 END) +
        (CASE WHEN pgs.steals >= 10 THEN 1 ELSE 0 END) +
        (CASE WHEN pgs.blocks >= 10 THEN 1 ELSE 0 END)
    ) >= 3 AS triple_double,
    
    -- ========== Timestamps ==========
    pgs.created_at,
    pgs.updated_at

FROM player_game_stats pgs
INNER JOIN players p ON pgs.player_id = p.id
INNER JOIN games g ON pgs.game_id = g.id
INNER JOIN teams t ON pgs.team_id = t.id
LEFT JOIN teams ht ON g.home_team_id = ht.id
LEFT JOIN teams vt ON g.visitor_team_id = vt.id;

-- ============================================
-- Verificação
-- ============================================
SELECT 'VIEW vw_player_game_stats_detailed criada com sucesso!' AS Status;

-- Teste rápido
SELECT COUNT(*) AS total_registros FROM vw_player_game_stats_detailed;

-- ============================================
-- Queries de Exemplo
-- ============================================

-- Exemplo 1: Buscar stats de um jogador em um jogo
-- SELECT * FROM vw_player_game_stats_detailed WHERE player_id = 1 AND game_id = 1;

-- Exemplo 2: Últimos 10 jogos de um jogador
-- SELECT * FROM vw_player_game_stats_detailed WHERE player_id = 1 ORDER BY game_date DESC LIMIT 10;

-- Exemplo 3: Todos os jogadores de um jogo
-- SELECT * FROM vw_player_game_stats_detailed WHERE game_id = 1 ORDER BY team_id, is_starter DESC, points DESC;

-- Exemplo 4: Jogos com double-double
-- SELECT * FROM vw_player_game_stats_detailed WHERE double_double = 1 ORDER BY game_date DESC LIMIT 20;

-- Exemplo 5: Jogos com triple-double
-- SELECT * FROM vw_player_game_stats_detailed WHERE triple_double = 1 ORDER BY game_date DESC;
