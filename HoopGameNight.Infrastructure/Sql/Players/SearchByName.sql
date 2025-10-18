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
    p.updated_at,
    t.id AS TeamId,
    t.external_id AS TeamExternalId,
    t.name AS TeamName,
    t.full_name AS TeamFullName,
    t.abbreviation AS TeamAbbreviation,
    t.city AS TeamCity,
    t.conference AS TeamConference,
    t.division AS TeamDivision
FROM players p
LEFT JOIN teams t ON p.team_id = t.id
WHERE
    (@Search IS NULL OR @Search = '' OR
     CONCAT(p.first_name, ' ', p.last_name) LIKE CONCAT('%', @Search, '%') OR
     p.first_name LIKE CONCAT('%', @Search, '%') OR
     p.last_name LIKE CONCAT('%', @Search, '%'))
    AND (@TeamId IS NULL OR p.team_id = @TeamId)
    AND (@Position IS NULL OR @Position = '' OR UPPER(p.position) = UPPER(@Position))
ORDER BY p.last_name, p.first_name
LIMIT @PageSize OFFSET @Offset;