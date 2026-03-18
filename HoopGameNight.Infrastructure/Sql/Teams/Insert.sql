INSERT INTO teams (
    external_id,
    external_espn_id,
    name,
    full_name,
    abbreviation,
    city,
    conference,
    division,
    wins,
    losses
) VALUES (
    @ExternalId,
    @EspnId,
    @Name,
    @FullName,
    @Abbreviation,
    @City,
    @Conference,
    @Division,
    @Wins,
    @Losses
);

SELECT LAST_INSERT_ID();