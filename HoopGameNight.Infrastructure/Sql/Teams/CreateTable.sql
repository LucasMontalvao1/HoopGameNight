CREATE TABLE IF NOT EXISTS teams (
    id INT AUTO_INCREMENT PRIMARY KEY,
    external_id INT NOT NULL UNIQUE COMMENT 'Legacy external ID',
    external_espn_id VARCHAR(50) DEFAULT NULL UNIQUE COMMENT 'ESPN Team ID (string)',
    name VARCHAR(100) NOT NULL,
    full_name VARCHAR(150) NOT NULL,
    abbreviation VARCHAR(5) NOT NULL,
    city VARCHAR(100) NOT NULL,
    conference VARCHAR(10) NOT NULL,
    division VARCHAR(20) NOT NULL,
    created_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP,
    updated_at TIMESTAMP DEFAULT CURRENT_TIMESTAMP ON UPDATE CURRENT_TIMESTAMP,

    INDEX idx_teams_external_id (external_id),
    INDEX idx_teams_espn_id (external_espn_id),
    INDEX idx_teams_abbreviation (abbreviation),
    INDEX idx_teams_conference (conference),
    INDEX idx_teams_division (division)
);