SELECT MAX(updated_at) as LastSyncDate
FROM (
    SELECT MAX(updated_at) as updated_at FROM player_season_stats WHERE player_id = @PlayerId
    UNION ALL
    SELECT MAX(updated_at) as updated_at FROM player_game_stats WHERE player_id = @PlayerId
    UNION ALL
    SELECT MAX(updated_at) as updated_at FROM player_career_stats WHERE player_id = @PlayerId
) AS sync_dates;
