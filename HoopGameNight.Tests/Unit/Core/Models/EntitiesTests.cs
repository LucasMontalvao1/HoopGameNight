using FluentAssertions;
using HoopGameNight.Core.Enums;
using HoopGameNight.Core.Models.Entities;
using HoopGameNight.Tests.Helpers;
using System;
using System.Threading;
using Xunit;

namespace HoopGameNight.Tests.Unit.Core.Models
{
    public class EntitiesTests
    {
        #region Testes de BaseEntity

        [Fact(DisplayName = "Deve inicializar com timestamp atual quando criado")]
        public async Task DeveInicializarComTimestampAtual_QuandoCriado()
        {
            // Arrange
            var tempoAntes = DateTime.UtcNow;

            // Act
            var entidade = new EntidadeTeste();
            var tempoDepois = DateTime.UtcNow;

            // Assert
            entidade.CreatedAt.Should().BeAfter(tempoAntes.AddMilliseconds(-100));
            entidade.CreatedAt.Should().BeBefore(tempoDepois.AddMilliseconds(100));
            entidade.UpdatedAt.Should().Be(entidade.CreatedAt);
        }

        [Fact(DisplayName = "Deve atualizar UpdatedAt quando UpdateTimestamp é chamado")]
        public async Task DeveAtualizarUpdatedAt_QuandoUpdateTimestampChamado()
        {
            // Arrange
            var entidade = new EntidadeTeste();
            var timestampOriginal = entidade.UpdatedAt;
            var createdAtOriginal = entidade.CreatedAt;

            // Aguarda para garantir diferença no timestamp
            await Task.Delay(50);

            // Act
            entidade.UpdateTimestamp();

            // Assert
            entidade.UpdatedAt.Should().BeAfter(timestampOriginal);
            entidade.CreatedAt.Should().Be(createdAtOriginal, "CreatedAt não deve mudar");
        }

        #endregion

        #region Testes da Entidade Game

        [Fact(DisplayName = "IsToday deve retornar true quando jogo é hoje")]
        public void DeveRetornarTrue_QuandoJogoEhHoje()
        {
            // Arrange
            var jogo = TestDataBuilder.CreateGame();
            jogo.Date = DateTime.Today;

            // Act
            var resultado = jogo.IsToday;

            // Assert
            resultado.Should().BeTrue();
        }

        [Fact(DisplayName = "IsToday deve retornar false quando jogo não é hoje")]
        public void DeveRetornarFalse_QuandoJogoNaoEhHoje()
        {
            // Arrange
            var jogo = TestDataBuilder.CreateGame();
            jogo.Date = DateTime.Today.AddDays(-1);

            // Act
            var resultado = jogo.IsToday;

            // Assert
            resultado.Should().BeFalse();
        }

        [Fact(DisplayName = "IsTomorrow deve retornar true quando jogo é amanhã")]
        public void DeveRetornarTrue_QuandoJogoEhAmanha()
        {
            // Arrange
            var jogo = TestDataBuilder.CreateGame();
            jogo.Date = DateTime.Today.AddDays(1);

            // Act
            var resultado = jogo.IsTomorrow;

            // Assert
            resultado.Should().BeTrue();
        }

        [Theory(DisplayName = "Propriedades de status devem funcionar corretamente")]
        [InlineData(GameStatus.Live, true, false, false)]
        [InlineData(GameStatus.Final, false, true, false)]
        [InlineData(GameStatus.Scheduled, false, false, true)]
        public void PropriedadesStatus_DevemFuncionarCorretamente(
            GameStatus status, bool esperadoLive, bool esperadoCompleted, bool esperadoScheduled)
        {
            // Arrange
            var jogo = TestDataBuilder.CreateGame();
            jogo.Status = status;

            // Act & Assert
            jogo.IsLive.Should().Be(esperadoLive);
            jogo.IsCompleted.Should().Be(esperadoCompleted);
            jogo.IsScheduled.Should().Be(esperadoScheduled);
        }

