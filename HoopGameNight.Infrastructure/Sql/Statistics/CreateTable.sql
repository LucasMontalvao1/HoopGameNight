-- ============================================
-- TABELA DE ESTATÍSTICAS POR TEMPORADA
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
    UNIQUE KEY unique_player_season_team (player_id, season, team_id),
    FOREIGN KEY (player_id) REFERENCES players(id) ON DELETE CASCADE,
    FOREIGN KEY (team_id) REFERENCES teams(id) ON DELETE SET NULL,
    
    -- Indexes
    INDEX idx_season_stats_player (player_id),
    INDEX idx_season_stats_season (season),
    INDEX idx_season_stats_team (team_id),
    INDEX idx_season_stats_points (points DESC),
    INDEX idx_season_stats_avg_points (avg_points DESC)
);

-- ============================================
-- TABELA DE ESTATÍSTICAS POR JOGO
-- ============================================
CREATE TABLE IF NOT EXISTS player_game_stats (
    id INT AUTO_INCREMENT PRIMARY KEY,
    player_id INT NOT NULL,
    game_id INT NOT NULL,
    team_id INT NOT NULL,
    
    -- Status do jogador no jogo
    did_not_play BOOLEAN DEFAULT FALSE,
    is_starter BOOLEAN DEFAULT FALSE,
    
    -- Tempo jogado
    minutes_played INT DEFAULT 0,
    seconds_played INT DEFAULT 0,
    
    -- Pontuação
    points INT DEFAULT 0,
    field_goals_made INT DEFAULT 0,
    field_goals_attempted INT DEFAULT 0,
    
    -- Arremessos de 3 pontos
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
    
    -- Plus/Minus
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
    INDEX idx_game_stats_date (game_id, player_id)
);

-- ============================================
-- TABELA DE ESTATÍSTICAS DE CARREIRA
-- ============================================
CREATE TABLE IF NOT EXISTS player_career_stats (
    id INT AUTO_INCREMENT PRIMARY KEY,
    player_id INT NOT NULL UNIQUE,
    
    -- Totais de carreira
    total_seasons INT DEFAULT 0,
    total_games INT DEFAULT 0,
    total_games_started INT DEFAULT 0,
    total_minutes DECIMAL(10, 2) DEFAULT 0,
    
    -- Pontuação total
    total_points INT DEFAULT 0,
    total_field_goals_made INT DEFAULT 0,
    total_field_goals_attempted INT DEFAULT 0,
    total_three_pointers_made INT DEFAULT 0,
    total_three_pointers_attempted INT DEFAULT 0,
    total_free_throws_made INT DEFAULT 0,
    total_free_throws_attempted INT DEFAULT 0,
    
    -- Outros totais
    total_rebounds INT DEFAULT 0,
    total_assists INT DEFAULT 0,
    total_steals INT DEFAULT 0,
    total_blocks INT DEFAULT 0,
    total_turnovers INT DEFAULT 0,
    
    -- Médias de carreira
    career_ppg DECIMAL(5, 2), -- Points per game
    career_rpg DECIMAL(5, 2), -- Rebounds per game
    career_apg DECIMAL(5, 2), -- Assists per game
    career_fg_percentage DECIMAL(5, 3),
    career_3pt_percentage DECIMAL(5, 3),
    career_ft_percentage DECIMAL(5, 3),
    
    -- Recordes
    highest_points_game INT DEFAULT 0,
    highest_rebounds_game INT DEFAULT 0,
    highest_assists_game INT DEFAULT 0,
    
    -- Timestamps
    last_game_date DATE,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    
    FOREIGN KEY (player_id) REFERENCES players(id) ON DELETE CASCADE,
    INDEX idx_career_stats_points (total_points DESC),
    INDEX idx_career_stats_ppg (career_ppg DESC)
);