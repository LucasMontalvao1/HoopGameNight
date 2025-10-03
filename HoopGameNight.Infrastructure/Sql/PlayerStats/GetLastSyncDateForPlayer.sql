SELECT MAX(UpdatedAt) as LastSyncDate
FROM (
    SELECT MAX(UpdatedAt) as UpdatedAt FROM PlayerSeasonStats WHERE PlayerId = @PlayerId
    UNION ALL
    SELECT MAX(UpdatedAt) as UpdatedAt FROM PlayerGameStats WHERE PlayerId = @PlayerId
    UNION ALL
    SELECT MAX(UpdatedAt) as UpdatedAt FROM PlayerCareerStats WHERE PlayerId = @PlayerId
) as sync_dates;
