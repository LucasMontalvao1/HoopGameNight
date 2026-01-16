-- Buscar todos os jogos de um jogador (paginado)
SELECT * 
FROM vw_player_game_stats_detailed
WHERE player_id = @PlayerId
ORDER BY game_date DESC
LIMIT @PageSize OFFSET @Offset;
