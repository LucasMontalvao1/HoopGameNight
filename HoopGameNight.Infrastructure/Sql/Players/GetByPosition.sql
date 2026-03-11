SELECT 
    p.id,
    p.external_id AS ExternalId,
    p.first_name AS FirstName,
    p.last_name AS LastName,
    p.position,
    p.height_feet AS HeightFeet,
    p.height_inches AS HeightInches,
    p.weight_pounds AS WeightPounds,
    p.team_id AS TeamId,
    p.created_at AS CreatedAt,
    p.updated_at AS UpdatedAt
FROM players p
WHERE UPPER(p.position) = UPPER(@Position)
ORDER BY p.last_name, p.first_name;
