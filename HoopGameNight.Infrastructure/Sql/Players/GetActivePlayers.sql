SELECT
    p.id,
    p.external_id AS ExternalId,
    p.nba_stats_id AS NbaStatsId,
    p.espn_id AS EspnId,
    p.first_name AS FirstName,
    p.last_name AS LastName,
    p.position,
    p.height_feet AS HeightFeet,
    p.height_inches AS HeightInches,
    p.weight_pounds AS WeightPounds,
    p.team_id AS TeamId,
    p.birth_date AS BirthDate,
    p.jersey_number AS JerseyNumber,
    p.created_at AS CreatedAt,
    p.updated_at AS UpdatedAt
FROM players p
WHERE p.is_active = 1
ORDER BY p.last_name, p.first_name;
