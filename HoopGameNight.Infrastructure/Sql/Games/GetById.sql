SELECT
    g.id,
    g.external_id,
    g.date,
    g.datetime,
    g.home_team_id,
    g.visitor_team_id,
    g.home_team_score,
    g.visitor_team_score,
    g.status,
    g.period,
    g.time_remaining,
    g.postseason,
    g.season,
    g.created_at,
    g.updated_at,
    -- Home Team
    ht.id,
    ht.external_id,
    ht.name,
    ht.full_name,
    ht.abbreviation,
    ht.city,
    ht.conference,
    ht.division,
    ht.created_at,
    ht.updated_at,
    -- Visitor Team
    vt.id,
    vt.external_id,
    vt.name,
    vt.full_name,
    vt.abbreviation,
    vt.city,
    vt.conference,
    vt.division,
    vt.created_at,
    vt.updated_at
FROM games g
LEFT JOIN teams ht ON g.home_team_id = ht.id
LEFT JOIN teams vt ON g.visitor_team_id = vt.id
WHERE g.id = @Id;