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
ORDER BY conference, division, city;