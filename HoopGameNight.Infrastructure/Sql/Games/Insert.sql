-- Insert novo jogo (campos otimizados - sem is_future_game e data_source)
INSERT INTO games (
    external_id, date, datetime,
    home_team_id, visitor_team_id,
    home_team_score, visitor_team_score,
    status, period, time_remaining,
    postseason, season,
    created_at, updated_at
) VALUES (
    @ExternalId, @Date, @DateTime,
    @HomeTeamId, @VisitorTeamId,
    @HomeTeamScore, @VisitorTeamScore,
    @Status, @Period, @TimeRemaining,
    @PostSeason, @Season,
    NOW(), NOW()
);

SELECT LAST_INSERT_ID();