using System;
using System.Collections.Generic;

namespace HoopGameNight.Core.DTOs.Response
{
    /// <summary>
    /// Estatísticas detalhadas de um jogador em um jogo específico
    /// </summary>
    public class PlayerGameStatsDetailedResponse
    {
        // IDs
        public int Id { get; set; }
        public int PlayerId { get; set; }
        public int GameId { get; set; }
        public int TeamId { get; set; }

        // Informações do Jogador
        public string PlayerFirstName { get; set; } = string.Empty;
        public string PlayerLastName { get; set; } = string.Empty;
        public string PlayerFullName { get; set; } = string.Empty;
        public int? JerseyNumber { get; set; }
        public string? Position { get; set; }

        // Informações do Jogo
        public DateTime GameDate { get; set; }
        public int HomeTeamId { get; set; }
        public int VisitorTeamId { get; set; }
        public int? HomeTeamScore { get; set; }
        public int? VisitorTeamScore { get; set; }
        public string GameStatus { get; set; } = string.Empty;

        // Time do Jogador
        public string TeamAbbreviation { get; set; } = string.Empty;
        public string TeamName { get; set; } = string.Empty;
        public string? TeamLogo { get; set; }

        // Time Adversário
        public string OpponentAbbreviation { get; set; } = string.Empty;
        public string OpponentName { get; set; } = string.Empty;

        // Resultado
        public string Result { get; set; } = string.Empty; // W, L, T
        public bool IsHome { get; set; }

        // Indicadores
        public bool DidNotPlay { get; set; }
        public bool IsStarter { get; set; }

        // Tempo de Jogo
        public int MinutesPlayed { get; set; }
        public int SecondsPlayed { get; set; }
        public string MinutesFormatted { get; set; } = string.Empty;

        // Pontuação
        public int Points { get; set; }
        public int FieldGoalsMade { get; set; }
        public int FieldGoalsAttempted { get; set; }
        public string FieldGoalsFormatted { get; set; } = string.Empty;
        public decimal FieldGoalPercentage { get; set; }

        // Três Pontos
        public int ThreePointersMade { get; set; }
        public int ThreePointersAttempted { get; set; }
        public string ThreePointersFormatted { get; set; } = string.Empty;
        public decimal ThreePointPercentage { get; set; }

        // Lances Livres
        public int FreeThrowsMade { get; set; }
        public int FreeThrowsAttempted { get; set; }
        public string FreeThrowsFormatted { get; set; } = string.Empty;
        public decimal FreeThrowPercentage { get; set; }

        // Rebotes
        public int OffensiveRebounds { get; set; }
        public int DefensiveRebounds { get; set; }
        public int TotalRebounds { get; set; }

        // Outras Estatísticas
        public int Assists { get; set; }
        public int Steals { get; set; }
        public int Blocks { get; set; }
        public int Turnovers { get; set; }
        public int PersonalFouls { get; set; }
        public int PlusMinus { get; set; }

        // Indicadores de Performance
        public bool DoubleDouble { get; set; }
        public bool TripleDouble { get; set; }
    }

    /// <summary>
    /// Resposta com estatísticas de todos os jogadores em um jogo
    /// </summary>
    public class GamePlayerStatsResponse
    {
        public int GameId { get; set; }
        public DateTime GameDate { get; set; }
        public string HomeTeam { get; set; } = string.Empty;
        public string VisitorTeam { get; set; } = string.Empty;
        public int? HomeScore { get; set; }
        public int? VisitorScore { get; set; }
        public List<PlayerGameStatsDetailedResponse> HomeTeamStats { get; set; } = new();
        public List<PlayerGameStatsDetailedResponse> VisitorTeamStats { get; set; } = new();
    }
}
