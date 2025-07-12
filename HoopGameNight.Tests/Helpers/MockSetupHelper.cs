using AutoMapper;
using HoopGameNight.Core.DTOs.External.BallDontLie;
using HoopGameNight.Core.DTOs.Response;
using HoopGameNight.Core.Interfaces.Repositories;
using HoopGameNight.Core.Interfaces.Services;
using HoopGameNight.Core.Models.Entities;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;

namespace HoopGameNight.Tests.Helpers
{
    public static class MockSetupHelper
    {
        #region Repository Mocks

        public static Mock<IGameRepository> CreateGameRepositoryMock()
        {
            var mock = new Mock<IGameRepository>();

            // Setup default behaviors
            mock.Setup(x => x.GetTodayGamesAsync())
                .ReturnsAsync(new List<Game>());

            mock.Setup(x => x.GetByIdAsync(It.IsAny<int>()))
                .ReturnsAsync((Game?)null);

            mock.Setup(x => x.ExistsAsync(It.IsAny<int>()))
                .ReturnsAsync(false);

            return mock;
        }

        public static Mock<ITeamRepository> CreateTeamRepositoryMock()
        {
            var mock = new Mock<ITeamRepository>();

            mock.Setup(x => x.GetAllAsync())
                .ReturnsAsync(new List<Team>());

            mock.Setup(x => x.GetByIdAsync(It.IsAny<int>()))
                .ReturnsAsync((Team?)null);

            mock.Setup(x => x.GetByExternalIdAsync(It.IsAny<int>()))
                .ReturnsAsync((Team?)null);

            return mock;
        }

        public static Mock<IPlayerRepository> CreatePlayerRepositoryMock()
        {
            var mock = new Mock<IPlayerRepository>();

            mock.Setup(x => x.GetByIdAsync(It.IsAny<int>()))
                .ReturnsAsync((Player?)null);

            mock.Setup(x => x.ExistsAsync(It.IsAny<int>()))
                .ReturnsAsync(false);

            return mock;
        }

        #endregion

        #region Service Mocks

        public static Mock<IBallDontLieService> CreateBallDontLieServiceMock()
        {
            var mock = new Mock<IBallDontLieService>();

            mock.Setup(x => x.GetTodaysGamesAsync())
                .ReturnsAsync(new List<BallDontLieGameDto>());

            mock.Setup(x => x.GetAllTeamsAsync())
                .ReturnsAsync(new List<BallDontLieTeamDto>());

            mock.Setup(x => x.SearchPlayersAsync(It.IsAny<string>(), It.IsAny<int>()))
                .ReturnsAsync(new List<BallDontLiePlayerDto>());

            return mock;
        }

        #endregion

        #region Infrastructure Mocks

        public static Mock<IMapper> CreateMapperMock()
        {
            var mock = new Mock<IMapper>();

            // Setup common mappings with default returns
            mock.Setup(x => x.Map<List<GameResponse>>(It.IsAny<List<Game>>()))
                .Returns(new List<GameResponse>());

            mock.Setup(x => x.Map<List<TeamResponse>>(It.IsAny<List<Team>>()))
                .Returns(new List<TeamResponse>());

            mock.Setup(x => x.Map<List<PlayerResponse>>(It.IsAny<List<Player>>()))
                .Returns(new List<PlayerResponse>());

            return mock;
        }

        public static IMemoryCache CreateMemoryCache()
        {
            return new MemoryCache(new MemoryCacheOptions());
        }

        public static Mock<ILogger<T>> CreateLoggerMock<T>()
        {
            return new Mock<ILogger<T>>();
        }

        #endregion

        #region Verification Helpers

        public static void VerifyLoggerCalled<T>(Mock<ILogger<T>> loggerMock, LogLevel logLevel, string message)
        {
            loggerMock.Verify(
                x => x.Log(
                    logLevel,
                    It.IsAny<EventId>(),
                    It.Is<It.IsAnyType>((v, t) => v.ToString()!.Contains(message)),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.AtLeastOnce);
        }

        public static void VerifyLoggerCalledWithException<T>(Mock<ILogger<T>> loggerMock, LogLevel logLevel)
        {
            loggerMock.Verify(
                x => x.Log(
                    logLevel,
                    It.IsAny<EventId>(),
                    It.IsAny<It.IsAnyType>(),
                    It.IsAny<Exception>(),
                    It.IsAny<Func<It.IsAnyType, Exception?, string>>()),
                Times.AtLeastOnce);
        }

        #endregion
    }
}