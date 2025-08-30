CREATE TABLE IF NOT EXISTS players (
    id INT AUTO_INCREMENT PRIMARY KEY,
    external_id INT NOT NULL UNIQUE,
    
    -- Informações básicas
    first_name VARCHAR(50) NOT NULL,
    last_name VARCHAR(50) NOT NULL,
    jersey_number INT,
    
    -- Posição e características físicas
    position ENUM('PG', 'SG', 'SF', 'PF', 'C', 'G', 'F', 'G-F', 'F-C') DEFAULT NULL,
    height_feet INT,
    height_inches INT,
    weight_pounds INT,
    
    -- Informações adicionais
    birth_date DATE,
    birth_city VARCHAR(100),
    birth_country VARCHAR(50),
    college VARCHAR(100),
    draft_year INT,
    draft_round INT,
    draft_pick INT,
    
    -- Time atual
    team_id INT,
    is_active BOOLEAN DEFAULT TRUE,
    
    -- Timestamps
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,
    
    -- Foreign Keys
    FOREIGN KEY (team_id) REFERENCES teams(id) ON DELETE SET NULL,
    
    -- Indexes para performance
    INDEX idx_players_external_id (external_id),
    INDEX idx_players_team_id (team_id),
    INDEX idx_players_full_name (first_name, last_name),
    INDEX idx_players_position (position),
    INDEX idx_players_is_active (is_active),
    INDEX idx_players_jersey (team_id, jersey_number)
);