UPDATE players SET
    first_name = @FirstName,
    last_name = @LastName,
    position = @Position,
    height_feet = @HeightFeet,
    height_inches = @HeightInches,
    weight_pounds = @WeightPounds,
    team_id = @TeamId,
    updated_at = @UpdatedAt
WHERE id = @Id;