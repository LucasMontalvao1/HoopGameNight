INSERT INTO player_game_stats (
    player_id, game_id, team_id, did_not_play, is_starter,
    minutes_played, seconds_played, points,
    field_goals_made, field_goals_attempted,
    three_pointers_made, three_pointers_attempted,
    free_throws_made, free_throws_attempted,
    offensive_rebounds, defensive_rebounds, total_rebounds,
    assists, steals, blocks, turnovers, personal_fouls, plus_minus,
    created_at, updated_at
) VALUES (
    @PlayerId, @GameId, @TeamId, @DidNotPlay, @IsStarter,
    @MinutesPlayed, @SecondsPlayed, @Points,
    @FieldGoalsMade, @FieldGoalsAttempted,
    @ThreePointersMade, @ThreePointersAttempted,
    @FreeThrowsMade, @FreeThrowsAttempted,
    @OffensiveRebounds, @DefensiveRebounds, @TotalRebounds,
    @Assists, @Steals, @Blocks, @Turnovers, @PersonalFouls, @PlusMinus,
    NOW(), NOW()
);
SELECT LAST_INSERT_ID();INSERT INTO player_game_stats (
    player_id, game_id, team_id, did_not_play, is_starter,
    minutes_played, seconds_played, points,
    field_goals_made, field_goals_attempted,
    three_pointers_made, three_pointers_attempted,
    free_throws_made, free_throws_attempted,
    offensive_rebounds, defensive_rebounds, total_rebounds,
    assists, steals, blocks, turnovers, personal_fouls, plus_minus,
    created_at, updated_at
) VALUES (
    @PlayerId, @GameId, @TeamId, @DidNotPlay, @IsStarter,
    @MinutesPlayed, @SecondsPlayed, @Points,
    @FieldGoalsMade, @FieldGoalsAttempted,
    @ThreePointersMade, @ThreePointersAttempted,
    @FreeThrowsMade, @FreeThrowsAttempted,
    @OffensiveRebounds, @DefensiveRebounds, @TotalRebounds,
    @Assists, @Steals, @Blocks, @Turnovers, @PersonalFouls, @PlusMinus,
    NOW(), NOW()
);
SELECT LAST_INSERT_ID();