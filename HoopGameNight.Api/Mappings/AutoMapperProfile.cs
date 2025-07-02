using AutoMapper;
using HoopGameNight.Core.DTOs.External.BallDontLie;
using HoopGameNight.Core.DTOs.Response;
using HoopGameNight.Core.Enums;
using HoopGameNight.Core.Extensions;
using HoopGameNight.Core.Models.Entities;

namespace HoopGameNight.Api.Mappings
{
    public class AutoMapperProfile : Profile
    {
        public AutoMapperProfile()
        {
            CreateEntityToResponseMaps();
            CreateExternalToEntityMaps();
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
                .ForMember(dest => dest.PositionDisplay, opt => opt.MapFrom(src => src.Position.HasValue ? src.Position.Value.GetDescription() : null));

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
                .ForMember(dest => dest.Position, opt => opt.MapFrom(src => ParsePlayerPosition(src.Position)))
                .ForMember(dest => dest.TeamId, opt => opt.MapFrom(src => src.Team != null ? (int?)src.Team.Id : null))
                .ForMember(dest => dest.Team, opt => opt.Ignore())
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
    }
}