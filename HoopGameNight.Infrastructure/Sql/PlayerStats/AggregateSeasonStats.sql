-- =============================================
-- Script: AggregateSeasonStats.sql
-- Desc: Calculates season totals by summing player_game_stats
--       and upserts into player_season_stats.
-- =============================================

INSERT INTO player_season_stats (
    player_id, 
    season, 
    season_type_id, 
    team_id, 
    games_played,
    games_started,
    minutes_played,
    points,
    field_goals_made,
    field_goals_attempted,
    three_pointers_made,
    three_pointers_attempted,
    free_throws_made,
    free_throws_attempted,
    offensive_rebounds,
    defensive_rebounds,
    total_rebounds,
    assists,
    steals,
    blocks,
    turnovers,
    personal_fouls,
    avg_points,
    avg_rebounds,
    avg_assists,
    avg_minutes,
    field_goal_percentage,
    three_point_percentage,
    free_throw_percentage,
    created_at,
    updated_at
)
SELECT 
    pgs.player_id,
    g.season,
    @SeasonTypeId,
    MAX(pgs.team_id) as team_id, -- Simplification: use the last team for season stats
    COUNT(pgs.id) as games_played,
    SUM(pgs.is_starter) as games_started,
    SUM(pgs.minutes_played) as minutes_played,
    SUM(pgs.points) as points,
    SUM(pgs.field_goals_made) as field_goals_made,
    SUM(pgs.field_goals_attempted) as field_goals_attempted,
    SUM(pgs.three_pointers_made) as three_pointers_made,
    SUM(pgs.three_pointers_attempted) as three_pointers_attempted,
    SUM(pgs.free_throws_made) as free_throws_made,
    SUM(pgs.free_throws_attempted) as free_throws_attempted,
    SUM(pgs.offensive_rebounds) as offensive_rebounds,
    SUM(pgs.defensive_rebounds) as defensive_rebounds,
    SUM(pgs.total_rebounds) as total_rebounds,
    SUM(pgs.assists) as assists,
    SUM(pgs.steals) as steals,
    SUM(pgs.blocks) as blocks,
    SUM(pgs.turnovers) as turnovers,
    SUM(pgs.personal_fouls) as personal_fouls,
    -- Averages
    SUM(pgs.points) / NULLIF(COUNT(pgs.id), 0) as avg_points,
    SUM(pgs.total_rebounds) / NULLIF(COUNT(pgs.id), 0) as avg_rebounds,
    SUM(pgs.assists) / NULLIF(COUNT(pgs.id), 0) as avg_assists,
    SUM(pgs.minutes_played) / NULLIF(COUNT(pgs.id), 0) as avg_minutes,
    -- Percentages
    SUM(pgs.field_goals_made) / NULLIF(SUM(pgs.field_goals_attempted), 0) as field_goal_percentage,
    SUM(pgs.three_pointers_made) / NULLIF(SUM(pgs.three_pointers_attempted), 0) as three_point_percentage,
    SUM(pgs.free_throws_made) / NULLIF(SUM(pgs.free_throws_attempted), 0) as free_throw_percentage,
    NOW(),
    NOW()
FROM player_game_stats pgs
JOIN games g ON pgs.game_id = g.id
WHERE pgs.player_id = @PlayerId 
  AND g.season = @Season 
  AND g.postseason = CASE WHEN @SeasonTypeId = 3 THEN 1 ELSE 0 END
GROUP BY pgs.player_id, g.season
ON DUPLICATE KEY UPDATE
    team_id = VALUES(team_id),
    games_played = VALUES(games_played),
    games_started = VALUES(games_started),
    minutes_played = VALUES(minutes_played),
    points = VALUES(points),
    field_goals_made = VALUES(field_goals_made),
    field_goals_attempted = VALUES(field_goals_attempted),
    three_pointers_made = VALUES(three_pointers_made),
    three_pointers_attempted = VALUES(three_pointers_attempted),
    free_throws_made = VALUES(free_throws_made),
    free_throws_attempted = VALUES(free_throws_attempted),
    offensive_rebounds = VALUES(offensive_rebounds),
    defensive_rebounds = VALUES(defensive_rebounds),
    total_rebounds = VALUES(total_rebounds),
    assists = VALUES(assists),
    steals = VALUES(steals),
    blocks = VALUES(blocks),
    turnovers = VALUES(turnovers),
    personal_fouls = VALUES(personal_fouls),
    avg_points = VALUES(avg_points),
    avg_rebounds = VALUES(avg_rebounds),
    avg_assists = VALUES(avg_assists),
    avg_minutes = VALUES(avg_minutes),
    field_goal_percentage = VALUES(field_goal_percentage),
    three_point_percentage = VALUES(three_point_percentage),
    free_throw_percentage = VALUES(free_throw_percentage),
    updated_at = NOW();
