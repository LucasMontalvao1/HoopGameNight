using System.Collections.Generic;
using System.Threading.Tasks;
using HoopGameNight.Core.Models.Entities;

namespace HoopGameNight.Core.Interfaces.Services
{
    public interface IGameUpdateNotifier
    {
        Task NotifyGamesUpdatedAsync(IEnumerable<Game> games);
    }
}
