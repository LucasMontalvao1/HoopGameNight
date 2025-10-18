-- Adicionar coluna espn_id na tabela players
ALTER TABLE players
ADD COLUMN espn_id VARCHAR(50) NULL AFTER external_id;

-- Criar índice para melhorar performance de busca
CREATE INDEX idx_players_espn_id ON players(espn_id);

-- Verificar
SELECT COUNT(*) as total_players,
       COUNT(espn_id) as players_with_espn_id
FROM players;
