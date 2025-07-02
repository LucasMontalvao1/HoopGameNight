UPDATE teams SET
    name = @Name,
    full_name = @FullName,
    abbreviation = @Abbreviation,
    city = @City,
    conference = @Conference,
    division = @Division,
    updated_at = @UpdatedAt
WHERE id = @Id;