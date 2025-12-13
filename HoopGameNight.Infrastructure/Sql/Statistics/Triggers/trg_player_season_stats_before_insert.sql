-- ============================================
-- Trigger: Calcular médias em player_season_stats
-- Nome: trg_player_season_stats_before_insert
-- Momento: BEFORE INSERT
-- ============================================

CREATE TRIGGER trg_player_season_stats_before_insert
BEFORE INSERT ON player_season_stats
FOR EACH ROW
BEGIN
    IF NEW.games_played > 0 THEN
        SET NEW.avg_points   = ROUND(NEW.points / NULLIF(NEW.games_played, 0), 2);
        SET NEW.avg_rebounds = ROUND(NEW.total_rebounds / NULLIF(NEW.games_played, 0), 2);
        SET NEW.avg_assists  = ROUND(NEW.assists / NULLIF(NEW.games_played, 0), 2);
        SET NEW.avg_minutes  = ROUND(NEW.minutes_played / NULLIF(NEW.games_played, 0), 2);

        IF NEW.field_goals_attempted > 0 THEN
            SET NEW.field_goal_percentage = ROUND(NEW.field_goals_made / NEW.field_goals_attempted, 3);
        END IF;

        IF NEW.three_pointers_attempted > 0 THEN
            SET NEW.three_point_percentage = ROUND(NEW.three_pointers_made / NEW.three_pointers_attempted, 3);
        END IF;

        IF NEW.free_throws_attempted > 0 THEN
            SET NEW.free_throw_percentage = ROUND(NEW.free_throws_made / NEW.free_throws_attempted, 3);
        END IF;
    END IF;
END
