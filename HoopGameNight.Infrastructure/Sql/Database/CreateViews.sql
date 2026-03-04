DROP VIEW IF EXISTS vw_player_game_stats_detailed;

CREATE VIEW vw_player_game_stats_detailed AS
SELECT 
    pgs.player_id,
    pgs.game_id,
    pgs.team_id,
    
    p.first_name AS player_first_name,
    p.last_name AS player_last_name,
    CONCAT(p.first_name, ' ', p.last_name) AS player_full_name,
    p.jersey_number,
    p.position,
    
    g.date AS game_date,
    g.home_team_id,
    g.visitor_team_id,
    g.home_team_score,
    g.visitor_team_score,
    g.status AS game_status,
    
    t.abbreviation AS team_abbreviation,
    t.full_name AS team_name,
    
    CASE 
        WHEN pgs.team_id = g.home_team_id THEN vt.abbreviation
        ELSE ht.abbreviation
    END AS opponent_abbreviation,
    CASE 
        WHEN pgs.team_id = g.home_team_id THEN vt.full_name
        ELSE ht.full_name
    END AS opponent_name,
    
    CASE 
        WHEN pgs.team_id = g.home_team_id THEN
            CASE 
                WHEN g.home_team_score > g.visitor_team_score THEN 'W'
                WHEN g.home_team_score < g.visitor_team_score THEN 'L'
                ELSE 'T'
            END
        ELSE
            CASE 
                WHEN g.visitor_team_score > g.home_team_score THEN 'W'
                WHEN g.visitor_team_score < g.home_team_score THEN 'L'
                ELSE 'T'
            END
    END AS result,
    
    pgs.did_not_play,
    pgs.is_starter,
    (pgs.team_id = g.home_team_id) AS is_home,
    
    pgs.minutes_played,
    pgs.seconds_played,
    CONCAT(pgs.minutes_played, ':', LPAD(pgs.seconds_played, 2, '0')) AS minutes_formatted,
    
    pgs.points,
    pgs.field_goals_made,
    pgs.field_goals_attempted,
    CONCAT(pgs.field_goals_made, '/', pgs.field_goals_attempted) AS field_goals_formatted,
    CASE 
        WHEN pgs.field_goals_attempted > 0 
        THEN ROUND((pgs.field_goals_made / pgs.field_goals_attempted) * 100, 1)
        ELSE 0 
    END AS field_goal_percentage,
    
    pgs.three_pointers_made,
    pgs.three_pointers_attempted,
    CONCAT(pgs.three_pointers_made, '/', pgs.three_pointers_attempted) AS three_pointers_formatted,
    CASE 
        WHEN pgs.three_pointers_attempted > 0 
        THEN ROUND((pgs.three_pointers_made / pgs.three_pointers_attempted) * 100, 1)
        ELSE 0 
    END AS three_point_percentage,
    
    pgs.free_throws_made,
    pgs.free_throws_attempted,
    CONCAT(pgs.free_throws_made, '/', pgs.free_throws_attempted) AS free_throws_formatted,
    CASE 
        WHEN pgs.free_throws_attempted > 0 
        THEN ROUND((pgs.free_throws_made / pgs.free_throws_attempted) * 100, 1)
        ELSE 0 
    END AS free_throw_percentage,
    
    pgs.offensive_rebounds,
    pgs.defensive_rebounds,
    pgs.total_rebounds,
    pgs.assists,
    pgs.steals,
    pgs.blocks,
    pgs.turnovers,
    pgs.personal_fouls,
    pgs.plus_minus,
    
    (
        (CASE WHEN pgs.points >= 10 THEN 1 ELSE 0 END) +
        (CASE WHEN pgs.total_rebounds >= 10 THEN 1 ELSE 0 END) +
        (CASE WHEN pgs.assists >= 10 THEN 1 ELSE 0 END) +
        (CASE WHEN pgs.steals >= 10 THEN 1 ELSE 0 END) +
        (CASE WHEN pgs.blocks >= 10 THEN 1 ELSE 0 END)
    ) >= 2 AS double_double,
    
    (
        (CASE WHEN pgs.points >= 10 THEN 1 ELSE 0 END) +
        (CASE WHEN pgs.total_rebounds >= 10 THEN 1 ELSE 0 END) +
        (CASE WHEN pgs.assists >= 10 THEN 1 ELSE 0 END) +
        (CASE WHEN pgs.steals >= 10 THEN 1 ELSE 0 END) +
        (CASE WHEN pgs.blocks >= 10 THEN 1 ELSE 0 END)
    ) >= 3 AS triple_double,
    
    pgs.created_at,
    pgs.updated_at

FROM player_game_stats pgs
INNER JOIN players p ON pgs.player_id = p.id
INNER JOIN games g ON pgs.game_id = g.id
INNER JOIN teams t ON pgs.team_id = t.id
LEFT JOIN teams ht ON g.home_team_id = ht.id
LEFT JOIN teams vt ON g.visitor_team_id = vt.id;


