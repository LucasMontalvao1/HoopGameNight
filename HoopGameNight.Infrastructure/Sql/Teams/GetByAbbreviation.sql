SELECT
    id,
    external_id,
    name,
    full_name,
    abbreviation,
    city,
    conference,
    division,
    created_at,
    updated_at
FROM teams
WHERE UPPER(TRIM(abbreviation)) = UPPER(TRIM(@Abbreviation))
ORDER BY external_id DESC
LIMIT 1;