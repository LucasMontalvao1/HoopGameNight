INSERT INTO teams (
    external_id,
    name,
    full_name,
    abbreviation,
    city,
    conference,
    division
) VALUES (
    @ExternalId,
    @Name,
    @FullName,
    @Abbreviation,
    @City,
    @Conference,
    @Division
);

SELECT LAST_INSERT_ID();