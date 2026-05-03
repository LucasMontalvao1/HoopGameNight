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
                        case "avgminutes":
                        case "mpg":
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

                        case "pts": case "points": case "totalpoints": stats.Points = SafeParseInt(statValue); break;
                        
                        case "fg": case "fgm/fga": case "fgm-fga": case "fieldgoals":
                            var fgParts = statValue.Split(new[] { '-', '/' });
                            if (fgParts.Length == 2)
                            {
                                stats.FieldGoalsMade = SafeParseInt(fgParts[0]);
                                stats.FieldGoalsAttempted = SafeParseInt(fgParts[1]);
                            }
                            break;
                        case "fgm": case "fieldgoalsmade": stats.FieldGoalsMade = SafeParseInt(statValue); break;
                        case "fga": case "fieldgoalsattempted": stats.FieldGoalsAttempted = SafeParseInt(statValue); break;

                        case "3pt": case "3pm/3pa": case "3pm-3pa": case "threepointers": case "3p":
                            var tpParts = statValue.Split(new[] { '-', '/' });
                            if (tpParts.Length == 2)
                            {
                                stats.ThreePointersMade = SafeParseInt(tpParts[0]);
                                stats.ThreePointersAttempted = SafeParseInt(tpParts[1]);
                            }
                            break;
                        case "3pm": case "threepointersmade": stats.ThreePointersMade = SafeParseInt(statValue); break;
                        case "3pa": case "threepointersattempted": stats.ThreePointersAttempted = SafeParseInt(statValue); break;

                        case "ft": case "ftm/fta": case "ftm-fta": case "freethrows":
                            var ftParts = statValue.Split(new[] { '-', '/' });
                            if (ftParts.Length == 2)
                            {
                                stats.FreeThrowsMade = SafeParseInt(ftParts[0]);
                                stats.FreeThrowsAttempted = SafeParseInt(ftParts[1]);
                            }
                            break;
                        case "ftm": case "freethrowsmade": stats.FreeThrowsMade = SafeParseInt(statValue); break;
                        case "fta": case "freethrowsattempted": stats.FreeThrowsAttempted = SafeParseInt(statValue); break;

                        case "oreb": case "offensiverebounds": stats.OffensiveRebounds = SafeParseInt(statValue); break;
                        case "dreb": case "defensiverebounds": stats.DefensiveRebounds = SafeParseInt(statValue); break;
                        case "reb": case "totalrebounds": stats.TotalRebounds = SafeParseInt(statValue); break;
                        case "ast": case "assists": case "totalassists": stats.Assists = SafeParseInt(statValue); break;
                        case "stl": case "steals": case "totalsteals": stats.Steals = SafeParseInt(statValue); break;
                        case "blk": case "blocks": case "totalblocks": stats.Blocks = SafeParseInt(statValue); break;
                        case "to": case "tov": case "turnovers": case "totalturnovers": stats.Turnovers = SafeParseInt(statValue); break;
                        case "pf": case "fouls": case "personalfouls": case "totalpersonalfouls": stats.PersonalFouls = SafeParseInt(statValue); break;
                        case "+/-": case "pm": case "plusminus": stats.PlusMinus = SafeParseInt(statValue); break;
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

            decimal avgFgm = 0, avgFga = 0, avg3pm = 0, avg3pa = 0, avgFtm = 0, avgFta = 0;

            foreach (var category in espnStats.Splits.Categories)
            {
                if (category.Stats == null) continue;

                var categoryName = category.Name?.ToLowerInvariant() ?? "";
                bool isAverageCategory = categoryName.Contains("average") || categoryName.Contains("pergame") || categoryName.Contains("pg");

                foreach (var stat in category.Stats)
                {
                    var name = stat.Name?.ToLowerInvariant();
                    if (name == null) continue;
                    
                    var val = (stat.Value == 0 && !string.IsNullOrEmpty(stat.DisplayValue) && stat.DisplayValue != "0") 
                        ? SafeParseDecimal(stat.DisplayValue) 
                        : (decimal)stat.Value;

                    switch (name)
                    {
                        case "gamesplayed": case "gp": stats.GamesPlayed = (int)val; break;
                        case "gamesstarted": case "gs": stats.GamesStarted = (int)val; break;
                        case "minutes": case "min": 
                            if (isAverageCategory) stats.AvgMinutes = val;
                            else stats.MinutesPlayed = val; 
                            break;
                        case "points": case "pts": case "totalpoints": 
                            if (isAverageCategory) stats.AvgPoints = val;
                            else stats.Points = (int)val; 
                            break;
                        case "fieldgoalsmade": case "fgm": stats.FieldGoalsMade = (int)val; break;
                        case "fieldgoalsattempted": case "fga": stats.FieldGoalsAttempted = (int)val; break;
                        case "fieldgoalpercentage": case "fg%": case "fgpct": stats.FieldGoalPercentage = val; break;
                        case "threepointfieldgoalsmade": case "3pm": case "threepointersmade": stats.ThreePointersMade = (int)val; break;
                        case "threepointfieldgoalsattempted": case "3pa": case "threepointersattempted": stats.ThreePointersAttempted = (int)val; break;
                        case "threepointfieldgoalpercentage": case "3p%": case "3ptpct": stats.ThreePointPercentage = val; break;
                        case "freethrowsmade": case "ftm": stats.FreeThrowsMade = (int)val; break;
                        case "freethrowsattempted": case "fta": stats.FreeThrowsAttempted = (int)val; break;
                        case "freethrowpercentage": case "ft%": case "ftpct": stats.FreeThrowPercentage = val; break;
                        case "offensiverebounds": case "oreb": stats.OffensiveRebounds = (int)val; break;
                        case "defensiverebounds": case "dreb": stats.DefensiveRebounds = (int)val; break;
                        case "totalrebounds": case "reb": case "totalreb": case "trb": 
                            if (isAverageCategory) stats.AvgRebounds = val;
                            else stats.TotalRebounds = (int)val; 
                            break;
                        case "assists": case "ast": 
                            if (isAverageCategory) stats.AvgAssists = val;
                            else stats.Assists = (int)val; 
                            break;
                        case "steals": case "stl": 
                            if (isAverageCategory) stats.AvgSteals = val;
                            else stats.Steals = (int)val; 
                            break;
                        case "blocks": case "blk": 
                            if (isAverageCategory) stats.AvgBlocks = val;
                            else stats.Blocks = (int)val; 
                            break;
                        case "turnovers": case "to": case "tov": 
                            if (isAverageCategory) stats.AvgTurnovers = val;
                            else stats.Turnovers = (int)val; 
                            break;
                        case "personalfouls": case "pf": 
                            if (isAverageCategory) stats.AvgFouls = val;
                            else stats.PersonalFouls = (int)val; 
                            break;
                        case "avgpoints": case "ppg": case "pointspergame": stats.AvgPoints = val; break;
                        case "avgrebounds": case "rpg": case "reboundspergame": stats.AvgRebounds = val; break;
                        case "avgassists": case "apg": case "assistspergame": stats.AvgAssists = val; break;
                        case "avgminutes": case "mpg": case "minutespergame": stats.AvgMinutes = val; break;
                        case "avgfieldgoalsmade": case "avgfgm": avgFgm = val; break;
                        case "avgfieldgoalsattempted": case "avgfga": avgFga = val; break;
                        case "avgthreepointfieldgoalsmade": case "avg3pm": avg3pm = val; break;
                        case "avgthreepointfieldgoalsattempted": case "avg3pa": avg3pa = val; break;
                        case "avgfreethrowsmade": case "avgftm": avgFtm = val; break;
                        case "avgfreethrowsattempted": case "avgfta": avgFta = val; break;
                        case "fieldgoals": case "fg":
                            if (!string.IsNullOrEmpty(stat.DisplayValue))
                            {
                                var parts = stat.DisplayValue.Split('/', '-');
                                if (parts.Length == 2 && int.TryParse(parts[0], out int m) && int.TryParse(parts[1], out int a))
                                {
                                    stats.FieldGoalsMade = m;
                                    stats.FieldGoalsAttempted = a;
                                }
                            }
                            break;
                        case "threepointers": case "3p":
                            if (!string.IsNullOrEmpty(stat.DisplayValue))
                            {
                                var parts = stat.DisplayValue.Split('/', '-');
                                if (parts.Length == 2 && int.TryParse(parts[0], out int m) && int.TryParse(parts[1], out int a))
                                {
                                    stats.ThreePointersMade = m;
                                    stats.ThreePointersAttempted = a;
                                }
                            }
                            break;
                        case "freethrows": case "ft":
                            if (!string.IsNullOrEmpty(stat.DisplayValue))
                            {
                                var parts = stat.DisplayValue.Split('/', '-');
                                if (parts.Length == 2 && int.TryParse(parts[0], out int m) && int.TryParse(parts[1], out int a))
                                {
                                    stats.FreeThrowsMade = m;
                                    stats.FreeThrowsAttempted = a;
                                }
                            }
                            break;
                        case "avgsteals": case "spg": case "stealspergame": stats.AvgSteals = val; break;
                        case "avgblocks": case "bpg": case "blockspergame": stats.AvgBlocks = val; break;
                        case "avgturnovers": case "tpg": case "turnoverspergame": stats.AvgTurnovers = val; break;
                        case "avgpersonalfouls": case "fpg": case "foulspergame": stats.AvgFouls = val; break;
                    }
                }
            }

            // Post-Mapping Fallbacks (Estimating totals from averages if missing, or vice-versa)
            if (stats.GamesPlayed > 0)
            {
                // If total is 0 but avg is not, calculate total
                if (stats.Points == 0 && stats.AvgPoints > 0) stats.Points = (int)Math.Round(stats.AvgPoints * stats.GamesPlayed);
                if (stats.TotalRebounds == 0 && stats.AvgRebounds > 0) stats.TotalRebounds = (int)Math.Round(stats.AvgRebounds * stats.GamesPlayed);
                if (stats.Assists == 0 && stats.AvgAssists > 0) stats.Assists = (int)Math.Round(stats.AvgAssists * stats.GamesPlayed);
                if (stats.MinutesPlayed == 0 && stats.AvgMinutes > 0) stats.MinutesPlayed = Math.Round(stats.AvgMinutes * (decimal)stats.GamesPlayed, 1);
                
                // If avg is 0 but total is not, calculate avg
                if (stats.AvgPoints == 0 && stats.Points > 0) stats.AvgPoints = Math.Round((decimal)stats.Points / stats.GamesPlayed, 2);
                if (stats.AvgRebounds == 0 && stats.TotalRebounds > 0) stats.AvgRebounds = Math.Round((decimal)stats.TotalRebounds / stats.GamesPlayed, 2);
                if (stats.AvgAssists == 0 && stats.Assists > 0) stats.AvgAssists = Math.Round((decimal)stats.Assists / stats.GamesPlayed, 2);
                if (stats.AvgMinutes == 0 && stats.MinutesPlayed > 0) stats.AvgMinutes = Math.Round((decimal)stats.MinutesPlayed / stats.GamesPlayed, 2);

                // Shooting fallbacks
                if (stats.FieldGoalsMade == 0 && avgFgm > 0) stats.FieldGoalsMade = (int)Math.Round(avgFgm * stats.GamesPlayed);
                if (stats.FieldGoalsAttempted == 0 && avgFga > 0) stats.FieldGoalsAttempted = (int)Math.Round(avgFga * stats.GamesPlayed);
                if (stats.ThreePointersMade == 0 && avg3pm > 0) stats.ThreePointersMade = (int)Math.Round(avg3pm * stats.GamesPlayed);
                if (stats.ThreePointersAttempted == 0 && avg3pa > 0) stats.ThreePointersAttempted = (int)Math.Round(avg3pa * stats.GamesPlayed);
                if (stats.FreeThrowsMade == 0 && avgFtm > 0) stats.FreeThrowsMade = (int)Math.Round(avgFtm * stats.GamesPlayed);
                if (stats.FreeThrowsAttempted == 0 && avgFta > 0) stats.FreeThrowsAttempted = (int)Math.Round(avgFta * stats.GamesPlayed);

                // Defensive fallbacks
                if (stats.Steals == 0 && stats.AvgSteals > 0) stats.Steals = (int)Math.Round(stats.AvgSteals * stats.GamesPlayed);
                if (stats.Blocks == 0 && stats.AvgBlocks > 0) stats.Blocks = (int)Math.Round(stats.AvgBlocks * stats.GamesPlayed);
                if (stats.Turnovers == 0 && stats.AvgTurnovers > 0) stats.Turnovers = (int)Math.Round(stats.AvgTurnovers * stats.GamesPlayed);
                if (stats.PersonalFouls == 0 && stats.AvgFouls > 0) stats.PersonalFouls = (int)Math.Round(stats.AvgFouls * stats.GamesPlayed);
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
                        game.Date = DateTime.TryParse(dateString, null, System.Globalization.DateTimeStyles.AdjustToUniversal, out var d) 
                            ? d 
                            : DateTime.UtcNow;
                    }
                }

                if (eventElement.TryGetProperty("status", out var status))
                {
                    string? statusValue = null;
                    if (status.TryGetProperty("type", out var statusType))
                    {
                        statusValue = GetPropertySafe(statusType, "state");
                        if (string.IsNullOrEmpty(statusValue))
                            statusValue = GetPropertySafe(statusType, "name");
                    }
                    if (string.IsNullOrEmpty(statusValue))
                        statusValue = GetPropertySafe(status, "name");

                    game.Status = statusValue ?? "Scheduled";
                    if (status.TryGetProperty("period", out var period)) game.Period = period.GetInt32();
                    if (status.TryGetProperty("displayClock", out var clock)) game.TimeRemaining = clock.GetString();
                }

                if (eventElement.TryGetProperty("competitions", out var competitions))
                {
                    var competition = competitions.EnumerateArray().FirstOrDefault();
                    if (competition.ValueKind != JsonValueKind.Undefined && competition.TryGetProperty("competitors", out var competitors))
                    {
                        var homeLines = new List<int>();
                        var visitorLines = new List<int>();

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

                            // Extract Team Record
                            if (competitor.TryGetProperty("records", out var records))
                            {
                                var overallRecord = records.EnumerateArray().FirstOrDefault(r => 
                                    GetPropertySafe(r, "name").ToLower().Contains("overall") || 
                                    GetPropertySafe(r, "type").ToLower().Contains("total") ||
                                    GetPropertySafe(r, "name") == "" || GetPropertySafe(r, "type") == "");
                                
                                if (overallRecord.ValueKind != JsonValueKind.Undefined)
                                {
                                    var recordStr = GetPropertySafe(overallRecord, "summary");
                                    if (isHome) game.HomeTeamRecord = recordStr;
                                    else game.AwayTeamRecord = recordStr;
                                }
                            }

                            // Extract LineScores
                            if (competitor.TryGetProperty("linescores", out var linescores))
                            {
                                foreach (var line in linescores.EnumerateArray())
                                {
                                    if (line.TryGetProperty("value", out var val))
                                    {
                                        int score = 0;
                                        if (val.ValueKind == JsonValueKind.Number) score = (int)val.GetDouble();
                                        else if (val.ValueKind == JsonValueKind.String) int.TryParse(val.GetString(), out score);

                                        if (isHome) homeLines.Add(score);
                                        else visitorLines.Add(score);
                                    }
                                }
                            }
                        }

                        // Format LineScoreJson
                        if (homeLines.Any() || visitorLines.Any())
                        {
                            var lineObj = new List<object>();
                            int maxPeriods = Math.Max(homeLines.Count, visitorLines.Count);
                            for (int i = 0; i < maxPeriods; i++)
                            {
                                string periodName = (i < 4) ? $"Q{i + 1}" : $"OT{i - 3}";
                                lineObj.Add(new
                                {
                                    Period = periodName,
                                    HomeScore = i < homeLines.Count ? homeLines[i] : 0,
                                    VisitorScore = i < visitorLines.Count ? visitorLines[i] : 0
                                });
                            }
                            game.LineScoreJson = JsonSerializer.Serialize(lineObj);
                        }
                    }

                    // Extract Leaders
                    if (competition.ValueKind != JsonValueKind.Undefined && competition.TryGetProperty("leaders", out var leadersArray))
                    {
                        var leadersObj = new
                        {
                            HomeTeamLeaders = ExtractTeamLeaders(leadersArray, true, game.HomeTeamAbbreviation ?? ""),
                            VisitorTeamLeaders = ExtractTeamLeaders(leadersArray, false, game.AwayTeamAbbreviation ?? "")
                        };
                        game.GameLeadersJson = JsonSerializer.Serialize(leadersObj);
                    }

                    // Extract Series Info
                    if (competition.ValueKind != JsonValueKind.Undefined && competition.TryGetProperty("notes", out var notes))
                    {
                        var firstNote = notes.EnumerateArray().FirstOrDefault();
                        if (firstNote.ValueKind != JsonValueKind.Undefined)
                        {
                            game.SeriesNote = GetPropertySafe(firstNote, "headline");
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
            // Only split for known composite keys like "FGM-FGA"
            if (valStr.Contains("-") && (key.Contains("Made-") || key.Contains("Attempted") || key.Contains("fieldGoals") || key.Contains("threePoint") || key.Contains("freeThrows")))
            {
                var parts = valStr.Split('-');
                if (parts.Length == 2)
                {
                    if (key == "fieldGoalsMade-fieldGoalsAttempted")
                    {
                        targetStats.Add(new EspnStatDto { Name = "fieldGoalsMade", Value = (double)SafeParseDecimal(parts[0]) });
                        targetStats.Add(new EspnStatDto { Name = "fieldGoalsAttempted", Value = (double)SafeParseDecimal(parts[1]) });
                        return;
                    }
                    else if (key == "threePointFieldGoalsMade-threePointFieldGoalsAttempted")
                    {
                        targetStats.Add(new EspnStatDto { Name = "threePointFieldGoalsMade", Value = (double)SafeParseDecimal(parts[0]) });
                        targetStats.Add(new EspnStatDto { Name = "threePointFieldGoalsAttempted", Value = (double)SafeParseDecimal(parts[1]) });
                        return;
                    }
                    else if (key == "freeThrowsMade-freeThrowsAttempted")
                    {
                        targetStats.Add(new EspnStatDto { Name = "freeThrowsMade", Value = (double)SafeParseDecimal(parts[0]) });
                        targetStats.Add(new EspnStatDto { Name = "freeThrowsAttempted", Value = (double)SafeParseDecimal(parts[1]) });
                        return;
                    }
                }
            }
            
            // Fallback for normal stats, including negative numbers like plusMinus "-5"
            targetStats.Add(new EspnStatDto { Name = key, Value = (double)SafeParseDecimal(valStr) });
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
            
            string part = value.Trim();
            int dashIndex = part.IndexOf('-', 1); // Ignora sinal de menos no início
            int slashIndex = part.IndexOf('/');
            
            int firstSeparator = -1;
            if (dashIndex > 0 && slashIndex > 0) firstSeparator = Math.Min(dashIndex, slashIndex);
            else if (dashIndex > 0) firstSeparator = dashIndex;
            else if (slashIndex > 0) firstSeparator = slashIndex;
            
            if (firstSeparator > 0)
            {
                part = part.Substring(0, firstSeparator).Trim();
            }
            
            var clean = new string(part.Where(c => char.IsDigit(c) || c == '-' || c == '+').ToArray());
            if (int.TryParse(clean, out int res)) return res;
            return 0;
        }

        public decimal SafeParseDecimal(string? value)
        {
            if (string.IsNullOrWhiteSpace(value)) return 0;
            
            string part = value.Replace("%", "").Trim();
            int dashIndex = part.IndexOf('-', 1); // Ignora sinal de menos no início
            int slashIndex = part.IndexOf('/');
            
            int firstSeparator = -1;
            if (dashIndex > 0 && slashIndex > 0) firstSeparator = Math.Min(dashIndex, slashIndex);
            else if (dashIndex > 0) firstSeparator = dashIndex;
            else if (slashIndex > 0) firstSeparator = slashIndex;

            if (firstSeparator > 0)
            {
                part = part.Substring(0, firstSeparator).Trim();
            }

            var clean = new string(part.Where(c => char.IsDigit(c) || c == '.' || c == ',' || c == '-' || c == '+').ToArray());
            if (clean.Contains(",") && !clean.Contains(".")) clean = clean.Replace(",", ".");

            if (decimal.TryParse(clean, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out decimal res)) return res;
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
                "in" => GameStatus.Live,
                "post" => GameStatus.Final,
                "pre" => GameStatus.Scheduled,

                "final" or "completed" or "f/ot" or "f/2ot" => GameStatus.Final,
                "in progress" or "live" or "in_progress" or "status_in_progress" => GameStatus.Live,
                "scheduled" or "pregame" or "status_scheduled" => GameStatus.Scheduled,
                "postponed" or "delayed" or "status_postponed" => GameStatus.Postponed,
                "cancelled" or "canceled" or "status_cancelled" => GameStatus.Cancelled,
                _ => GameStatus.Scheduled
            };
        }

        private object ExtractTeamLeaders(JsonElement leadersArray, bool isHome, string teamAbbreviation)
        {
            object? pointsLeader = null;
            object? reboundsLeader = null;
            object? assistsLeader = null;

            var side = isHome ? "home" : "away";

            foreach (var category in leadersArray.EnumerateArray())
            {
                var name = GetPropertySafe(category, "name")?.ToLowerInvariant() ?? "";
                if (category.TryGetProperty("leaders", out var teamLeaders))
                {
                    var sideLeaders = teamLeaders.EnumerateArray().FirstOrDefault(l => GetPropertySafe(l, "address") == side || GetPropertySafe(l, "homeAway") == side);
                    if (sideLeaders.ValueKind == JsonValueKind.Undefined)
                        sideLeaders = teamLeaders.EnumerateArray().FirstOrDefault();

                    if (sideLeaders.ValueKind != JsonValueKind.Undefined && sideLeaders.TryGetProperty("athlete", out var athlete))
                    {
                        var leader = new {
                            PlayerId = SafeParseInt(GetPropertyStringSafe(athlete, "id")),
                            PlayerName = GetPropertyStringSafe(athlete, "displayName"),
                            TeamAbbreviation = teamAbbreviation,
                            Value = sideLeaders.TryGetProperty("value", out var val) ? (val.ValueKind == JsonValueKind.Number ? val.GetDouble() : (double)SafeParseDecimal(val.GetString())) : 0,
                            Jersey = GetPropertyStringSafe(athlete, "jersey"),
                            Position = athlete.TryGetProperty("position", out var pos) ? GetPropertyStringSafe(pos, "abbreviation") : ""
                        };

                        if (name == "points") pointsLeader = leader;
                        else if (name == "rebounds") reboundsLeader = leader;
                        else if (name == "assists") assistsLeader = leader;
                    }
                }
            }

            return new {
                PointsLeader = pointsLeader,
                ReboundsLeader = reboundsLeader,
                AssistsLeader = assistsLeader
            };
        }

        public List<GamePlay> ParsePlaysResponse(string json, int gameId)
        {
            var plays = new List<GamePlay>();
            try
            {
                using var document = JsonDocument.Parse(json);
                var root = document.RootElement;

                // ESPN usually provides plays under 'plays' or 'items' depending on the endpoint
                JsonElement playsArray;
                if (!root.TryGetProperty("plays", out playsArray))
                {
                    if (!root.TryGetProperty("items", out playsArray))
                        return plays;
                }

                foreach (var item in playsArray.EnumerateArray())
                {
                    var play = new GamePlay
                    {
                        GameId = gameId,
                        ExternalId = GetPropertySafe(item, "id"),
                        Sequence = item.TryGetProperty("sequence", out var seq) ? seq.GetInt32() : 0,
                        Text = GetPropertySafe(item, "text"),
                        Type = item.TryGetProperty("type", out var type) ? GetPropertySafe(type, "text") : "",
                        ScoreValue = item.TryGetProperty("scoreValue", out var sv) ? sv.GetInt32() : 0,
                        HomeScore = item.TryGetProperty("homeScore", out var hs) ? hs.GetInt32() : 0,
                        AwayScore = item.TryGetProperty("awayScore", out var its) ? its.GetInt32() : 0,
                        CreatedAt = DateTime.UtcNow
                    };

                    if (item.TryGetProperty("period", out var period))
                        play.Period = period.TryGetProperty("number", out var pNum) ? pNum.GetInt32() : 0;

                    if (item.TryGetProperty("clock", out var clock))
                        play.Clock = GetPropertySafe(clock, "displayValue");

                    if (item.TryGetProperty("team", out var team))
                        play.TeamId = SafeParseInt(GetPropertySafe(team, "id"));

                    if (item.TryGetProperty("participants", out var participants) && participants.EnumerateArray().Any())
                        play.PlayerId = SafeParseInt(GetPropertySafe(participants.EnumerateArray().First(), "id"));

                    plays.Add(play);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error parsing ESPN plays response for game {GameId}", gameId);
            }
            return plays;
        }

        public Dictionary<string, (int Wins, int Losses)> ParseStandings(EspnStandingsDto standings)
        {
            var results = new Dictionary<string, (int Wins, int Losses)>();
            
            if (standings == null) return results;

            ProcessStandingEntries(standings.Entries, results);
            if (standings.Standings != null)
            {
                ProcessStandingEntries(standings.Standings.Entries, results);
            }
            ProcessStandingChildren(standings.Children, results);

            return results;
        }

        private void ProcessStandingChildren(List<EspnStandingsDto>? children, Dictionary<string, (int Wins, int Losses)> results)
        {
            if (children == null) return;

            foreach (var child in children)
            {
                ProcessStandingEntries(child.Entries, results);
                if (child.Standings != null)
                {
                    ProcessStandingEntries(child.Standings.Entries, results);
                }
                ProcessStandingChildren(child.Children, results);
            }
        }

        private void ProcessStandingEntries(List<EspnStandingEntryDto>? entries, Dictionary<string, (int Wins, int Losses)> results)
        {
            if (entries == null) return;

            foreach (var entry in entries)
            {
                if (entry.Team == null || string.IsNullOrEmpty(entry.Team.Id) || entry.Stats == null) continue;

                int wins = 0;
                int losses = 0;

                foreach (var stat in entry.Stats)
                {
                    var name = stat.Name?.ToLowerInvariant();
                    if (stat.Value != null && stat.Value.Value.ValueKind != JsonValueKind.Null && stat.Value.Value.ValueKind != JsonValueKind.Undefined)
                    {
                        var val = stat.Value.Value.ValueKind == JsonValueKind.Number 
                            ? stat.Value.Value.GetDecimal() 
                            : SafeParseDecimal(stat.Value.Value.GetString());

                        if (name == "wins" || name == "w")
                        {
                            wins = (int)val;
                        }
                        else if (name == "losses" || name == "l")
                        {
                            losses = (int)val;
                        }
                    }
                }

                results[entry.Team.Id] = (wins, losses);
            }
        }

        private string GetPropertyStringSafe(JsonElement element, string propertyName)
        {
            if (element.ValueKind == JsonValueKind.Null || element.ValueKind == JsonValueKind.Undefined) return "";
            if (!element.TryGetProperty(propertyName, out var property)) return "";
            
            return property.ValueKind switch
            {
                JsonValueKind.String => property.GetString() ?? "",
                JsonValueKind.Number => property.ToString(),
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                _ => ""
            };
        }

        private string GetPropertySafe(JsonElement element, string propertyName) => GetPropertyStringSafe(element, propertyName);
    }
}