        [Fact(DisplayName = "Score deve retornar placar formatado quando ambos times têm pontos")]
        public void DeveRetornarPlacarFormatado_QuandoAmbosPontosDefinidos()
        {
            // Arrange
            var jogo = TestDataBuilder.CreateGame();
            jogo.HomeTeamScore = 110;
            jogo.VisitorTeamScore = 105;

            // Act
            var placar = jogo.Score;

            // Assert
            placar.Should().Be("110 - 105");
        }

        [Fact(DisplayName = "Score deve retornar 0 - 0 quando pontos são nulos")]
        public void DeveRetornarPlacarZero_QuandoPontosNulos()
        {
            // Arrange
            var jogo = TestDataBuilder.CreateGame();
            jogo.HomeTeamScore = null;
            jogo.VisitorTeamScore = null;

            // Act
            var placar = jogo.Score;

            // Assert
            placar.Should().Be("0 - 0");
        }

        [Fact(DisplayName = "GameTitle deve retornar título formatado com abreviações")]
        public void DeveRetornarTituloFormatado_ComAbreviacoes()
        {
            // Arrange
            var jogo = TestDataBuilder.CreateGame();
            jogo.HomeTeam = TestDataBuilder.CreateTeam(1, "Lakers");
            jogo.HomeTeam.Abbreviation = "LAL";
            jogo.VisitorTeam = TestDataBuilder.CreateTeam(2, "Warriors");
            jogo.VisitorTeam.Abbreviation = "GSW";

            // Act
            var titulo = jogo.GameTitle;

            // Assert
            titulo.Should().Be("GSW @ LAL");
        }

        [Fact(DisplayName = "GameTitle deve retornar título genérico quando times são nulos")]
        public void DeveRetornarTituloGenerico_QuandoTimesNulos()
        {
            // Arrange
            var jogo = TestDataBuilder.CreateGame();
            jogo.HomeTeam = null;
            jogo.VisitorTeam = null;
            jogo.HomeTeamId = 1;
            jogo.VisitorTeamId = 2;

            // Act
            var titulo = jogo.GameTitle;

            // Assert
            titulo.Should().Be("Team 2 @ Team 1");
        }

        [Fact(DisplayName = "WinningTeam deve retornar time da casa quando tem maior pontuação")]
        public void DeveRetornarTimeCasa_QuandoTemMaiorPontuacao()
        {
            // Arrange
            var timeCasa = TestDataBuilder.CreateTeam(1, "Lakers");
            var timeVisitante = TestDataBuilder.CreateTeam(2, "Warriors");

            var jogo = TestDataBuilder.CreateGame();
            jogo.Status = GameStatus.Final;
            jogo.HomeTeam = timeCasa;
            jogo.VisitorTeam = timeVisitante;
            jogo.HomeTeamScore = 110;
            jogo.VisitorTeamScore = 105;

            // Act
            var vencedor = jogo.WinningTeam;

            // Assert
            vencedor.Should().Be(timeCasa);
        }

        [Fact(DisplayName = "WinningTeam deve retornar visitante quando tem maior pontuação")]
        public void DeveRetornarTimeVisitante_QuandoTemMaiorPontuacao()
        {
            // Arrange
            var timeCasa = TestDataBuilder.CreateTeam(1, "Lakers");
            var timeVisitante = TestDataBuilder.CreateTeam(2, "Warriors");

            var jogo = TestDataBuilder.CreateGame();
            jogo.Status = GameStatus.Final;
            jogo.HomeTeam = timeCasa;
            jogo.VisitorTeam = timeVisitante;
            jogo.HomeTeamScore = 105;
            jogo.VisitorTeamScore = 110;

            // Act
            var vencedor = jogo.WinningTeam;

            // Assert
            vencedor.Should().Be(timeVisitante);
        }

        [Theory(DisplayName = "WinningTeam deve retornar null em cenários específicos")]
        [InlineData(GameStatus.Live, 110, 105, "jogo ainda está em andamento")]
        [InlineData(GameStatus.Final, 110, 110, "placar está empatado")]
        [InlineData(GameStatus.Scheduled, 0, 0, "jogo ainda não começou")]
        public void DeveRetornarNull_EmCenariosEspecificos(
            GameStatus status, int pontoCasa, int pontoVisitante, string razao)
        {
            // Arrange
            var jogo = TestDataBuilder.CreateGame();
            jogo.Status = status;
            jogo.HomeTeamScore = pontoCasa;
            jogo.VisitorTeamScore = pontoVisitante;
            jogo.HomeTeam = TestDataBuilder.CreateTeam(1, "Lakers");
            jogo.VisitorTeam = TestDataBuilder.CreateTeam(2, "Warriors");

            // Act
            var vencedor = jogo.WinningTeam;

            // Assert
            vencedor.Should().BeNull(razao);
        }

