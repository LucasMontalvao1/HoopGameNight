using System;
using AutoMapper;
using HoopGameNight.Core.DTOs.Response;
using HoopGameNight.Core.Enums;
using HoopGameNight.Core.Extensions;
using HoopGameNight.Core.Models.Entities;
using static HoopGameNight.Core.Models.Entities.Player;

namespace HoopGameNight.Api.Mappings
{
    public class AutoMapperProfile : Profile
    {
        public AutoMapperProfile()
        {
            CreateEntityToResponseMaps();
            CreatePlayerStatsMaps(); 
        }

        private void CreateEntityToResponseMaps()
        {
            // Team Mappings
            CreateMap<Team, TeamResponse>()
                .ForMember(dest => dest.Conference, opt => opt.MapFrom(src => src.Conference.ToString()))
                .ForMember(dest => dest.ConferenceDisplay, opt => opt.MapFrom(src => src.Conference.GetDescription()))
                .ForMember(dest => dest.DisplayName, opt => opt.MapFrom(src => src.DisplayName));

            CreateMap<Team, TeamSummaryResponse>()
                .ForMember(dest => dest.DisplayName, opt => opt.MapFrom(src => src.DisplayName));

            // Player Mappings
            CreateMap<Player, PlayerResponse>()
                .ForMember(dest => dest.Position, opt => opt.MapFrom(src => src.Position.HasValue ? src.Position.Value.ToString() : null))
                .ForMember(dest => dest.PositionDisplay, opt => opt.MapFrom(src => src.Position.HasValue ? src.Position.Value.GetDescription() : null))
                .ForMember(dest => dest.Team, opt => opt.MapFrom(src => src.Team));

            CreateMap<Player, PlayerSummaryResponse>()
                .ForMember(dest => dest.Position, opt => opt.MapFrom(src => src.Position.HasValue ? src.Position.Value.ToString() : null))
                .ForMember(dest => dest.Team, opt => opt.MapFrom(src => src.Team != null ? src.Team.DisplayName : null));

            // Game Mappings
            CreateMap<Game, GameResponse>()
                .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.Status.ToString()))
                .ForMember(dest => dest.StatusDisplay, opt => opt.MapFrom(src => src.Status.GetDescription()))
                .ForMember(dest => dest.HomeTeam, opt => opt.MapFrom(src => src.HomeTeam))
                .ForMember(dest => dest.VisitorTeam, opt => opt.MapFrom(src => src.VisitorTeam))
                .ForMember(dest => dest.WinningTeam, opt => opt.MapFrom(src => GetWinningTeamSummary(src)))
                .ForMember(dest => dest.Period, opt => opt.MapFrom(src => src.Period))
                .ForMember(dest => dest.TimeRemaining, opt => opt.MapFrom(src => src.TimeRemaining))
                .ForMember(dest => dest.PostSeason, opt => opt.MapFrom(src => src.PostSeason))
                .ForMember(dest => dest.Score, opt => opt.MapFrom(src => src.Score))
                .ForMember(dest => dest.GameTitle, opt => opt.MapFrom(src => src.GameTitle))
                .ForMember(dest => dest.IsLive, opt => opt.MapFrom(src => src.IsLive))
                .ForMember(dest => dest.IsCompleted, opt => opt.MapFrom(src => src.IsCompleted))
                .ForMember(dest => dest.IsFutureGame, opt => opt.MapFrom(src => src.IsFutureGame));

