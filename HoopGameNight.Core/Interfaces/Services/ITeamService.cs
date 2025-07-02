﻿using HoopGameNight.Core.DTOs.Response;

namespace HoopGameNight.Core.Interfaces.Services
{
    public interface ITeamService
    {
        Task<List<TeamResponse>> GetAllTeamsAsync();
        Task<TeamResponse?> GetTeamByIdAsync(int id);
        Task<TeamResponse?> GetTeamByAbbreviationAsync(string abbreviation);
        Task SyncAllTeamsAsync();
    }
}