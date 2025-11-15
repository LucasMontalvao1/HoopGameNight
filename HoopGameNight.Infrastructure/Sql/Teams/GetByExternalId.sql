SELECT
    id,
    external_id,
    external_espn_id,
    name,
    full_name,
    abbreviation,
    city,
    conference,
    division,
    created_at,
    updated_at
FROM teams
WHERE external_id = @ExternalId;