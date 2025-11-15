INSERT INTO teams (
    external_id,
    external_espn_id,
    name,
    full_name,
    abbreviation,
    city,
    conference,
    division
) VALUES (
    @ExternalId,
    @EspnId,
    @Name,
    @FullName,
    @Abbreviation,
    @City,
    @Conference,
    @Division
);

SELECT LAST_INSERT_ID();