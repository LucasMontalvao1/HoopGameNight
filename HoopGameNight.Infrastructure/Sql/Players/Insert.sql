INSERT INTO players (
    external_id,
    first_name,
    last_name,
    position,
    height_feet,
    height_inches,
    weight_pounds,
    team_id,
    nba_stats_id,
    espn_id,
    birth_date,
    jersey_number
) VALUES (
    @ExternalId,
    @FirstName,
    @LastName,
    @Position,
    @HeightFeet,
    @HeightInches,
    @WeightPounds,
    @TeamId,
    @NbaStatsId,
    @EspnId,
    @BirthDate,
    @JerseyNumber
);

SELECT LAST_INSERT_ID();