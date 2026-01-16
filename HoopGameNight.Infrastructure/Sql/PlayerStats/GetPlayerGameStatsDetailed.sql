-- Buscar estatísticas detalhadas de um jogador em um jogo específico
SELECT * 
FROM vw_player_game_stats_detailed
WHERE player_id = @PlayerId 
  AND game_id = @GameId;
