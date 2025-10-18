SELECT
    p.id,
    p.external_id,
    p.nba_stats_id,
    p.espn_id,
    p.first_name,
    p.last_name,
    p.position,
    p.height_feet,
    p.height_inches,
    p.weight_pounds,
    p.team_id,
    p.created_at,
    p.updated_at
FROM players p
WHERE p.team_id = @TeamId
ORDER BY p.last_name, p.first_name
LIMIT @PageSize OFFSET @Offset;