        [Theory(DisplayName = "IsValid deve validar corretamente as propriedades do jogo")]
        [InlineData(1, 1, 2, 2020, true, "jogo válido")]
        [InlineData(1, 1, 1, 2020, false, "times iguais")]
        [InlineData(0, 1, 2, 2020, false, "ID externo inválido")]
        [InlineData(1, 0, 2, 2020, false, "ID time casa inválido")]
        [InlineData(1, 1, 0, 2020, false, "ID time visitante inválido")]
        [InlineData(1, 1, 2, 1999, false, "temporada inválida")]
        public void DeveValidarCorretamente_PropriedadesDoJogo(
            int idExterno, int idCasa, int idVisitante, int temporada, bool esperadoValido, string cenario)
        {
            // Arrange
            var jogo = TestDataBuilder.CreateGame();
            jogo.ExternalId = idExterno;
            jogo.HomeTeamId = idCasa;
            jogo.VisitorTeamId = idVisitante;
            jogo.Season = temporada;

            // Act
            var ehValido = jogo.IsValid();

            // Assert
            ehValido.Should().Be(esperadoValido, cenario);
        }

        #endregion

        #region Testes da Entidade Team

        [Fact(DisplayName = "DisplayName deve retornar cidade e nome formatados")]
        public void DeveRetornarCidadeENome_Formatados()
        {
            // Arrange
            var time = TestDataBuilder.CreateTeam();
            time.City = "Los Angeles";
            time.Name = "Lakers";

            // Act
            var nomeExibicao = time.DisplayName;

            // Assert
            nomeExibicao.Should().Be("Los Angeles Lakers");
        }

        [Theory(DisplayName = "ConferenceDisplay deve converter enum para string")]
        [InlineData(Conference.East, "East")]
        [InlineData(Conference.West, "West")]
        public void DeveConverterConferenceParaString(Conference conferencia, string esperado)
        {
            // Arrange
            var time = TestDataBuilder.CreateTeam();
            time.Conference = conferencia;

            // Act
            var conferenciaExibicao = time.ConferenceDisplay;

            // Assert
            conferenciaExibicao.Should().Be(esperado);
        }

        [Theory(DisplayName = "IsValid deve validar corretamente propriedades do time")]
        [InlineData("Lakers", "LA", "LAL", 1, true, "time válido")]
        [InlineData("", "LA", "LAL", 1, false, "nome vazio")]
        [InlineData("Lakers", "", "LAL", 1, false, "cidade vazia")]
        [InlineData("Lakers", "LA", "", 1, false, "abreviação vazia")]
        [InlineData("Lakers", "LA", "LAL", 0, false, "ID externo inválido")]
        public void DeveValidarCorretamente_PropriedadesDoTime(
            string nome, string cidade, string abreviacao, int idExterno, bool esperadoValido, string cenario)
        {
            // Arrange
            var time = TestDataBuilder.CreateTeam();
            time.Name = nome;
            time.City = cidade;
            time.Abbreviation = abreviacao;
            time.ExternalId = idExterno;

            // Act
            var ehValido = time.IsValid();

            // Assert
            ehValido.Should().Be(esperadoValido, cenario);
        }

        [Fact(DisplayName = "ToString deve retornar DisplayName")]
        public void ToString_DeveRetornarDisplayName()
        {
            // Arrange
            var time = TestDataBuilder.CreateTeam();
            time.City = "Golden State";
            time.Name = "Warriors";

            // Act
            var resultado = time.ToString();

            // Assert
            resultado.Should().Be("Golden State Warriors");
        }

        #endregion

        #region Testes da Entidade Player

