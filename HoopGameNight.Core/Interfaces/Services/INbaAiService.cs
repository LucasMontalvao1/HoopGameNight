using HoopGameNight.Core.DTOs.Request;
using HoopGameNight.Core.DTOs.Response;
using System.Threading.Tasks;

namespace HoopGameNight.Core.Interfaces.Services
{
    public interface INbaAiService
    {
        Task<AskResponse> AskAsync(AskRequest request);
    }
}