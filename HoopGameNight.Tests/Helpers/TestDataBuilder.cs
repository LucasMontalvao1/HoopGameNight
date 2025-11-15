using HoopGameNight.Core.DTOs.Response;
using HoopGameNight.Core.Enums;
using HoopGameNight.Core.Models.Entities;

namespace HoopGameNight.Tests.Helpers
{
    public static class TestDataBuilder
    {
        #region Game Builders

        public static Game CreateGame(int id = 1, int homeTeamId = 1, int visitorTeamId = 2)
        {
            return new Game
            {
                Id = id,
                ExternalId = $"test-game-{id}",
                Date = DateTime.Today,
                DateTime = DateTime.Today.AddHours(20),
                HomeTeamId = homeTeamId,
                VisitorTeamId = visitorTeamId,
                HomeTeamScore = 110,
                VisitorTeamScore = 105,
                Status = GameStatus.Final,
                Period = 4,
                TimeRemaining = "Final",
                PostSeason = false,
                Season = 2024,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
        }

        public static List<Game> CreateGames(int count)
        {
            return Enumerable.Range(1, count)
                .Select(i => CreateGame(i, i, i + 10))
                .ToList();
        }

        public static GameResponse CreateGameResponse(int id = 1)
        {
            return new GameResponse
            {
                Id = id,
                Date = DateTime.Today,
                DateTime = DateTime.Today.AddHours(20),
                HomeTeam = CreateTeamSummary(1, "Lakers"),
                VisitorTeam = CreateTeamSummary(2, "Warriors"),
                HomeTeamScore = 110,
                VisitorTeamScore = 105,
                Status = "Final",
                StatusDisplay = "Final",
                Period = 4,
                TimeRemaining = "Final",
                PostSeason = false,
                Season = 2024,
                Score = "110 - 105",
                GameTitle = "GSW @ LAL",
                IsLive = false,
                IsCompleted = true
            };
        }

        #endregion

        #region Team Builders

        public static Team CreateTeam(int id = 1, string name = "Lakers")
        {
            return new Team
            {
                Id = id,
                ExternalId = id + 100,
                Name = name,
                FullName = $"Sample {name}",
                Abbreviation = name[..Math.Min(3, name.Length)].ToUpper(),
                City = "Sample City",
                Conference = Conference.West,
                Division = "Pacific",
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
        }

        public static List<Team> CreateTeams(int count)
        {
            var teamNames = new[] { "Lakers", "Warriors", "Bulls", "Celtics", "Heat", "Spurs", "Nets", "Knicks" };

            return Enumerable.Range(1, count)
                .Select(i => CreateTeam(i, teamNames[(i - 1) % teamNames.Length]))
                .ToList();
        }

        public static TeamResponse CreateTeamResponse(int id = 1, string name = "Lakers")
        {
            return new TeamResponse
            {
                Id = id,
                Name = name,
                FullName = $"Sample {name}",
                Abbreviation = name[..Math.Min(3, name.Length)].ToUpper(),
                City = "Sample City",
                Conference = "West",
                ConferenceDisplay = "Western Conference",
                Division = "Pacific",
                DisplayName = $"Sample City {name}"
            };
        }

        public static TeamSummaryResponse CreateTeamSummary(int id, string name)
        {
            return new TeamSummaryResponse
            {
                Id = id,
                Name = name,
                Abbreviation = name[..Math.Min(3, name.Length)].ToUpper(),
                City = "Sample City",
                DisplayName = $"Sample City {name}"
            };
        }

        #endregion

        #region Player Builders

        public static Player CreatePlayer(int id = 1, int? teamId = 1)
        {
            return new Player
            {
                Id = id,
                ExternalId = id + 1000,
                FirstName = $"Player{id}",
                LastName = "Test",
                Position = PlayerPosition.PG,
                HeightFeet = 6,
                HeightInches = 6,
                WeightPounds = 200,
                TeamId = teamId,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };
        }

        public static List<Player> CreatePlayers(int count, int? teamId = null)
        {
            return Enumerable.Range(1, count)
                .Select(i => CreatePlayer(i, teamId ?? i))
                .ToList();
        }

        public static PlayerResponse CreatePlayerResponse(int id = 1)
        {
            return new PlayerResponse
            {
                Id = id,
                FirstName = $"Player{id}",
                LastName = "Test",
                FullName = $"Player{id} Test",
                Position = "PG",
                PositionDisplay = "Point Guard",
                Height = "6'6\"",
                Weight = "200 lbs",
                Team = CreateTeamSummary(1, "Lakers"),
                DisplayName = $"Player{id} Test (PG)"
            };
        }

        #endregion

        #region Date Helpers

        public static DateTime GetTestDate(int daysFromToday = 0)
        {
            return DateTime.Today.AddDays(daysFromToday);
        }

        public static DateTime GetTestDateTime(int daysFromToday = 0, int hours = 20)
        {
            return DateTime.Today.AddDays(daysFromToday).AddHours(hours);
        }

        #endregion
    }
}