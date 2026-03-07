using System.Collections.Generic;

namespace HoopGameNight.Core.Extensions
{
    public static class TeamExtensions
    {
        public static string GetTeamLogoUrl(string abbreviation)
        {
            if (string.IsNullOrEmpty(abbreviation)) return string.Empty;

            var logoMappings = new Dictionary<string, string>
            {
                { "ATL", "atl" }, { "BOS", "bos" }, { "BKN", "bkn" }, { "CHA", "cha" }, 
                { "CHI", "chi" }, { "CLE", "cle" }, { "DAL", "dal" }, { "DEN", "den" }, 
                { "DET", "det" }, { "GS", "gs" }, { "GSW", "gs" }, { "HOU", "hou" }, 
                { "IND", "ind" }, { "LAC", "lac" }, { "LAL", "lal" }, { "MEM", "mem" }, 
                { "MIA", "mia" }, { "MIL", "mil" }, { "MIN", "min" }, { "NOP", "no" }, 
                { "NO", "no" }, { "NY", "ny" }, { "NYK", "ny" }, { "OKC", "okc" }, 
                { "ORL", "orl" }, { "PHI", "phi" }, { "PHX", "phx" }, { "POR", "por" }, 
                { "SAC", "sac" }, { "SA", "sa" }, { "SAS", "sa" }, { "TOR", "tor" }, 
                { "UTAH", "utah" }, { "UTA", "utah" }, { "WSH", "wsh" }, { "WAS", "wsh" }
            };

            var upperAbbr = abbreviation.ToUpper();
            var logoAbbr = logoMappings.ContainsKey(upperAbbr) ? logoMappings[upperAbbr] : abbreviation.ToLower();

            return $"https://a.espncdn.com/i/teamlogos/nba/500/{logoAbbr}.png";
        }
    }
}
