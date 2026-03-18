INSERT INTO games (
    external_id, date, datetime,
    home_team_id, visitor_team_id,
    home_team_score, visitor_team_score,
    status, period, time_remaining,
    postseason, season,
    ai_summary, ai_highlights,
    line_score_json, game_leaders_json,
    created_at, updated_at
) VALUES (
    @ExternalId, @Date, @DateTime,
    @HomeTeamId, @VisitorTeamId,
    @HomeTeamScore, @VisitorTeamScore,
    @Status, @Period, @TimeRemaining,
    @PostSeason, @Season,
    @AiSummary, @AiHighlights,
    @LineScoreJson, @GameLeadersJson,
    NOW(), NOW()
)
ON DUPLICATE KEY UPDATE
    home_team_score = VALUES(home_team_score),
    visitor_team_score = VALUES(visitor_team_score),
    status = VALUES(status),
    period = VALUES(period),
    time_remaining = VALUES(time_remaining),
    line_score_json = VALUES(line_score_json),
    game_leaders_json = VALUES(game_leaders_json),
    updated_at = NOW();

SELECT LAST_INSERT_ID();