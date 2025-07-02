SELECT COUNT(*) 
FROM games 
WHERE home_team_id = @TeamId OR visitor_team_id = @TeamId;