-- Update jogo existente (campos otimizados - sem is_future_game e data_source)
UPDATE games SET
    home_team_score = @HomeTeamScore,
    visitor_team_score = @VisitorTeamScore,
    status = @Status,
    period = @Period,
    time_remaining = @TimeRemaining,
    updated_at = @UpdatedAt
WHERE id = @Id;