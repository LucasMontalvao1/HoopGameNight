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
    ht.id as HomeTeam_Id,
    ht.external_id as HomeTeam_ExternalId,
    ht.name as HomeTeam_Name,
    ht.full_name as HomeTeam_FullName,
    ht.abbreviation as HomeTeam_Abbreviation,
    ht.city as HomeTeam_City,
    ht.conference as HomeTeam_Conference,
    ht.division as HomeTeam_Division,
    ht.created_at as HomeTeam_CreatedAt,
    ht.updated_at as HomeTeam_UpdatedAt,
    -- Visitor Team
    vt.id as VisitorTeam_Id,
    vt.external_id as VisitorTeam_ExternalId,
    vt.name as VisitorTeam_Name,
    vt.full_name as VisitorTeam_FullName,
    vt.abbreviation as VisitorTeam_Abbreviation,
    vt.city as VisitorTeam_City,
    vt.conference as VisitorTeam_Conference,
    vt.division as VisitorTeam_Division,
    vt.created_at as VisitorTeam_CreatedAt,
    vt.updated_at as VisitorTeam_UpdatedAt
FROM games g
LEFT JOIN teams ht ON g.home_team_id = ht.id
LEFT JOIN teams vt ON g.visitor_team_id = vt.id
WHERE DATE(g.date) = @Date
ORDER BY g.date;