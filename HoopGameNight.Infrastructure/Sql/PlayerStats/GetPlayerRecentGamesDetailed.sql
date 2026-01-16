-- Buscar últimos N jogos de um jogador (detalhado)
SELECT * 
FROM vw_player_game_stats_detailed
WHERE player_id = @PlayerId
ORDER BY game_date DESC
LIMIT @Limit;
