using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using HoopGameNight.Core.DTOs.Request;
using HoopGameNight.Core.DTOs.Response;
using HoopGameNight.Core.DTOs.External.ESPN;

namespace HoopGameNight.Core.Interfaces.Services
{
    public interface IGameService
    {
        /// <summary>
        /// Busca jogos do dia atual
        /// </summary>
        Task<List<GameResponse>> GetTodayGamesAsync();

        /// <summary>
        /// Busca jogos por data específica
        /// </summary>
        Task<List<GameResponse>> GetGamesByDateAsync(DateTime date);

        /// <summary>
        /// Busca jogos em um intervalo de datas (otimizado para evitar N+1)
        /// </summary>
        Task<List<GameResponse>> GetGamesByDateRangeAsync(DateTime startDate, DateTime endDate);

        /// <summary>
        /// Busca jogos com paginação e filtros
        /// </summary>
        Task<(List<GameResponse> Games, int TotalCount)> GetGamesAsync(GetGamesRequest request);

        /// <summary>
        /// Busca jogos de um time específico com paginação
        /// </summary>
        Task<(List<GameResponse> Games, int TotalCount)> GetGamesByTeamAsync(int teamId, int page, int pageSize);

        /// <summary>
        /// Busca um jogo por ID
        /// </summary>
        Task<GameResponse?> GetGameByIdAsync(int id);

        /// <summary>
        /// Busca jogos de múltiplos times em um período
        /// </summary>
        Task<MultipleTeamsGamesResponse> GetGamesForMultipleTeamsAsync(GetMultipleTeamsGamesRequest request);

        /// <summary>
        /// Busca próximos jogos de um time
        /// </summary>
        Task<List<GameResponse>> GetUpcomingGamesForTeamAsync(int teamId, int days = 7);

        /// <summary>
        /// Busca jogos recentes de um time
        /// </summary>
        Task<List<GameResponse>> GetRecentGamesForTeamAsync(int teamId, int days = 7);

        /// <summary>
        /// Sincroniza jogos por data específica
        /// </summary>
        Task<int> SyncGamesByDateAsync(DateTime date);

        /// <summary>
        /// Sincroniza jogos para um período específico
        /// </summary>
        Task<int> SyncGamesForPeriodAsync(DateTime startDate, DateTime endDate);

        /// <summary>
        /// Sincroniza jogos do dia atual
        /// </summary>
        Task SyncTodayGamesAsync();

        /// <summary>
        /// Sincroniza jogos futuros da ESPN e salva no banco
        /// </summary>
        Task<int> SyncFutureGamesAsync(int days = 30);

        // Métodos para Estatísticas e Líderes
        Task<EspnBoxscoreDto?> GetGameBoxscoreAsync(int gameId);
        Task<GameLeadersResponse?> GetGameLeadersAsync(int gameId);
        Task<TeamSeasonLeadersResponse?> GetTeamLeadersAsync(int teamId);

        /// <summary>
        /// Sincroniza um jogo específico pelo ID da ESPN
        /// </summary>
        Task<int> SyncGameByIdAsync(string gameId);
    }
}