-- ============================================
-- TABLE: Estatísticas por Temporada (Materializada)
-- Autor: HoopGameNight Team
-- Data: 2026-01-16
-- Descrição: Tabela que mantém dados agregados para performance
-- ============================================

USE hoop_game_night;

-- 1. Criar a tabela se não existir
CREATE TABLE IF NOT EXISTS season_stats_materialized (
    player_id INT NOT NULL,
    season INT NOT NULL,
    team_id INT NOT NULL,
    
    -- Contadores e Totais
    games_played INT DEFAULT 0,
    games_started INT DEFAULT 0,
    total_points INT DEFAULT 0,
    total_rebounds INT DEFAULT 0,
    total_assists INT DEFAULT 0,
    total_steals INT DEFAULT 0,
    total_blocks INT DEFAULT 0,
    total_turnovers INT DEFAULT 0,
    
    -- Médias (PPG, RPG, APG)
    ppg DECIMAL(5,1) DEFAULT 0.0,
    rpg DECIMAL(5,1) DEFAULT 0.0,
    apg DECIMAL(5,1) DEFAULT 0.0,
    spg DECIMAL(5,1) DEFAULT 0.0,
    bpg DECIMAL(5,1) DEFAULT 0.0,
    
    -- Porcentagens
    fg_percentage DECIMAL(5,1) DEFAULT 0.0,
    three_percentage DECIMAL(5,1) DEFAULT 0.0,
    ft_percentage DECIMAL(5,1) DEFAULT 0.0,
    
    double_doubles INT DEFAULT 0,
    triple_doubles INT DEFAULT 0,
    
    last_updated TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    
    PRIMARY KEY (player_id, season, team_id),
    INDEX idx_ssm_season_ppg (season, ppg DESC),
    INDEX idx_ssm_season_rpg (season, rpg DESC),
    INDEX idx_ssm_season_apg (season, apg DESC),
    INDEX idx_ssm_team (team_id)
) ENGINE=InnoDB;

-- 2. Stored Procedure para Refresh
DELIMITER //

DROP PROCEDURE IF EXISTS refresh_season_stats_materialized //

CREATE PROCEDURE refresh_season_stats_materialized()
BEGIN
    -- Limpar e reinserir dados (abordagem simples para materialização parcial)
    -- Em produção real, poderíamos usar gatilhos ou atualização incremental
    TRUNCATE TABLE season_stats_materialized;
    
    INSERT INTO season_stats_materialized (
        player_id, season, team_id,
        games_played, games_started, total_points, total_rebounds, total_assists, total_steals, total_blocks, total_turnovers,
        ppg, rpg, apg, spg, bpg,
        fg_percentage, three_percentage, ft_percentage,
        double_doubles, triple_doubles
    )
    SELECT 
        player_id, season, team_id,
        games_played, games_started, total_points, total_rebounds, total_assists, total_steals, total_blocks, total_turnovers,
        ppg, rpg, apg, spg, bpg,
        fg_percentage, three_percentage, ft_percentage,
        double_doubles, triple_doubles
    FROM vw_player_season_stats_calculated;
    
    SELECT 'Season stats materialized table refreshed' AS Status;
END //

DELIMITER ;

-- 3. Executar refresh inicial
-- CALL refresh_season_stats_materialized();
