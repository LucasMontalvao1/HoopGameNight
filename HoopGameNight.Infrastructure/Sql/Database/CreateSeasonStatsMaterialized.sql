USE hoop_game_night;

CREATE TABLE IF NOT EXISTS season_stats_materialized (
    player_id INT NOT NULL,
    season INT NOT NULL,
    team_id INT NOT NULL,
    
    games_played INT DEFAULT 0,
    games_started INT DEFAULT 0,
    total_points INT DEFAULT 0,
    total_rebounds INT DEFAULT 0,
    total_assists INT DEFAULT 0,
    total_steals INT DEFAULT 0,
    total_blocks INT DEFAULT 0,
    total_turnovers INT DEFAULT 0,
    
    ppg DECIMAL(5,1) DEFAULT 0.0,
    rpg DECIMAL(5,1) DEFAULT 0.0,
    apg DECIMAL(5,1) DEFAULT 0.0,
    spg DECIMAL(5,1) DEFAULT 0.0,
    bpg DECIMAL(5,1) DEFAULT 0.0,
    
    fg_percentage DECIMAL(5,3) DEFAULT 0.000,
    three_percentage DECIMAL(5,3) DEFAULT 0.000,
    ft_percentage DECIMAL(5,3) DEFAULT 0.000,
    
    double_doubles INT DEFAULT 0,
    triple_doubles INT DEFAULT 0,
    
    last_updated TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    
    PRIMARY KEY (player_id, season, team_id),
    INDEX idx_ssm_season_ppg (season, ppg DESC),
    INDEX idx_ssm_season_rpg (season, rpg DESC),
    INDEX idx_ssm_season_apg (season, apg DESC),
    INDEX idx_ssm_team (team_id)
) ENGINE=InnoDB;

DELIMITER //

DROP PROCEDURE IF EXISTS refresh_season_stats_materialized //

CREATE PROCEDURE refresh_season_stats_materialized()
BEGIN
    TRUNCATE TABLE season_stats_materialized;
    
    INSERT INTO season_stats_materialized (
        player_id, season, team_id,
        games_played, games_started,
        total_points, total_rebounds, total_assists,
        total_steals, total_blocks, total_turnovers,
        ppg, rpg, apg, spg, bpg,
        fg_percentage, three_percentage, ft_percentage,
        double_doubles, triple_doubles
    )
    SELECT 
        player_id, season, team_id,
        GamesPlayed, GamesStarted,
        TotalPoints, TotalRebounds, TotalAssists,
        TotalSteals, TotalBlocks, TotalTurnovers,
        PPG, RPG, APG, SPG, BPG,
        FGPercentage, ThreePointPercentage, FTPercentage,
        DoubleDoubles, TripleDoubles
    FROM vw_player_season_stats_calculated;
    
    SELECT 'Season stats materialized table refreshed' AS Status;
END //

DELIMITER ;

-- CALL refresh_season_stats_materialized();