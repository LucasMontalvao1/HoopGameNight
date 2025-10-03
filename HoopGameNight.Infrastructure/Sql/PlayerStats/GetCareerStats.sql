SELECT 
    pc.*
FROM player_career_stats pc
WHERE pc.player_id = @PlayerId;