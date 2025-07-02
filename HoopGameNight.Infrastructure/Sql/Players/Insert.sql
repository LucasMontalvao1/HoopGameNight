INSERT INTO players (
    external_id,
    first_name,
    last_name,
    position,
    height_feet,
    height_inches,
    weight_pounds,
    team_id
) VALUES (
    @ExternalId,
    @FirstName,
    @LastName,
    @Position,
    @HeightFeet,
    @HeightInches,
    @WeightPounds,
    @TeamId
);

SELECT LAST_INSERT_ID();