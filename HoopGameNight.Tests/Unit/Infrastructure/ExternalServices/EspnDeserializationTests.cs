using FluentAssertions;
using HoopGameNight.Core.DTOs.External.ESPN;
using System.Text.Json;
using Xunit;

namespace HoopGameNight.Tests.Unit.Infrastructure.ExternalServices
{
    public class EspnDeserializationTests
    {
        private readonly string _jsonSamplePath;

        public EspnDeserializationTests()
        {
            // O arquivo de sample deve ser copiado para o diretório de saída
            _jsonSamplePath = Path.Combine(AppContext.BaseDirectory, "Resources", "espn_scoreboard_sample.json");
        }

        [Fact]
        public void Deserialize_Scoreboard_ShouldParse_Games_And_Teams()
        {
            // Arrange
            if (!File.Exists(_jsonSamplePath))
                Assert.Fail($"Arquivo de amostra não encontrado: {_jsonSamplePath}");

            var json = File.ReadAllText(_jsonSamplePath);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };

            // Act
            var scoreboard = JsonSerializer.Deserialize<EspnScoreboardResponse>(json, options);

            // Assert
            scoreboard.Should().NotBeNull();
            scoreboard.Events.Should().HaveCount(1);
            
            var gameEvent = scoreboard.Events.First();
            gameEvent.ShortName.Should().Be("LAL @ BOS");
            gameEvent.Date.Should().Be("2024-02-02T00:30:00Z");

            var competition = gameEvent.Competitions.First();
            competition.Competitors.Should().HaveCount(2);

            var homeTeam = competition.Competitors.FirstOrDefault(c => c.HomeAway == "home");
            homeTeam.Should().NotBeNull();
            homeTeam.Team.Abbreviation.Should().Be("BOS");
            homeTeam.Score.ToString().Should().Be("105");

            var awayTeam = competition.Competitors.FirstOrDefault(c => c.HomeAway == "away");
            awayTeam.Should().NotBeNull();
            awayTeam.Team.Abbreviation.Should().Be("LAL");
            awayTeam.Score.ToString().Should().Be("114");

            competition.Status?.Type?.Name.Should().Be("STATUS_FINAL");
        }
    }
}
