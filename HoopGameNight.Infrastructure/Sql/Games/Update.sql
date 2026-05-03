UPDATE games SET
    datetime = @DateTime,
    home_team_score = @HomeTeamScore,
    visitor_team_score = @VisitorTeamScore,
    status = @Status,
    period = @Period,
    time_remaining = @TimeRemaining,
    ai_summary = @AiSummary,
    ai_highlights = @AiHighlights,
    line_score_json = @LineScoreJson,
    game_leaders_json = @GameLeadersJson,
    series_note = @SeriesNote,
    updated_at = @UpdatedAt
WHERE id = @Id;