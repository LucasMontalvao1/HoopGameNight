using HoopGameNight.Core.DTOs.External.ESPN;
using HoopGameNight.Core.DTOs.Response;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace HoopGameNight.Core.Interfaces.Services
{
    public interface IPlayerStatsService
    {
        // === LEITURA (Cache -> DB/View -> ESPN) ===
        Task<PlayerSeasonStatsResponse?> GetPlayerSeasonStatsAsync(int playerId, int season);
        Task<PlayerGameStatsDetailedResponse?> GetPlayerGameStatsAsync(int playerId, int gameId); // Por jogo específico
        Task<IEnumerable<PlayerGameStatsDetailedResponse>> GetPlayerRecentGamesAsync(int playerId, int limit = 5);
        
        // Novos métodos com DTOs simplificados (Cache -> ESPN -> Mapeamento)
        Task<PlayerGamelogResponse?> GetPlayerGamelogFromEspnAsync(int playerId); 
        Task<PlayerSplitsResponse?> GetPlayerSplitsFromEspnAsync(int playerId);
        Task<PlayerCareerResponse?> GetPlayerCareerStatsFromEspnAsync(int playerId);

        // === SINCRONIZAÇÃO (Manual trigger) ===
        Task<bool> SyncPlayerSeasonStatsAsync(int playerId, int season); // Puxa todos os jogos da season e calcula/salva
        Task<bool> SyncPlayerGameStatsAsync(int playerId, int gameId);   // Puxa um jogo específico
        
        // Feature de consulta direta (debug) criada anteriormente
        Task<object?> GetPlayerGameStatsDirectAsync(int playerId, int gameId);

        // Legacy / Background Service Support
        Task<bool> UpdatePlayerCareerStatsAsync(int playerId);
    }
}