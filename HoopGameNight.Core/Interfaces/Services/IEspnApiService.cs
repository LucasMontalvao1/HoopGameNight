using HoopGameNight.Core.DTOs.External;
using HoopGameNight.Core.DTOs.External.ESPN;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HoopGameNight.Core.Interfaces.Services
{
    /// <summary>
    /// Interface completa para ESPN API Service
    /// </summary>
    public interface IEspnApiService
    {
        #region Games/Events - Jogos e Eventos

        /// <summary>
        /// Busca jogos por data específica
        /// </summary>
        Task<List<EspnGameDto>> GetGamesByDateAsync(DateTime date);

        /// <summary>
        /// Busca jogos futuros (próximos N dias)
        /// </summary>
        Task<List<EspnGameDto>> GetFutureGamesAsync(int days = 7);

        /// <summary>
        /// Busca jogos de um time por período
        /// </summary>
        Task<List<EspnGameDto>> GetTeamScheduleAsync(int teamId, DateTime startDate, DateTime endDate);

        /// <summary>
        /// Busca detalhes completos de um jogo (summary)
        /// </summary>
        Task<EspnGameDetailDto?> GetGameEventAsync(string gameId);

        /// <summary>
        /// Busca evento via Core API
        /// </summary>
        Task<EspnEventDto?> GetCoreEventAsync(string gameId);

        /// <summary>
        /// Busca status atual de um jogo
        /// </summary>
        Task<EspnGameStatusDto?> GetGameStatusAsync(string gameId);

        /// <summary>
        /// Busca líderes de um jogo específico
        /// </summary>
        Task<EspnGameLeadersDto?> GetGameLeadersAsync(string gameId);

        /// <summary>
        /// Busca boxscore completo de um jogo
        /// </summary>
        Task<EspnBoxscoreDto?> GetGameBoxscoreAsync(string gameId);

        #endregion

        #region Teams - Times

        /// <summary>
        /// Busca todos os times da NBA
        /// </summary>
        Task<List<EspnTeamDto>> GetAllTeamsAsync();

        /// <summary>
        /// Busca estatísticas de um time na temporada
        /// </summary>
        Task<EspnTeamStatisticsDto?> GetTeamStatisticsAsync(string teamId);

        /// <summary>
        /// Busca líderes estatísticos de um time na temporada
        /// </summary>
        Task<EspnTeamLeadersDto?> GetTeamLeadersAsync(string teamId);

        /// <summary>
        /// Busca roster completo de um time
        /// </summary>
        Task<List<EspnPlayerDetailsDto>> GetTeamRosterAsync(string teamId);

        #endregion

        #region Players - Jogadores

        /// <summary>
        /// Busca todos os jogadores (lista de referências)
        /// </summary>
        Task<List<EspnAthleteRefDto>> GetAllPlayersAsync();

        /// <summary>
        /// Busca detalhes completos de um jogador (Carreira)
        /// </summary>
        Task<EspnPlayerDetailsDto?> GetPlayerDetailsAsync(string playerId);

        /// <summary>
        /// Busca estatísticas atuais de um jogador (Temporada)
        /// </summary>
        Task<EspnPlayerStatsDto?> GetPlayerStatsAsync(string playerId);

        /// <summary>
        /// Busca estatísticas de uma temporada específica
        /// </summary>
        Task<EspnPlayerStatsDto?> GetPlayerSeasonStatsAsync(string playerId, int season, int seasonType = 2);

        /// <summary>
        /// Busca estatísticas de carreira completa
        /// </summary>
        Task<List<EspnPlayerStatsDto>> GetPlayerCareerStatsAsync(string playerId);

        /// <summary>
        /// Busca gamelog (histórico de jogos) de um jogador - Usado para stats por Jogo
        /// </summary>
        Task<EspnPlayerGamelogDto?> GetPlayerGamelogAsync(string playerId);

        /// <summary>
        /// Busca estatísticas detalhadas de um jogador em um jogo específico
        /// </summary>
        Task<EspnPlayerStatsDto?> GetPlayerGameStatsAsync(string playerId, string gameId);

        /// <summary>
        /// Busca splits (home/away, etc) de um jogador
        /// </summary>
        Task<EspnPlayerSplitsDto?> GetPlayerSplitsAsync(string playerId);

        #endregion

        #region Standings - Classificação

        /// <summary>
        /// Busca classificação por conferência
        /// </summary>
        Task<EspnStandingsDto?> GetConferenceStandingsAsync();

        /// <summary>
        /// Busca classificação por divisão
        /// </summary>
        Task<EspnStandingsDto?> GetDivisionStandingsAsync();

        #endregion

        #region Leaders - Líderes

        /// <summary>
        /// Busca líderes da liga na temporada
        /// </summary>
        Task<EspnLeadersDto?> GetLeagueLeadersAsync();

        #endregion

        #region Utility - Utilitários

        /// <summary>
        /// Verifica se a API está disponível
        /// </summary>
        Task<bool> IsApiAvailableAsync();

        #endregion
    }
}