            CreateMap<Game, GameSummaryResponse>()
                .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.Status.ToString()))
                .ForMember(dest => dest.HomeTeam, opt => opt.MapFrom(src => src.HomeTeam != null ? src.HomeTeam.DisplayName : $"Team {src.HomeTeamId}"))
                .ForMember(dest => dest.VisitorTeam, opt => opt.MapFrom(src => src.VisitorTeam != null ? src.VisitorTeam.DisplayName : $"Team {src.VisitorTeamId}"));
        }

        private void CreatePlayerStatsMaps()
        {
            // Player para PlayerDetailedResponse
            CreateMap<Player, PlayerDetailedResponse>()
                .ForMember(dest => dest.FullName, opt => opt.MapFrom(src => src.FullName))
                .ForMember(dest => dest.Height, opt => opt.MapFrom(src => src.Height))
                .ForMember(dest => dest.Weight, opt => opt.MapFrom(src => src.Weight))
                .ForMember(dest => dest.Age, opt => opt.MapFrom(src => src.Age))
                .ForMember(dest => dest.Position, opt => opt.MapFrom(src =>
                    src.Position.HasValue ? src.Position.Value.ToString() : null))
                .ForMember(dest => dest.Draft, opt => opt.MapFrom(src =>
                    src.DraftYear.HasValue && src.DraftRound.HasValue && src.DraftPick.HasValue
                    ? new DraftInfo
                    {
                        Year = src.DraftYear.Value,
                        Round = src.DraftRound.Value,
                        Pick = src.DraftPick.Value
                    }
                    : null))
                .ForMember(dest => dest.CurrentTeam, opt => opt.MapFrom(src => src.Team))
                .ForMember(dest => dest.CurrentSeasonStats, opt => opt.Ignore())
                .ForMember(dest => dest.CareerStats, opt => opt.Ignore())
                .ForMember(dest => dest.RecentGames, opt => opt.Ignore());

            // PlayerSeasonStats para PlayerSeasonStatsResponse
            CreateMap<PlayerSeasonStats, PlayerSeasonStatsResponse>()
                .ForMember(dest => dest.PPG, opt => opt.MapFrom(src => src.AvgPoints))
                .ForMember(dest => dest.RPG, opt => opt.MapFrom(src => src.AvgRebounds))
                .ForMember(dest => dest.APG, opt => opt.MapFrom(src => src.AvgAssists))
                .ForMember(dest => dest.SPG, opt => opt.MapFrom(src =>
                    src.GamesPlayed > 0 ? Math.Round((decimal)src.Steals / src.GamesPlayed, 1) : 0))
                .ForMember(dest => dest.BPG, opt => opt.MapFrom(src =>
                    src.GamesPlayed > 0 ? Math.Round((decimal)src.Blocks / src.GamesPlayed, 1) : 0))
                .ForMember(dest => dest.MPG, opt => opt.MapFrom(src => src.AvgMinutes))
                .ForMember(dest => dest.FGPercentage, opt => opt.MapFrom(src => src.FieldGoalPercentage ?? 0))
                .ForMember(dest => dest.ThreePointPercentage, opt => opt.MapFrom(src => src.ThreePointPercentage ?? 0))
                .ForMember(dest => dest.FTPercentage, opt => opt.MapFrom(src => src.FreeThrowPercentage ?? 0))
                .ForMember(dest => dest.TotalPoints, opt => opt.MapFrom(src => src.Points))
                .ForMember(dest => dest.TotalRebounds, opt => opt.MapFrom(src => src.TotalRebounds))
                .ForMember(dest => dest.TotalAssists, opt => opt.MapFrom(src => src.Assists))
                .ForMember(dest => dest.TeamName, opt => opt.Ignore()); 

            // PlayerCareerStats para PlayerCareerStatsResponse
            CreateMap<PlayerCareerStats, PlayerCareerStatsResponse>()
                .ForMember(dest => dest.CareerHighPoints, opt => opt.MapFrom(src => src.HighestPointsGame))
                .ForMember(dest => dest.CareerHighRebounds, opt => opt.MapFrom(src => src.HighestReboundsGame))
                .ForMember(dest => dest.CareerHighAssists, opt => opt.MapFrom(src => src.HighestAssistsGame));

            // PlayerGameStats para PlayerRecentGameResponse
            CreateMap<PlayerGameStats, PlayerRecentGameResponse>()
                .ForMember(dest => dest.GameDate, opt => opt.MapFrom(src =>
                    src.Game != null ? src.Game.Date : DateTime.MinValue))
                .ForMember(dest => dest.Opponent, opt => opt.Ignore())
                .ForMember(dest => dest.IsHome, opt => opt.Ignore())
                .ForMember(dest => dest.Result, opt => opt.Ignore())
                .ForMember(dest => dest.Minutes, opt => opt.MapFrom(src => src.MinutesFormatted))
                .ForMember(dest => dest.FieldGoals, opt => opt.MapFrom(src => src.FieldGoalsFormatted))
                .ForMember(dest => dest.ThreePointers, opt => opt.MapFrom(src => src.ThreePointersFormatted))
                .ForMember(dest => dest.FreeThrows, opt => opt.MapFrom(src => src.FreeThrowsFormatted))
                .ForMember(dest => dest.DoubleDouble, opt => opt.MapFrom(src => src.DoubleDouble))
                .ForMember(dest => dest.TripleDouble, opt => opt.MapFrom(src => src.TripleDouble));
        }

        private static TeamSummaryResponse? GetWinningTeamSummary(Game game)
        {
            var winningTeam = game.WinningTeam;
            return winningTeam != null ? new TeamSummaryResponse
            {
                Id = winningTeam.Id,
                Name = winningTeam.Name,
                Abbreviation = winningTeam.Abbreviation,
                City = winningTeam.City,
                DisplayName = winningTeam.DisplayName
            } : null;
        }

        private static Conference ParseConference(string conference)
        {
            return conference?.ToLower() switch
            {
                "east" => Conference.East,
                "west" => Conference.West,
                _ => Conference.East
            };
        }

        private static PlayerPosition? ParsePlayerPosition(string? position)
        {
            if (string.IsNullOrWhiteSpace(position))
                return null;

            return position.ToUpper() switch
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

        private static GameStatus ParseGameStatus(string status)
        {
            return status?.ToLower() switch
            {
                "scheduled" => GameStatus.Scheduled,
                "live" => GameStatus.Live,
                "final" => GameStatus.Final,
                "postponed" => GameStatus.Postponed,
                "cancelled" => GameStatus.Cancelled,
                _ => GameStatus.Scheduled
            };
        }

        private static DateTime ParseGameDate(string dateString)
        {
            if (DateTime.TryParse(dateString, out var date))
                return date;

            return DateTime.UtcNow;
        }

        private static int ParseMinutesFromString(string? minutesStr)
        {
            if (string.IsNullOrEmpty(minutesStr)) return 0;

            var parts = minutesStr.Split(':');
            if (parts.Length == 2 && int.TryParse(parts[0], out int minutes))
                return minutes;

            return 0;
        }

        private static int ParseSecondsFromString(string? minutesStr)
        {
            if (string.IsNullOrEmpty(minutesStr)) return 0;

            var parts = minutesStr.Split(':');
            if (parts.Length == 2 && int.TryParse(parts[1], out int seconds))
                return seconds;

            return 0;
        }
    }
}