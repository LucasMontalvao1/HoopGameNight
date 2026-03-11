SELECT 
    id,
    external_id AS ExternalId,
    nba_stats_id AS NbaStatsId,
    espn_id AS EspnId,
    first_name AS FirstName,
    last_name AS LastName,
    position,
    height_feet AS HeightFeet,
    height_inches AS HeightInches,
    weight_pounds AS WeightPounds,
    team_id AS TeamId,
    created_at AS CreatedAt,
    updated_at AS UpdatedAt
FROM Players 
WHERE ExternalId = @ExternalId
LIMIT 1;