        [Fact(DisplayName = "FullName deve retornar nome completo formatado")]
        public void DeveRetornarNomeCompleto_Formatado()
        {
            // Arrange
            var jogador = TestDataBuilder.CreatePlayer();
            jogador.FirstName = "LeBron";
            jogador.LastName = "James";

            // Act
            var nomeCompleto = jogador.FullName;

            // Assert
            nomeCompleto.Should().Be("LeBron James");
        }

        [Fact(DisplayName = "FullName deve remover espaços extras do início e fim")]
        public void DeveRemoverEspacosExtras_DoInicioEFim()
        {
            // Arrange
            var jogador = TestDataBuilder.CreatePlayer();
            jogador.FirstName = "LeBron";
            jogador.LastName = "James";

            // Act
            var nomeCompleto = jogador.FullName;

            // Assert
            nomeCompleto.Should().Be("LeBron James");
        }

        [Fact(DisplayName = "FullName deve funcionar com nomes compostos")]
        public void DeveFuncionarComNomesCompostos()
        {
            // Arrange
            var jogador = TestDataBuilder.CreatePlayer();
            jogador.FirstName = "Mary Jane";
            jogador.LastName = "Smith-Jones";

            // Act
            var nomeCompleto = jogador.FullName;

            // Assert
            nomeCompleto.Should().Be("Mary Jane Smith-Jones");
        }

        [Theory(DisplayName = "DisplayName deve incluir posição corretamente")]
        [InlineData(PlayerPosition.PG, "LeBron James (PG)")]
        [InlineData(PlayerPosition.SG, "LeBron James (SG)")]
        [InlineData(PlayerPosition.SF, "LeBron James (SF)")]
        [InlineData(PlayerPosition.PF, "LeBron James (PF)")]
        [InlineData(PlayerPosition.C, "LeBron James (C)")]
        [InlineData(null, "LeBron James (N/A)")]
        public void DeveIncluirPosicao_NoDisplayName(PlayerPosition? posicao, string esperado)
        {
            // Arrange
            var jogador = TestDataBuilder.CreatePlayer();
            jogador.FirstName = "LeBron";
            jogador.LastName = "James";
            jogador.Position = posicao;

            // Act
            var nomeExibicao = jogador.DisplayName;

            // Assert
            nomeExibicao.Should().Be(esperado);
        }

        [Theory(DisplayName = "Height deve formatar altura corretamente")]
        [InlineData(6, 8, "6'8\"")]
        [InlineData(7, 0, "7'0\"")]
        [InlineData(5, 11, "5'11\"")]
        [InlineData(null, null, "N/A")]
        [InlineData(6, null, "N/A")]
        [InlineData(null, 8, "N/A")]
        public void DeveFormatarAltura_Corretamente(int? pes, int? polegadas, string esperado)
        {
            // Arrange
            var jogador = TestDataBuilder.CreatePlayer();
            jogador.HeightFeet = pes;
            jogador.HeightInches = polegadas;

            // Act
            var altura = jogador.Height;

            // Assert
            altura.Should().Be(esperado);
        }

        [Theory(DisplayName = "Weight deve formatar peso corretamente")]
        [InlineData(250, "250 lbs")]
        [InlineData(180, "180 lbs")]
        [InlineData(null, "N/A")]
        public void DeveFormatarPeso_Corretamente(int? libras, string esperado)
        {
            // Arrange
            var jogador = TestDataBuilder.CreatePlayer();
            jogador.WeightPounds = libras;

            // Act
            var peso = jogador.Weight;

            // Assert
            peso.Should().Be(esperado);
        }

        [Theory(DisplayName = "HeightInInches deve calcular altura total em polegadas")]
        [InlineData(6, 8, 80)]   // 6*12 + 8 = 80
        [InlineData(7, 0, 84)]   // 7*12 + 0 = 84
        [InlineData(5, 11, 71)]  // 5*12 + 11 = 71
        [InlineData(null, 8, null)]
        [InlineData(6, null, null)]
        [InlineData(null, null, null)]
        public void DeveCalcularAlturaEmPolegadas_Corretamente(int? pes, int? polegadas, int? esperado)
        {
            // Arrange
            var jogador = TestDataBuilder.CreatePlayer();
            jogador.HeightFeet = pes;
            jogador.HeightInches = polegadas;

            // Act
            var alturaTotal = jogador.HeightInInches;

            // Assert
            alturaTotal.Should().Be(esperado);
        }

