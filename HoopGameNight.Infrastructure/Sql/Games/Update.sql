UPDATE games SET
    home_team_score = @HomeTeamScore,
    visitor_team_score = @VisitorTeamScore,
    status = @Status,
    period = @Period,
    time_remaining = @TimeRemaining,
    ai_summary = @AiSummary,
    ai_highlights = @AiHighlights,
    updated_at = @UpdatedAt
WHERE id = @Id;