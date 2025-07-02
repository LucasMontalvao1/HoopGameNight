SELECT COUNT(*)
FROM games g
WHERE 
    (@Date IS NULL OR DATE(g.date) = DATE(@Date))
    AND (@StartDate IS NULL OR g.date >= @StartDate)
    AND (@EndDate IS NULL OR g.date <= @EndDate)
    AND (@TeamId IS NULL OR g.home_team_id = @TeamId OR g.visitor_team_id = @TeamId)
    AND (@Status IS NULL OR @Status = '' OR g.status = @Status)
    AND (@PostSeason IS NULL OR g.postseason = @PostSeason)
    AND (@Season IS NULL OR g.season = @Season);