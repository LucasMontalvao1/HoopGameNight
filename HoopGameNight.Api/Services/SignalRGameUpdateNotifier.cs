using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using HoopGameNight.Core.Models.Entities;
using HoopGameNight.Core.Interfaces.Services;
using HoopGameNight.Api.Hubs;

namespace HoopGameNight.Api.Services
{
    public class SignalRGameUpdateNotifier : IGameUpdateNotifier
    {
        private readonly IHubContext<GameHub> _hubContext;
        private readonly AutoMapper.IMapper _mapper;

        public SignalRGameUpdateNotifier(IHubContext<GameHub> hubContext, AutoMapper.IMapper mapper)
        {
            _hubContext = hubContext;
            _mapper = mapper;
        }

        public async Task NotifyGamesUpdatedAsync(IEnumerable<Game> games)
        {
            if (games == null) return;
            
            var responses = _mapper.Map<IEnumerable<HoopGameNight.Core.DTOs.Response.GameResponse>>(games);
            
            // Envia para o grupo Genérico "DashboardGroup" (que escuta "ReceiveGameUpdates")
            await _hubContext.Clients.Group("DashboardGroup").SendAsync("ReceiveGameUpdates", responses);
            
            // Opcional no futuro:
            // foreach (var game in games)
            //    await _hubContext.Clients.Group($"Game_{game.Id}").SendAsync("ReceiveGameDetails", game);
        }
    }
}
