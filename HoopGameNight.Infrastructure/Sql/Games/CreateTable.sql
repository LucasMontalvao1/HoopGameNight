CREATE TABLE IF NOT EXISTS games (
    id INT AUTO_INCREMENT PRIMARY KEY,

    -- IDs e datas
    external_id VARCHAR(100) NOT NULL COMMENT 'ESPN Game ID',
    date DATE NOT NULL COMMENT 'Data do jogo (local time)',
    datetime DATETIME NOT NULL COMMENT 'Data e hora completa',

    -- Times e placares
    home_team_id INT NOT NULL,
    visitor_team_id INT NOT NULL,
    home_team_score INT DEFAULT NULL,
    visitor_team_score INT DEFAULT NULL,

    -- Status e informações do jogo
    status VARCHAR(20) DEFAULT 'Scheduled' COMMENT 'Scheduled, Live, Final, Postponed, Cancelled',
    period INT DEFAULT NULL COMMENT 'Período/Quarter atual',
    time_remaining VARCHAR(10) DEFAULT NULL COMMENT 'Tempo restante',

    -- Informações da temporada
    postseason BOOLEAN DEFAULT FALSE COMMENT 'Playoffs',
    season INT NOT NULL COMMENT 'Ano da temporada',

    -- Timestamps
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,

    -- Foreign keys com CASCADE
    FOREIGN KEY (home_team_id) REFERENCES teams(id) ON DELETE CASCADE,
    FOREIGN KEY (visitor_team_id) REFERENCES teams(id) ON DELETE CASCADE,

    -- UNIQUE: Evita duplicatas (um jogo é único por: home + visitor + date)
    UNIQUE KEY uk_game_natural_key (home_team_id, visitor_team_id, date),

    -- Índices para buscas rápidas
    INDEX idx_games_external_id (external_id),
    INDEX idx_games_date (date),
    INDEX idx_games_datetime (datetime),
    INDEX idx_games_home_team (home_team_id),
    INDEX idx_games_visitor_team (visitor_team_id),
    INDEX idx_games_status (status),
    INDEX idx_games_season (season),
    INDEX idx_games_team_date (home_team_id, date),
    INDEX idx_games_visitor_date (visitor_team_id, date),
    INDEX idx_games_date_status (date, status)

) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci
COMMENT='Jogos da NBA - ESPN API';