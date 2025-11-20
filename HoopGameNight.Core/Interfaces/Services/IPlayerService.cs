using HoopGameNight.Core.DTOs.Request;
using HoopGameNight.Core.DTOs.Response;

namespace HoopGameNight.Core.Interfaces.Services
{
    public interface IPlayerService
    {
        Task<(List<PlayerResponse> Players, int TotalCount)> SearchPlayersAsync(SearchPlayerRequest request);
        Task<(List<PlayerResponse> Players, int TotalCount)> GetPlayersByTeamAsync(int teamId, int page, int pageSize);
        Task<(List<PlayerResponse> Players, int TotalCount)> GetAllPlayersAsync(int page, int pageSize);
        Task<PlayerResponse?> GetPlayerByIdAsync(int id);
        Task SyncPlayersAsync(string? searchTerm = null);
    }
}