UPDATE teams SET
    external_espn_id = @EspnId,
    name = @Name,
    full_name = @FullName,
    abbreviation = @Abbreviation,
    city = @City,
    conference = @Conference,
    division = @Division,
    updated_at = @UpdatedAt
WHERE id = @Id;