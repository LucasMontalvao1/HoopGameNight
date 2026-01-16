using System.Collections.Generic;

namespace HoopGameNight.Core.DTOs.Response
{
    /// <summary>
    /// Response DTO for player gamelog
    /// Contains list of games with statistics (reuses existing PlayerRecentGameResponse)
    /// </summary>
    public class PlayerGamelogResponse
    {
        public int PlayerId { get; set; }
        public string PlayerName { get; set; } = string.Empty;
        public int Season { get; set; }
        
        /// <summary>
        /// List of games with statistics (reuses existing DTO)
        /// </summary>
        public List<PlayerRecentGameResponse> Games { get; set; } = new();
    }
}
