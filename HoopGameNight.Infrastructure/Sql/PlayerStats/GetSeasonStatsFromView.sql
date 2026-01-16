-- Recupera as métricas agregadas da temporada a partir da VIEW vw_player_season_stats_calculated
SELECT * 
FROM vw_player_season_stats_calculated
WHERE player_id = @PlayerId 
  AND season = @Season;