        [Theory(DisplayName = "IsValid deve validar corretamente propriedades do jogador")]
        [InlineData("LeBron", "James", 1, true, "jogador válido")]
        [InlineData("", "James", 1, false, "primeiro nome vazio")]
        [InlineData("LeBron", "", 1, false, "último nome vazio")]
        [InlineData("LeBron", "James", 0, false, "ID externo inválido")]
        [InlineData("   ", "James", 1, false, "primeiro nome apenas espaços")]
        [InlineData("LeBron", "   ", 1, false, "último nome apenas espaços")]
        public void DeveValidarCorretamente_PropriedadesDoJogador(
            string primeiroNome, string ultimoNome, int idExterno, bool esperadoValido, string cenario)
        {
            // Arrange
            var jogador = TestDataBuilder.CreatePlayer();
            jogador.FirstName = primeiroNome;
            jogador.LastName = ultimoNome;
            jogador.ExternalId = idExterno;

            // Act
            var ehValido = jogador.IsValid();

            // Assert
            ehValido.Should().Be(esperadoValido, cenario);
        }

        [Fact(DisplayName = "ToString deve retornar nome completo")]
        public void ToString_DeveRetornarNomeCompleto()
        {
            // Arrange
            var jogador = TestDataBuilder.CreatePlayer();
            jogador.FirstName = "Stephen";
            jogador.LastName = "Curry";

            // Act
            var resultado = jogador.ToString();

            // Assert
            resultado.Should().Be("Stephen Curry");
        }

        #endregion

        #region Testes de Cenários Complexos

        [Fact(DisplayName = "Game deve calcular corretamente todas as propriedades relacionadas")]
        public void Game_DeveCalcularTodasPropriedades_Corretamente()
        {
            // Arrange
            var timeCasa = TestDataBuilder.CreateTeam(1, "Lakers");
            timeCasa.Abbreviation = "LAL";
            var timeVisitante = TestDataBuilder.CreateTeam(2, "Warriors");
            timeVisitante.Abbreviation = "GSW";

            var jogo = TestDataBuilder.CreateGame();
            jogo.HomeTeam = timeCasa;
            jogo.VisitorTeam = timeVisitante;
            jogo.HomeTeamScore = 120;
            jogo.VisitorTeamScore = 118;
            jogo.Status = GameStatus.Final;
            jogo.Date = DateTime.Today;

            // Act & Assert
            using (new FluentAssertions.Execution.AssertionScope())
            {
                jogo.Score.Should().Be("120 - 118");
                jogo.GameTitle.Should().Be("GSW @ LAL");
                jogo.WinningTeam.Should().Be(timeCasa);
                jogo.IsToday.Should().BeTrue();
                jogo.IsCompleted.Should().BeTrue();
                jogo.IsLive.Should().BeFalse();
                jogo.IsValid().Should().BeTrue();
            }
        }

        [Fact(DisplayName = "Player deve calcular todas as propriedades de exibição")]
        public void Player_DeveCalcularTodasPropriedadesExibicao()
        {
            // Arrange
            var jogador = TestDataBuilder.CreatePlayer();
            jogador.FirstName = "Giannis";
            jogador.LastName = "Antetokounmpo";
            jogador.Position = PlayerPosition.PF;
            jogador.HeightFeet = 6;
            jogador.HeightInches = 11;
            jogador.WeightPounds = 242;

            // Act & Assert
            using (new FluentAssertions.Execution.AssertionScope())
            {
                jogador.FullName.Should().Be("Giannis Antetokounmpo");
                jogador.DisplayName.Should().Be("Giannis Antetokounmpo (PF)");
                jogador.Height.Should().Be("6'11\"");
                jogador.Weight.Should().Be("242 lbs");
                jogador.HeightInInches.Should().Be(83);
                jogador.IsValid().Should().BeTrue();
            }
        }

        #endregion

        #region Classes Auxiliares

        private class EntidadeTeste : BaseEntity
        {
            // Classe vazia para testar funcionalidade de BaseEntity
        }

        #endregion
    }
}