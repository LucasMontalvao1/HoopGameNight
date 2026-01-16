-- ============================================
-- TABELA DE ESTATÍSTICAS POR TEMPORADA
-- Única tabela de estatísticas focada em stats por temporada
-- ============================================
CREATE TABLE IF NOT EXISTS player_season_stats (
    id INT AUTO_INCREMENT PRIMARY KEY,
    player_id INT NOT NULL,
    season INT NOT NULL,
    season_type_id INT NOT NULL DEFAULT 2,
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
    field_goal_percentage DECIMAL(5, 2),

    -- Arremessos de 3 pontos
    three_pointers_made INT DEFAULT 0,
    three_pointers_attempted INT DEFAULT 0,
    three_point_percentage DECIMAL(5, 2),

    -- Lances livres
    free_throws_made INT DEFAULT 0,
    free_throws_attempted INT DEFAULT 0,
    free_throw_percentage DECIMAL(5, 2),

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
    UNIQUE KEY unique_player_season (player_id, season, season_type_id, team_id),
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

-- ============================================
-- TABELA DE ESTATÍSTICAS POR JOGO
-- Estatísticas detalhadas de cada jogador em cada jogo
-- ============================================
CREATE TABLE IF NOT EXISTS player_game_stats (
    id INT AUTO_INCREMENT PRIMARY KEY,
    player_id INT NOT NULL,
    game_id INT NOT NULL,
    team_id INT NOT NULL,
    
    -- Status
    did_not_play BOOLEAN DEFAULT FALSE,
    is_starter BOOLEAN DEFAULT FALSE,
    
    -- Tempo de jogo
    minutes_played INT DEFAULT 0,
    seconds_played INT DEFAULT 0,
    
    -- Pontuação
    points INT DEFAULT 0,
    field_goals_made INT DEFAULT 0,
    field_goals_attempted INT DEFAULT 0,
    
    -- Três pontos
    three_pointers_made INT DEFAULT 0,
    three_pointers_attempted INT DEFAULT 0,
    
    -- Lances livres
    free_throws_made INT DEFAULT 0,
    free_throws_attempted INT DEFAULT 0,
    
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
    plus_minus INT DEFAULT 0,
    
    -- Timestamps
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    
    -- Constraints
    UNIQUE KEY unique_player_game (player_id, game_id),
    FOREIGN KEY (player_id) REFERENCES players(id) ON DELETE CASCADE,
    FOREIGN KEY (game_id) REFERENCES games(id) ON DELETE CASCADE,
    FOREIGN KEY (team_id) REFERENCES teams(id) ON DELETE CASCADE,
    
    -- Indexes
    INDEX idx_game_stats_player (player_id),
    INDEX idx_game_stats_game (game_id),
    INDEX idx_game_stats_team (team_id),
    INDEX idx_game_stats_points (points DESC),
    INDEX idx_game_stats_player_game (player_id, game_id)
    
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
COMMENT='Estatísticas de jogadores por jogo';

-- ============================================
-- TABELA DE ESTATÍSTICAS DE CARREIRA
-- Agregação histórica do jogador
-- ============================================
CREATE TABLE IF NOT EXISTS player_career_stats (
    id INT AUTO_INCREMENT PRIMARY KEY,
    player_id INT NOT NULL,
    total_seasons INT DEFAULT 0,
    total_games INT DEFAULT 0,
    total_games_started INT DEFAULT 0,
    minutes_played DECIMAL(10, 1) DEFAULT 0,
    
    -- Totais
    points INT DEFAULT 0,
    field_goals_made INT DEFAULT 0,
    field_goals_attempted INT DEFAULT 0,
    three_pointers_made INT DEFAULT 0,
    three_pointers_attempted INT DEFAULT 0,
    free_throws_made INT DEFAULT 0,
    free_throws_attempted INT DEFAULT 0,
    offensive_rebounds INT DEFAULT 0,
    defensive_rebounds INT DEFAULT 0,
    total_rebounds INT DEFAULT 0,
    assists INT DEFAULT 0,
    steals INT DEFAULT 0,
    blocks INT DEFAULT 0,
    turnovers INT DEFAULT 0,
    personal_fouls INT DEFAULT 0,
    
    -- Médias
    career_ppg DECIMAL(5, 2) DEFAULT 0,
    career_rpg DECIMAL(5, 2) DEFAULT 0,
    career_apg DECIMAL(5, 2) DEFAULT 0,
    
    -- Porcentagens
    career_fg_percentage DECIMAL(5, 2) DEFAULT 0,
    career_3pt_percentage DECIMAL(5, 2) DEFAULT 0,
    career_ft_percentage DECIMAL(5, 2) DEFAULT 0,
    
    -- Highs
    highest_points_game INT DEFAULT 0,
    highest_rebounds_game INT DEFAULT 0,
    highest_assists_game INT DEFAULT 0,
    
    -- Metadata
    last_game_date DATETIME,
    career_summary VARCHAR(255),
    
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    
    FOREIGN KEY (player_id) REFERENCES players(id) ON DELETE CASCADE,
    UNIQUE KEY unique_player_career (player_id)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
