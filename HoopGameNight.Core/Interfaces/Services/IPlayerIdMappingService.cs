namespace HoopGameNight.Core.Interfaces.Services
{
    public interface IPlayerIdMappingService
    {
        Task<string?> GetEspnPlayerIdAsync(int ballDontLiePlayerId);
        void ClearCache();
    }
}
