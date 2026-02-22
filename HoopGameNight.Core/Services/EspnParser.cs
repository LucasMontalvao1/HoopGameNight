using HoopGameNight.Core.DTOs.External;
using HoopGameNight.Core.DTOs.External.ESPN;
using HoopGameNight.Core.Interfaces.Services;
using HoopGameNight.Core.Enums;
using HoopGameNight.Core.Models.Entities;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace HoopGameNight.Core.Services
{
    public class EspnParser : IEspnParser
    {
        private readonly ILogger<EspnParser> _logger;

        public EspnParser(ILogger<EspnParser> logger)
        {
            _logger = logger;
        }

        public PlayerGameStats ParseBoxscoreToGameStats(
            EspnAthleteStatDto statCategory,
            EspnAthleteEntryDto athleteEntry,
            int playerId,
            int gameId,
            int teamId)
        {
            var stats = new PlayerGameStats
            {
                PlayerId = playerId,
                GameId = gameId,
                TeamId = teamId,
                DidNotPlay = false,
                IsStarter = false
            };

            if (statCategory.Names == null || athleteEntry.Stats == null) return stats;

            for (int i = 0; i < statCategory.Names.Count && i < athleteEntry.Stats.Count; i++)
            {
                var statName = statCategory.Names[i]?.ToLowerInvariant() ?? "";
                var statValue = athleteEntry.Stats[i] ?? "0";

                try
                {
                    switch (statName)
                    {
                        case "min":
                        case "minutes":
                            if (statValue.Contains(":"))
                            {
                                var parts = statValue.Split(':');
                                if (parts.Length == 2)
                                {
                                    stats.MinutesPlayed = SafeParseInt(parts[0]);
                                    stats.SecondsPlayed = SafeParseInt(parts[1]);
                                }
                            }
                            else
                            {
                                stats.MinutesPlayed = SafeParseInt(statValue);
                            }
                            break;

                        case "pts": case "points": stats.Points = SafeParseInt(statValue); break;
                        
                        case "fg": case "fgm/fga": case "fgm-fga":
                            var fgParts = statValue.Split(new[] { '-', '/' });
                            if (fgParts.Length == 2)
                            {
                                stats.FieldGoalsMade = SafeParseInt(fgParts[0]);
                                stats.FieldGoalsAttempted = SafeParseInt(fgParts[1]);
                            }
                            break;
                        case "fgm": stats.FieldGoalsMade = SafeParseInt(statValue); break;
                        case "fga": stats.FieldGoalsAttempted = SafeParseInt(statValue); break;

                        case "3pt": case "3pm/3pa": case "3pm-3pa":
                            var tpParts = statValue.Split(new[] { '-', '/' });
                            if (tpParts.Length == 2)
                            {
                                stats.ThreePointersMade = SafeParseInt(tpParts[0]);
                                stats.ThreePointersAttempted = SafeParseInt(tpParts[1]);
                            }
                            break;
                        case "3pm": stats.ThreePointersMade = SafeParseInt(statValue); break;
                        case "3pa": stats.ThreePointersAttempted = SafeParseInt(statValue); break;

                        case "ft": case "ftm/fta": case "ftm-fta":
                            var ftParts = statValue.Split(new[] { '-', '/' });
                            if (ftParts.Length == 2)
                            {
                                stats.FreeThrowsMade = SafeParseInt(ftParts[0]);
                                stats.FreeThrowsAttempted = SafeParseInt(ftParts[1]);
                            }
                            break;
                        case "ftm": stats.FreeThrowsMade = SafeParseInt(statValue); break;
                        case "fta": stats.FreeThrowsAttempted = SafeParseInt(statValue); break;

                        case "oreb": stats.OffensiveRebounds = SafeParseInt(statValue); break;
                        case "dreb": stats.DefensiveRebounds = SafeParseInt(statValue); break;
                        case "reb": case "totalrebounds": stats.TotalRebounds = SafeParseInt(statValue); break;
                        case "ast": case "assists": stats.Assists = SafeParseInt(statValue); break;
                        case "stl": case "steals": stats.Steals = SafeParseInt(statValue); break;
                        case "blk": case "blocks": stats.Blocks = SafeParseInt(statValue); break;
                        case "to": case "turnovers": stats.Turnovers = SafeParseInt(statValue); break;
                        case "pf": case "fouls": stats.PersonalFouls = SafeParseInt(statValue); break;
                        case "+/-": case "plusminus": stats.PlusMinus = SafeParseInt(statValue); break;
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogDebug("Error parsing stat {StatName} with value {StatValue}: {Message}", statName, statValue, ex.Message);
                }
            }

            return stats;
        }

        public void MergeGameStats(PlayerGameStats existing, PlayerGameStats current)
        {
            existing.MinutesPlayed += current.MinutesPlayed;
            existing.SecondsPlayed += current.SecondsPlayed;
            existing.Points = Math.Max(existing.Points, current.Points);
            existing.Assists = Math.Max(existing.Assists, current.Assists);
            existing.TotalRebounds = Math.Max(existing.TotalRebounds, current.TotalRebounds);
            existing.OffensiveRebounds = Math.Max(existing.OffensiveRebounds, current.OffensiveRebounds);
            existing.DefensiveRebounds = Math.Max(existing.DefensiveRebounds, current.DefensiveRebounds);
            existing.Steals = Math.Max(existing.Steals, current.Steals);
            existing.Blocks = Math.Max(existing.Blocks, current.Blocks);
            existing.Turnovers = Math.Max(existing.Turnovers, current.Turnovers);
            existing.PersonalFouls = Math.Max(existing.PersonalFouls, current.PersonalFouls);
            existing.PlusMinus = existing.PlusMinus != 0 ? existing.PlusMinus : current.PlusMinus;
            
            if (existing.FieldGoalsMade == 0) existing.FieldGoalsMade = current.FieldGoalsMade;
            if (existing.FieldGoalsAttempted == 0) existing.FieldGoalsAttempted = current.FieldGoalsAttempted;
            if (existing.ThreePointersMade == 0) existing.ThreePointersMade = current.ThreePointersMade;
            if (existing.ThreePointersAttempted == 0) existing.ThreePointersAttempted = current.ThreePointersAttempted;
            if (existing.FreeThrowsMade == 0) existing.FreeThrowsMade = current.FreeThrowsMade;
            if (existing.FreeThrowsAttempted == 0) existing.FreeThrowsAttempted = current.FreeThrowsAttempted;
            
            existing.IsStarter = existing.IsStarter || current.IsStarter;
            existing.DidNotPlay = existing.DidNotPlay && current.DidNotPlay;
        }

        public List<EspnGameDto> ParseScoreboardResponse(string json)
        {
            var games = new List<EspnGameDto>();
            try
            {
                using var document = JsonDocument.Parse(json);
                var root = document.RootElement;

                if (root.TryGetProperty("events", out var events))
                {
                    foreach (var eventElement in events.EnumerateArray())
                    {
                        var game = ParseGameEvent(eventElement);
                        if (game != null) games.Add(game);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing ESPN scoreboard response");
            }
            return games;
        }

        public EspnPlayerDetailsDto? ParsePlayerFromRoster(JsonElement item)
        {
            try
            {
                var player = new EspnPlayerDetailsDto
                {
                    Id = GetPropertySafe(item, "id"),
                    Uid = GetPropertySafe(item, "uid"),
                    FirstName = GetPropertySafe(item, "firstName"),
                    LastName = GetPropertySafe(item, "lastName"),
                    FullName = GetPropertySafe(item, "fullName"),
                    DisplayName = GetPropertySafe(item, "displayName"),
                    DisplayWeight = GetPropertySafe(item, "displayWeight"),
                    DisplayHeight = GetPropertySafe(item, "displayHeight"),
                    DateOfBirth = GetPropertySafe(item, "dateOfBirth"),
                    Jersey = GetPropertySafe(item, "jersey")
                };

                if (item.TryGetProperty("weight", out var weight))
                    player.Weight = weight.TryGetDouble(out var w) ? w : 0;

                if (item.TryGetProperty("height", out var height))
                    player.Height = height.TryGetDouble(out var h) ? h : 0;

                if (item.TryGetProperty("age", out var age))
                    player.Age = age.TryGetInt32(out var a) ? a : 0;

                if (item.TryGetProperty("position", out var position))
                {
                    player.Position = new EspnPositionDto
                    {
                        Id = GetPropertySafe(position, "id"),
                        Name = GetPropertySafe(position, "name"),
                        DisplayName = GetPropertySafe(position, "displayName"),
                        Abbreviation = GetPropertySafe(position, "abbreviation")
                    };
                }

                if (item.TryGetProperty("active", out var active))
                    player.Active = active.GetBoolean();

                return player;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error parsing player from roster");
                return null;
            }
        }

        public EspnPlayerStatsDto? ParsePlayerGameStatsFromBoxscore(EspnBoxscoreDto boxscore, string playerId, string gameId)
        {
            if (boxscore?.Players == null) return null;

            foreach (var teamGroup in boxscore.Players)
            {
                if (teamGroup.Statistics == null) continue;

                foreach (var statGroup in teamGroup.Statistics)
                {
                    if (statGroup.Athletes == null || statGroup.Keys == null) continue;

                    var athleteEntry = statGroup.Athletes.FirstOrDefault(a => a.Athlete?.Id == playerId);
                    if (athleteEntry != null && athleteEntry.Stats != null)
                    {
                        var result = new EspnPlayerStatsDto
                        {
                            Athlete = athleteEntry.Athlete ?? new EspnAthleteRefDto { Id = playerId },
                            Team = teamGroup.Team,
                            Splits = new EspnPlayerStatsSplitDto
                            {
                                Categories = new List<EspnStatsCategoryDto>
                                {
                                    new EspnStatsCategoryDto
                                    {
                                        Name = "all",
                                        Stats = new List<EspnStatDto>()
                                    }
                                }
                            }
                        };

                        var targetStats = result.Splits.Categories[0].Stats;

                        for (int i = 0; i < statGroup.Keys.Count && i < athleteEntry.Stats.Count; i++)
                        {
                            var key = statGroup.Keys[i];
                            var valStr = athleteEntry.Stats[i];
                            ParseAndAddStat(targetStats, key, valStr);
                        }

                        return result;
                    }
                }
            }
            return null;
        }

        public PlayerSeasonStats ParseSeasonStats(EspnPlayerStatsDto espnStats, int playerId)
        {
            var stats = new PlayerSeasonStats
            {
                PlayerId = playerId,
                Season = espnStats.Season?.Year ?? 0,
                SeasonTypeId = espnStats.SeasonTypeId,
                TeamId = !string.IsNullOrEmpty(espnStats.Team?.Id) && int.TryParse(espnStats.Team.Id, out var tid) ? tid : (int?)null
            };

            if (espnStats.Splits?.Categories == null) return stats;

            foreach (var category in espnStats.Splits.Categories)
            {
                if (category.Stats == null) continue;

                foreach (var stat in category.Stats)
                {
                    var name = stat.Name?.ToLowerInvariant();
                    var val = (decimal)stat.Value;

                    switch (name)
                    {
                        case "gamesplayed": case "gp": stats.GamesPlayed = (int)val; break;
                        case "gamesstarted": case "gs": stats.GamesStarted = (int)val; break;
                        case "minutes": case "min": stats.MinutesPlayed = val; break;
                        case "points": case "pts": stats.Points = (int)val; break;
                        case "fieldgoalsmade": case "fgm": stats.FieldGoalsMade = (int)val; break;
                        case "fieldgoalsattempted": case "fga": stats.FieldGoalsAttempted = (int)val; break;
                        case "fieldgoalpercentage": case "fg%": stats.FieldGoalPercentage = val; break;
                        case "threepointfieldgoalsmade": case "3pm": stats.ThreePointersMade = (int)val; break;
                        case "threepointfieldgoalsattempted": case "3pa": stats.ThreePointersAttempted = (int)val; break;
                        case "threepointfieldgoalpercentage": case "3p%": stats.ThreePointPercentage = val; break;
                        case "freethrowsmade": case "ftm": stats.FreeThrowsMade = (int)val; break;
                        case "freethrowsattempted": case "fta": stats.FreeThrowsAttempted = (int)val; break;
                        case "freethrowpercentage": case "ft%": stats.FreeThrowPercentage = val; break;
                        case "offensiverebounds": case "oreb": stats.OffensiveRebounds = (int)val; break;
                        case "defensiverebounds": case "dreb": stats.DefensiveRebounds = (int)val; break;
                        case "totalrebounds": case "reb": stats.TotalRebounds = (int)val; break;
                        case "assists": case "ast": stats.Assists = (int)val; break;
                        case "steals": case "stl": stats.Steals = (int)val; break;
                        case "blocks": case "blk": stats.Blocks = (int)val; break;
                        case "turnovers": case "to": stats.Turnovers = (int)val; break;
                        case "personalfouls": case "pf": stats.PersonalFouls = (int)val; break;
                        case "avgpoints": case "ppg": stats.AvgPoints = val; break;
                        case "avgrebounds": case "rpg": stats.AvgRebounds = val; break;
                        case "avgassists": case "apg": stats.AvgAssists = val; break;
                        case "avgminutes": case "mpg": stats.AvgMinutes = val; break;
                    }
                }
            }

            return stats;
        }

        private EspnGameDto? ParseGameEvent(JsonElement eventElement)
        {
            try
            {
                var game = new EspnGameDto
                {
                    Id = eventElement.GetProperty("id").GetString() ?? ""
                };

                if (eventElement.TryGetProperty("date", out var dateElement))
                {
                    if (dateElement.ValueKind == JsonValueKind.String)
                    {
                        var dateString = dateElement.GetString() ?? "";
                        game.Date = DateTime.TryParse(dateString, out var d) ? d : DateTime.Now;
                    }
                }

                if (eventElement.TryGetProperty("status", out var status))
                {
                    game.Status = GetPropertySafe(status, "name") ?? "Scheduled";
                    if (status.TryGetProperty("period", out var period)) game.Period = period.GetInt32();
                    if (status.TryGetProperty("displayClock", out var clock)) game.TimeRemaining = clock.GetString();
                }

                if (eventElement.TryGetProperty("competitions", out var competitions))
                {
                    var competition = competitions.EnumerateArray().FirstOrDefault();
                    if (competition.ValueKind != JsonValueKind.Undefined && competition.TryGetProperty("competitors", out var competitors))
                    {
                        foreach (var competitor in competitors.EnumerateArray())
                        {
                            var isHome = GetPropertySafe(competitor, "homeAway") == "home";
                            if (competitor.TryGetProperty("team", out var teamElement))
                            {
                                var teamId = GetPropertySafe(teamElement, "id");
                                if (isHome) { game.HomeTeamId = teamId; game.HomeTeamName = GetPropertySafe(teamElement, "displayName"); game.HomeTeamAbbreviation = GetPropertySafe(teamElement, "abbreviation"); }
                                else { game.AwayTeamId = teamId; game.AwayTeamName = GetPropertySafe(teamElement, "displayName"); game.AwayTeamAbbreviation = GetPropertySafe(teamElement, "abbreviation"); }
                            }

                            if (competitor.TryGetProperty("score", out var scoreElement))
                            {
                                var scoreValue = ParseScore(scoreElement) ?? 0;
                                if (isHome) game.HomeTeamScore = scoreValue;
                                else game.AwayTeamScore = scoreValue;
                            }
                        }
                    }
                }

                if (eventElement.TryGetProperty("season", out var seasonElement))
                {
                    if (seasonElement.TryGetProperty("year", out var year)) game.Season = year.GetInt32();
                    if (seasonElement.TryGetProperty("type", out var type)) game.IsPostseason = type.GetInt32() == 3;
                }

                return (string.IsNullOrEmpty(game.HomeTeamId) || string.IsNullOrEmpty(game.AwayTeamId)) ? null : game;
            }
            catch { return null; }
        }

        private void ParseAndAddStat(List<EspnStatDto> targetStats, string key, string valStr)
        {
            if (valStr.Contains("-"))
            {
                var parts = valStr.Split('-');
                if (parts.Length == 2)
                {
                    if (key == "fieldGoalsMade-fieldGoalsAttempted")
                    {
                        targetStats.Add(new EspnStatDto { Name = "fieldGoalsMade", Value = (double)SafeParseDecimal(parts[0]) });
                        targetStats.Add(new EspnStatDto { Name = "fieldGoalsAttempted", Value = (double)SafeParseDecimal(parts[1]) });
                    }
                    else if (key == "threePointFieldGoalsMade-threePointFieldGoalsAttempted")
                    {
                        targetStats.Add(new EspnStatDto { Name = "threePointFieldGoalsMade", Value = (double)SafeParseDecimal(parts[0]) });
                        targetStats.Add(new EspnStatDto { Name = "threePointFieldGoalsAttempted", Value = (double)SafeParseDecimal(parts[1]) });
                    }
                    else if (key == "freeThrowsMade-freeThrowsAttempted")
                    {
                        targetStats.Add(new EspnStatDto { Name = "freeThrowsMade", Value = (double)SafeParseDecimal(parts[0]) });
                        targetStats.Add(new EspnStatDto { Name = "freeThrowsAttempted", Value = (double)SafeParseDecimal(parts[1]) });
                    }
                }
            }
            else
            {
                targetStats.Add(new EspnStatDto { Name = key, Value = (double)SafeParseDecimal(valStr) });
            }
        }

        public PlayerPosition? ParsePosition(string? pos)
        {
            if (string.IsNullOrEmpty(pos)) return null;
            return pos.ToUpper() switch
            {
                "PG" => PlayerPosition.PG,
                "SG" => PlayerPosition.SG,
                "SF" => PlayerPosition.SF,
                "PF" => PlayerPosition.PF,
                "C" => PlayerPosition.C,
                "G" => PlayerPosition.G,
                "F" => PlayerPosition.F,
                _ => null
            };
        }

        public string ExtractIdFromRef(string? reference)
        {
            if (string.IsNullOrEmpty(reference)) return string.Empty;
            var parts = reference.Split('/');
            var lastPart = parts.LastOrDefault() ?? "";
            return lastPart.Split('?').First();
        }

        public int SafeParseInt(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return 0;
            var clean = new string(value.Where(c => char.IsDigit(c) || c == '-' || c == '+').ToArray());
            if (int.TryParse(clean, out int res)) return res;
            return 0;
        }

        public decimal SafeParseDecimal(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return 0;
            var clean = new string(value.Where(c => char.IsDigit(c) || c == '.' || c == '-' || c == '+').ToArray());
            if (decimal.TryParse(clean, out decimal res)) return res;
            return 0;
        }

        public int? ParseScore(JsonElement? scoreElement)
        {
            if (scoreElement == null || scoreElement.Value.ValueKind == JsonValueKind.Null || scoreElement.Value.ValueKind == JsonValueKind.Undefined)
                return null;

            if (scoreElement.Value.ValueKind == JsonValueKind.Number)
                return scoreElement.Value.GetInt32();

            if (scoreElement.Value.ValueKind == JsonValueKind.String)
                return int.TryParse(scoreElement.Value.GetString(), out var val) ? val : null;

            if (scoreElement.Value.ValueKind == JsonValueKind.Object &&
                scoreElement.Value.TryGetProperty("value", out var valProp))
            {
                if (valProp.ValueKind == JsonValueKind.Number) return valProp.GetInt32();
                if (valProp.ValueKind == JsonValueKind.String) return int.TryParse(valProp.GetString(), out var val) ? val : null;
            }

            return null;
        }

        public GameStatus MapGameStatus(string? externalStatus)
        {
            if (string.IsNullOrEmpty(externalStatus)) return GameStatus.Scheduled;

            return externalStatus.Trim().ToLowerInvariant() switch
            {
                "final" or "completed" => GameStatus.Final,
                "in progress" or "live" or "in_progress" => GameStatus.Live,
                "scheduled" or "pre" or "pregame" => GameStatus.Scheduled,
                "postponed" or "delayed" => GameStatus.Postponed,
                "cancelled" or "canceled" => GameStatus.Cancelled,
                _ => GameStatus.Scheduled
            };
        }

        private string GetPropertySafe(JsonElement element, string propertyName)
        {
            return element.TryGetProperty(propertyName, out var property) && property.ValueKind == JsonValueKind.String
                ? property.GetString() ?? ""
                : "";
        }
    }
}
