SELECT 
    p.id,
    p.external_id,
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
WHERE (p.height_feet * 12 + COALESCE(p.height_inches, 0)) BETWEEN @MinHeightInches AND @MaxHeightInches
ORDER BY (p.height_feet * 12 + COALESCE(p.height_inches, 0)) DESC;