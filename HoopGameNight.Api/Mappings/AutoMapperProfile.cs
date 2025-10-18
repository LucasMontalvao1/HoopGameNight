using AutoMapper;
using HoopGameNight.Core.DTOs.External.BallDontLie;
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
            CreateExternalToEntityMaps();
            CreatePlayerStatsMaps(); 
        }

        private void CreateEntityToResponseMaps()
        {
            // Team Mappings
            CreateMap<Team, TeamResponse>()
                .ForMember(dest => dest.Conference, opt => opt.MapFrom(src => src.Conference.ToString()))
                .ForMember(dest => dest.ConferenceDisplay, opt => opt.MapFrom(src => src.Conference.GetDescription()));

            CreateMap<Team, TeamSummaryResponse>();

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
                .ForMember(dest => dest.WinningTeam, opt => opt.MapFrom(src => GetWinningTeamSummary(src)));

            CreateMap<Game, GameSummaryResponse>()
                .ForMember(dest => dest.Status, opt => opt.MapFrom(src => src.Status.ToString()))
                .ForMember(dest => dest.HomeTeam, opt => opt.MapFrom(src => src.HomeTeam != null ? src.HomeTeam.DisplayName : $"Team {src.HomeTeamId}"))
                .ForMember(dest => dest.VisitorTeam, opt => opt.MapFrom(src => src.VisitorTeam != null ? src.VisitorTeam.DisplayName : $"Team {src.VisitorTeamId}"));
        }

        private void CreateExternalToEntityMaps()
        {
            // Ball Don't Lie Team to Entity
            CreateMap<BallDontLieTeamDto, Team>()
                .ForMember(dest => dest.Id, opt => opt.Ignore())
                .ForMember(dest => dest.ExternalId, opt => opt.MapFrom(src => src.Id))
                .ForMember(dest => dest.Conference, opt => opt.MapFrom(src => ParseConference(src.Conference)))
                .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore());

            // Ball Don't Lie Player to Entity
            CreateMap<BallDontLiePlayerDto, Player>()
                .ForMember(dest => dest.Id, opt => opt.Ignore())
                .ForMember(dest => dest.ExternalId, opt => opt.MapFrom(src => src.Id))
                .ForMember(dest => dest.FirstName, opt => opt.MapFrom(src => src.FirstName))
                .ForMember(dest => dest.LastName, opt => opt.MapFrom(src => src.LastName))
                .ForMember(dest => dest.Position, opt => opt.MapFrom(src => ParsePlayerPosition(src.Position)))
                .ForMember(dest => dest.HeightFeet, opt => opt.MapFrom(src => src.HeightFeet))
                .ForMember(dest => dest.HeightInches, opt => opt.MapFrom(src => src.HeightInches))
                .ForMember(dest => dest.WeightPounds, opt => opt.MapFrom(src => src.WeightPounds))
                .ForMember(dest => dest.TeamId, opt => opt.MapFrom(src => src.Team != null ? (int?)src.Team.Id : null))
                .ForMember(dest => dest.Team, opt => opt.Ignore())
                .ForMember(dest => dest.NbaStatsId, opt => opt.Ignore()) // Será preenchido pela busca híbrida
                .ForMember(dest => dest.EspnId, opt => opt.Ignore()) // Será preenchido pela busca híbrida
                .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore());

            // Ball Don't Lie Game to Entity
            CreateMap<BallDontLieGameDto, Game>()
                .ForMember(dest => dest.Id, opt => opt.Ignore())
                .ForMember(dest => dest.ExternalId, opt => opt.MapFrom(src => src.Id))
                .ForMember(dest => dest.Date, opt => opt.MapFrom(src => ParseGameDate(src.Date)))
                .ForMember(dest => dest.DateTime, opt => opt.MapFrom(src => ParseGameDate(src.Date)))
                .ForMember(dest => dest.HomeTeamId, opt => opt.MapFrom(src => src.HomeTeam.Id))
                .ForMember(dest => dest.VisitorTeamId, opt => opt.MapFrom(src => src.VisitorTeam.Id))
                .ForMember(dest => dest.Status, opt => opt.MapFrom(src => ParseGameStatus(src.Status)))
                .ForMember(dest => dest.TimeRemaining, opt => opt.MapFrom(src => src.Time))
                .ForMember(dest => dest.PostSeason, opt => opt.MapFrom(src => src.Postseason))
                .ForMember(dest => dest.HomeTeam, opt => opt.Ignore())
                .ForMember(dest => dest.VisitorTeam, opt => opt.Ignore())
                .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore());

            // Ball Don't Lie Player Season Stats to Entity
            CreateMap<BallDontLiePlayerSeasonStatsDto, PlayerSeasonStats>()
                .ForMember(dest => dest.Id, opt => opt.Ignore())
                .ForMember(dest => dest.PlayerId, opt => opt.Ignore())
                .ForMember(dest => dest.Season, opt => opt.Ignore())
                .ForMember(dest => dest.TeamId, opt => opt.Ignore())
                .ForMember(dest => dest.GamesPlayed, opt => opt.MapFrom(src => src.GamesPlayed))
                .ForMember(dest => dest.Points, opt => opt.MapFrom(src => (int)(src.Pts * src.GamesPlayed)))
                .ForMember(dest => dest.TotalRebounds, opt => opt.MapFrom(src => (int)(src.Reb * src.GamesPlayed)))
                .ForMember(dest => dest.Assists, opt => opt.MapFrom(src => (int)(src.Ast * src.GamesPlayed)))
                .ForMember(dest => dest.Steals, opt => opt.MapFrom(src => (int)(src.Stl * src.GamesPlayed)))
                .ForMember(dest => dest.Blocks, opt => opt.MapFrom(src => (int)(src.Blk * src.GamesPlayed)))
                .ForMember(dest => dest.Turnovers, opt => opt.MapFrom(src => (int)(src.Turnover * src.GamesPlayed)))
                .ForMember(dest => dest.AvgPoints, opt => opt.MapFrom(src => (decimal)src.Pts))
                .ForMember(dest => dest.AvgRebounds, opt => opt.MapFrom(src => (decimal)src.Reb))
                .ForMember(dest => dest.AvgAssists, opt => opt.MapFrom(src => (decimal)src.Ast))
                // Cálculo aproximado de Field Goals Made/Attempted a partir de pontos e porcentagem
                // Assumindo mix de 2pts (65%), 3pts (25%) e FT (10%), média ~2.2 pts por FG
                .ForMember(dest => dest.FieldGoalsMade, opt => opt.MapFrom(src =>
                    src.FgPct.HasValue && src.FgPct.Value > 0
                        ? (int)Math.Round((src.Pts * src.GamesPlayed * 0.85) / 2.2)
                        : 0))
                .ForMember(dest => dest.FieldGoalsAttempted, opt => opt.MapFrom(src =>
                    src.FgPct.HasValue && src.FgPct.Value > 0
                        ? (int)Math.Round((src.Pts * src.GamesPlayed * 0.85) / 2.2 / src.FgPct.Value)
                        : 0))
                // Cálculo de 3-Point Made/Attempted (assumindo ~25% dos pontos vêm de 3pts)
                .ForMember(dest => dest.ThreePointersMade, opt => opt.MapFrom(src =>
                    src.Fg3Pct.HasValue && src.Fg3Pct.Value > 0
                        ? (int)Math.Round((src.Pts * src.GamesPlayed * 0.25) / 3.0)
                        : 0))
                .ForMember(dest => dest.ThreePointersAttempted, opt => opt.MapFrom(src =>
                    src.Fg3Pct.HasValue && src.Fg3Pct.Value > 0
                        ? (int)Math.Round((src.Pts * src.GamesPlayed * 0.25) / 3.0 / src.Fg3Pct.Value)
                        : 0))
                // Cálculo de Free Throws Made/Attempted (assumindo ~15% dos pontos vêm de FTs)
                .ForMember(dest => dest.FreeThrowsMade, opt => opt.MapFrom(src =>
                    src.FtPct.HasValue && src.FtPct.Value > 0
                        ? (int)Math.Round(src.Pts * src.GamesPlayed * 0.15)
                        : 0))
                .ForMember(dest => dest.FreeThrowsAttempted, opt => opt.MapFrom(src =>
                    src.FtPct.HasValue && src.FtPct.Value > 0
                        ? (int)Math.Round((src.Pts * src.GamesPlayed * 0.15) / src.FtPct.Value)
                        : 0))
                .ForMember(dest => dest.FieldGoalPercentage, opt => opt.MapFrom(src => src.FgPct.HasValue ? (decimal)src.FgPct.Value : (decimal?)null))
                .ForMember(dest => dest.ThreePointPercentage, opt => opt.MapFrom(src => src.Fg3Pct.HasValue ? (decimal)src.Fg3Pct.Value : (decimal?)null))
                .ForMember(dest => dest.FreeThrowPercentage, opt => opt.MapFrom(src => src.FtPct.HasValue ? (decimal)src.FtPct.Value : (decimal?)null))
                .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore());

            // Ball Don't Lie Player Game Stats to Entity
            CreateMap<BallDontLiePlayerGameStatsDto, PlayerGameStats>()
                .ForMember(dest => dest.Id, opt => opt.Ignore())
                .ForMember(dest => dest.PlayerId, opt => opt.Ignore())
                .ForMember(dest => dest.GameId, opt => opt.Ignore())
                .ForMember(dest => dest.TeamId, opt => opt.Ignore())
                .ForMember(dest => dest.Points, opt => opt.MapFrom(src => src.Pts))
                .ForMember(dest => dest.TotalRebounds, opt => opt.MapFrom(src => src.Reb))
                .ForMember(dest => dest.Assists, opt => opt.MapFrom(src => src.Ast))
                .ForMember(dest => dest.Steals, opt => opt.MapFrom(src => src.Stl))
                .ForMember(dest => dest.Blocks, opt => opt.MapFrom(src => src.Blk))
                .ForMember(dest => dest.Turnovers, opt => opt.MapFrom(src => src.Turnover))
                .ForMember(dest => dest.FieldGoalsMade, opt => opt.MapFrom(src => src.Fgm))
                .ForMember(dest => dest.FieldGoalsAttempted, opt => opt.MapFrom(src => src.Fga))
                .ForMember(dest => dest.ThreePointersMade, opt => opt.MapFrom(src => src.Fg3m))
                .ForMember(dest => dest.ThreePointersAttempted, opt => opt.MapFrom(src => src.Fg3a))
                .ForMember(dest => dest.FreeThrowsMade, opt => opt.MapFrom(src => src.Ftm))
                .ForMember(dest => dest.FreeThrowsAttempted, opt => opt.MapFrom(src => src.Fta))
                .ForMember(dest => dest.OffensiveRebounds, opt => opt.MapFrom(src => src.Oreb))
                .ForMember(dest => dest.DefensiveRebounds, opt => opt.MapFrom(src => src.Dreb))
                .ForMember(dest => dest.PersonalFouls, opt => opt.MapFrom(src => src.Pf))
                .ForMember(dest => dest.MinutesPlayed, opt => opt.MapFrom(src => ParseMinutesFromString(src.Min)))
                .ForMember(dest => dest.SecondsPlayed, opt => opt.MapFrom(src => ParseSecondsFromString(src.Min)))
                .ForMember(dest => dest.DidNotPlay, opt => opt.MapFrom(src => string.IsNullOrEmpty(src.Min) || src.Min == "00:00"))
                .ForMember(dest => dest.CreatedAt, opt => opt.Ignore())
                .ForMember(dest => dest.UpdatedAt, opt => opt.Ignore());
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