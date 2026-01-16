-- Buscar estatísticas de todos os jogadores em um jogo
SELECT * 
FROM vw_player_game_stats_detailed
WHERE game_id = @GameId
ORDER BY team_id, is_starter DESC, points DESC;
