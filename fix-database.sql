-- Script para corrigir problemas no banco de dados

-- 1. Remover jogos com times inválidos (IDs que não existem na tabela teams)
DELETE FROM games
WHERE home_team_id NOT IN (SELECT id FROM teams)
   OR visitor_team_id NOT IN (SELECT id FROM teams);

-- 2. Verificar times duplicados por abreviação
SELECT abbreviation, COUNT(*) as total, GROUP_CONCAT(id) as ids
FROM teams
GROUP BY abbreviation
HAVING total > 1;

-- 3. Limpar jogadores com dados vazios
UPDATE players
SET first_name = 'Unknown',
    last_name = 'Player'
WHERE first_name = '' OR first_name IS NULL;

-- 4. Verificar integridade
SELECT 'Games sem times' as problema, COUNT(*) as quantidade
FROM games
WHERE home_team_id NOT IN (SELECT id FROM teams)
   OR visitor_team_id NOT IN (SELECT id FROM teams)

UNION ALL

SELECT 'Jogadores sem nome' as problema, COUNT(*) as quantidade
FROM players
WHERE first_name = '' OR first_name IS NULL

UNION ALL

SELECT 'Times duplicados' as problema, COUNT(*) as quantidade
FROM (
    SELECT abbreviation
    FROM teams
    GROUP BY abbreviation
    HAVING COUNT(*) > 1
) as duplicates;
