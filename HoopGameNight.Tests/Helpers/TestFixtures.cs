using AutoMapper;
using HoopGameNight.Core.DTOs.Response;
using HoopGameNight.Core.Interfaces.Repositories;
using HoopGameNight.Core.Interfaces.Services;
using HoopGameNight.Core.Models.Entities;
using HoopGameNight.Core.Services;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Moq;

namespace HoopGameNight.Tests.Helpers
{
    public class GameServiceTestFixture : IDisposable
    {
        public Mock<IGameRepository> MockGameRepository { get; }
        public Mock<ITeamRepository> MockTeamRepository { get; }
        public Mock<IEspnApiService> MockEspnApiService { get; }
        public Mock<IMapper> MockMapper { get; }
        public Mock<ILogger<GameService>> MockLogger { get; }
        public Mock<ICacheService> MockCacheService { get; }
        public GameService GameService { get; }

        public GameServiceTestFixture()
        {
            MockGameRepository = MockSetupHelper.CreateGameRepositoryMock();
            MockTeamRepository = MockSetupHelper.CreateTeamRepositoryMock();
            MockEspnApiService = MockSetupHelper.CreateEspnApiServiceMock();
            MockMapper = MockSetupHelper.CreateMapperMock();
            MockLogger = MockSetupHelper.CreateLoggerMock<GameService>();
            MockCacheService = MockSetupHelper.CreateCacheServiceMock();

            GameService = new GameService(
                MockGameRepository.Object,
                MockTeamRepository.Object,
                MockEspnApiService.Object,
                MockMapper.Object,
                MockCacheService.Object,
                MockLogger.Object
            );
        }

        public void Dispose()
        {
            // Não é mais necessário dispor do MemoryCache
        }

        #region Métodos Adicionados para os Testes

        /// <summary>
        /// Reseta todos os mocks para estado inicial
        /// </summary>
        public void ResetarMocks()
        {
            MockGameRepository.Reset();
            MockTeamRepository.Reset();
            MockEspnApiService.Reset();
            MockMapper.Reset();
            MockLogger.Reset();

            // Reconfigurar comportamento padrão básico
            ConfigurarComportamentoPadrao();
        }

        /// <summary>
        /// Limpa o cache de memória
        /// </summary>
        public void LimparCache()
        {
            // Com mocks, resetamos o comportamento
            MockCacheService.Reset();
        }

        /// <summary>
        /// Configura comportamento padrão dos mocks para evitar null reference exceptions
        /// </summary>
        private void ConfigurarComportamentoPadrao()
        {
            // GameRepository - comportamento padrão
            MockGameRepository
                .Setup(x => x.GetTodayGamesAsync())
                .ReturnsAsync(new List<Game>());

            MockGameRepository
                .Setup(x => x.GetByIdAsync(It.IsAny<int>()))
                .ReturnsAsync((Game?)null);

            // TeamRepository - comportamento padrão  
            MockTeamRepository
                .Setup(x => x.GetByExternalIdAsync(It.IsAny<int>()))
                .ReturnsAsync((Team?)null);

            // BallDontLieService - comportamento padrão
            // REMOVIDO: BallDontLie DTOs deletados - método comentado em IBallDontLieService
            // MockBallDontLieService
            //     .Setup(x => x.GetTodaysGamesAsync())
            //     .ReturnsAsync(new List<Core.DTOs.External.BallDontLie.BallDontLieGameDto>());

            // Mapper - comportamento padrão
            MockMapper
                .Setup(x => x.Map<List<GameResponse>>(It.IsAny<List<Game>>()))
                .Returns(new List<GameResponse>());

            MockMapper
                .Setup(x => x.Map<GameResponse>(It.IsAny<Game>()))
                .Returns((GameResponse?)null);
        }

        #endregion
    }
}