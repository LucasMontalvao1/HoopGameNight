-- ============================================
-- TABELA DE ESTATÍSTICAS POR TEMPORADA
-- Única tabela de estatísticas focada em stats por temporada
-- ============================================
CREATE TABLE IF NOT EXISTS player_season_stats (
    id INT AUTO_INCREMENT PRIMARY KEY,
    player_id INT NOT NULL,
    season INT NOT NULL,
    team_id INT,

    -- Jogos
    games_played INT DEFAULT 0,
    games_started INT DEFAULT 0,

    -- Minutos
    minutes_played DECIMAL(10, 2) DEFAULT 0,

    -- Pontuação
    points INT DEFAULT 0,
    field_goals_made INT DEFAULT 0,
    field_goals_attempted INT DEFAULT 0,
    field_goal_percentage DECIMAL(5, 3),

    -- Arremessos de 3 pontos
    three_pointers_made INT DEFAULT 0,
    three_pointers_attempted INT DEFAULT 0,
    three_point_percentage DECIMAL(5, 3),

    -- Lances livres
    free_throws_made INT DEFAULT 0,
    free_throws_attempted INT DEFAULT 0,
    free_throw_percentage DECIMAL(5, 3),

    -- Rebotes
    offensive_rebounds INT DEFAULT 0,
    defensive_rebounds INT DEFAULT 0,
    total_rebounds INT DEFAULT 0,

    -- Outras estatísticas
    assists INT DEFAULT 0,
    steals INT DEFAULT 0,
    blocks INT DEFAULT 0,
    turnovers INT DEFAULT 0,
    personal_fouls INT DEFAULT 0,

    -- Médias (calculadas automaticamente via triggers)
    avg_points DECIMAL(5, 2),
    avg_rebounds DECIMAL(5, 2),
    avg_assists DECIMAL(5, 2),
    avg_minutes DECIMAL(5, 2),

    -- Timestamps
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,

    -- Constraints
    UNIQUE KEY unique_player_season (player_id, season),
    FOREIGN KEY (player_id) REFERENCES players(id) ON DELETE CASCADE,
    FOREIGN KEY (team_id) REFERENCES teams(id) ON DELETE SET NULL,

    -- Indexes
    INDEX idx_season_stats_player (player_id),
    INDEX idx_season_stats_season (season),
    INDEX idx_season_stats_team (team_id),
    INDEX idx_season_stats_points (points DESC),
    INDEX idx_season_stats_avg_points (avg_points DESC)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
COMMENT='Estatísticas por temporada de cada jogador';
