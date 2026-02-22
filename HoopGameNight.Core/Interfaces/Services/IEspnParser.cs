using HoopGameNight.Core.DTOs.External;
using HoopGameNight.Core.DTOs.External.ESPN;
using HoopGameNight.Core.Enums;
using HoopGameNight.Core.Models.Entities;
using System.Text.Json;

namespace HoopGameNight.Core.Interfaces.Services
{
    public interface IEspnParser
    {
        PlayerGameStats ParseBoxscoreToGameStats(
            EspnAthleteStatDto statCategory,
            EspnAthleteEntryDto athleteEntry,
            int playerId,
            int gameId,
            int teamId);

        void MergeGameStats(PlayerGameStats existing, PlayerGameStats current);
        PlayerPosition? ParsePosition(string? pos);
        string ExtractIdFromRef(string? reference);
        int SafeParseInt(string? value);
        decimal SafeParseDecimal(string? value);
        int? ParseScore(JsonElement? scoreElement);
        GameStatus MapGameStatus(string? externalStatus);
        
        List<EspnGameDto> ParseScoreboardResponse(string json);
        EspnPlayerDetailsDto? ParsePlayerFromRoster(JsonElement item);
        EspnPlayerStatsDto? ParsePlayerGameStatsFromBoxscore(EspnBoxscoreDto boxscore, string playerId, string gameId);
        PlayerSeasonStats ParseSeasonStats(EspnPlayerStatsDto espnStats, int playerId);
    }
}
