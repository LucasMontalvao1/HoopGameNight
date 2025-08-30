-- ============================================
-- Trigger: Atualizar estatísticas de carreira
-- Nome: trg_player_game_stats_after_insert
-- Momento: AFTER INSERT em player_game_stats
-- ============================================

CREATE TRIGGER trg_player_game_stats_after_insert
AFTER INSERT ON player_game_stats
FOR EACH ROW
BEGIN
    INSERT INTO player_career_stats (
        player_id,
        total_games,
        total_points,
        total_rebounds,
        total_assists,
        total_steals,
        total_blocks,
        highest_points_game,
        highest_rebounds_game,
        highest_assists_game
    ) VALUES (
        NEW.player_id,
        1,
        NEW.points,
        NEW.total_rebounds,
        NEW.assists,
        NEW.steals,
        NEW.blocks,
        NEW.points,
        NEW.total_rebounds,
        NEW.assists
    )
    ON DUPLICATE KEY UPDATE
        total_games     = total_games + 1,
        total_points    = total_points + NEW.points,
        total_rebounds  = total_rebounds + NEW.total_rebounds,
        total_assists   = total_assists + NEW.assists,
        total_steals    = total_steals + NEW.steals,
        total_blocks    = total_blocks + NEW.blocks,
        highest_points_game   = GREATEST(highest_points_game, NEW.points),
        highest_rebounds_game = GREATEST(highest_rebounds_game, NEW.total_rebounds),
        highest_assists_game  = GREATEST(highest_assists_game, NEW.assists),
        career_ppg = ROUND((total_points + NEW.points) / (total_games + 1), 2),
        career_rpg = ROUND((total_rebounds + NEW.total_rebounds) / (total_games + 1), 2),
        career_apg = ROUND((total_assists + NEW.assists) / (total_games + 1), 2);
END