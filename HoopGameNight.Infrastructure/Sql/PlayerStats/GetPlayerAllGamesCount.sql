-- Contar total de jogos de um jogador
SELECT COUNT(*) 
FROM vw_player_game_stats_detailed
WHERE player_id = @PlayerId;
