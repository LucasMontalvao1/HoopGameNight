import { Component, OnInit, OnDestroy, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { PlayersService } from '../../../core/services/players.service';
import { PlayerStatsApiService, PlayerSeasonStats } from '../../../core/services/player-stats-api.service';

@Component({
  selector: 'app-player-details',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './player-details.html',
  styleUrl: './player-details.scss'
})
export class PlayerDetailsComponent implements OnInit, OnDestroy {
  // Signals para estatísticas
  private readonly _selectedSeason = signal<number>(2025);
  private readonly _seasonStats = signal<PlayerSeasonStats | null>(null);
  private readonly _availableSeasons = signal<number[]>([]);
  private readonly _loadingStats = signal<boolean>(false);
  private readonly _currentPlayerId = signal<number | null>(null);

  // Getters públicos
  readonly selectedSeason = this._selectedSeason.asReadonly();
  readonly seasonStats = this._seasonStats.asReadonly();
  readonly availableSeasons = this._availableSeasons.asReadonly();
  readonly loadingStats = this._loadingStats.asReadonly();

  constructor(
    public readonly playersService: PlayersService,
    private readonly playerStatsService: PlayerStatsApiService,
    private readonly route: ActivatedRoute,
    private readonly router: Router
  ) {}

  async ngOnInit(): Promise<void> {
    this.route.params.subscribe(async params => {
      const playerId = params['id'];
      if (playerId) {
        this._currentPlayerId.set(+playerId);
        await this.playersService.loadPlayerById(+playerId);
        await this.loadPlayerStats(+playerId);
      }
    });
  }

  ngOnDestroy(): void {
    this.playersService.clearSelectedPlayer();
    this._seasonStats.set(null);
    this._availableSeasons.set([]);
  }

  goBack(): void {
    this.router.navigate(['/players']);
  }

  viewTeam(abbreviation: string): void {
    this.router.navigate(['/teams', abbreviation]);
  }

  getPlayerPhotoUrl(): string {
    const player = this.playersService.selectedPlayer();
    if (!player) return '';
    return this.playersService.getPlayerPhotoUrl(player.espnId || player.externalId);
  }

  getTeamLogoUrl(): string {
    const player = this.playersService.selectedPlayer();
    if (!player) return '';
    return this.playersService.getTeamLogoUrl(player.team.abbreviation);
  }

  onImageError(event: Event): void {
    const img = event.target as HTMLImageElement;
    img.src = 'assets/player-placeholder.png';
  }

  onTeamLogoError(event: Event): void {
    const img = event.target as HTMLImageElement;
    img.style.display = 'none';
  }

  // ===== MÉTODOS DE ESTATÍSTICAS =====

  private async loadPlayerStats(playerId: number): Promise<void> {
    try {
      console.log(`🏀 PLAYER DETAILS: Iniciando carregamento de stats para jogador ${playerId}`);
      this._loadingStats.set(true);

      // Buscar todas as temporadas disponíveis
      console.log(`→ Chamando playerStatsService.getPlayerAllSeasons(${playerId})`);
      const allSeasons = await this.playerStatsService.getPlayerAllSeasons(playerId);

      console.log(`✅ Recebido ${allSeasons?.length || 0} temporadas:`, allSeasons);

      if (allSeasons && allSeasons.length > 0) {
        // Extrair anos das temporadas e ordenar (mais recente primeiro)
        const seasons = allSeasons
          .map(s => s.season)
          .filter((v, i, a) => a.indexOf(v) === i) // remover duplicatas
          .sort((a, b) => b - a);

        console.log(`📅 Temporadas extraídas e ordenadas:`, seasons);
        this._availableSeasons.set(seasons);

        // Carregar temporada mais recente
        const latestSeason = seasons[0];
        console.log(`→ Carregando temporada mais recente: ${latestSeason}`);
        this._selectedSeason.set(latestSeason);
        await this.loadSeasonStats(latestSeason);
      } else {
        console.warn('⚠️ Nenhuma estatística disponível para este jogador');
      }
    } catch (error) {
      console.error('❌ Erro ao carregar estatísticas:', error);
    } finally {
      this._loadingStats.set(false);
      console.log(`✓ Loading stats finalizado`);
    }
  }

  async selectSeason(season: number): Promise<void> {
    console.log(`📅 Selecionando temporada: ${season}`);
    this._selectedSeason.set(season);
    await this.loadSeasonStats(season);
  }

  private async loadSeasonStats(season: number): Promise<void> {
    const playerId = this._currentPlayerId();
    if (!playerId) {
      console.warn('⚠️ Player ID não definido');
      return;
    }

    try {
      console.log(`🏀 PLAYER DETAILS: Carregando stats para temporada ${season}`);
      this._loadingStats.set(true);
      const stats = await this.playerStatsService.getPlayerSeasonStats(playerId, season);

      console.log(`✅ Stats recebidas para temporada ${season}:`, stats);
      
      if (stats) {
        console.log(`   PPG: ${stats.avgPoints}, RPG: ${stats.avgRebounds}, APG: ${stats.avgAssists}`);
        console.log(`   FG%: ${stats.fieldGoalPercentage}, 3P%: ${stats.threePointPercentage}, FT%: ${stats.freeThrowPercentage}`);
        console.log(`   GP: ${stats.gamesPlayed}, GS: ${stats.gamesStarted}`);
      } else {
        console.warn(`⚠️ Stats NULAS para temporada ${season}`);
      }

      this._seasonStats.set(stats);
    } catch (error) {
      console.error(`❌ Erro ao carregar stats da temporada ${season}:`, error);
      this._seasonStats.set(null);
    } finally {
      this._loadingStats.set(false);
    }
  }

  // ⚠️ FORMATAÇÃO DE PORCENTAGENS
  formatPercentage(value: number | null | undefined): string {
    console.log('🔍 formatPercentage chamado com:', value, typeof value);
    
    if (value === null || value === undefined) {
      console.log('→ Retornando "-" (valor null/undefined)');
      return '-';
    }
    
    // Converter para número se for string
    const numValue = typeof value === 'string' ? parseFloat(value) : value;
    
    if (isNaN(numValue)) {
      console.log('→ Retornando "-" (NaN)');
      return '-';
    }
    
    // Se o valor já estiver em percentual (> 1), não multiplicar
    // Caso contrário, multiplicar por 100
    const percentage = numValue > 1 ? numValue : numValue * 100;
    const formatted = percentage.toFixed(1) + '%';
    
    console.log(`→ Retornando: ${formatted} (valor original: ${value}, calculado: ${percentage})`);
    return formatted;
  }

  formatDecimal(value: number | null | undefined): string {
    if (value === null || value === undefined) return '-';
    
    const numValue = typeof value === 'string' ? parseFloat(value) : value;
    
    if (isNaN(numValue)) return '-';
    
    return numValue.toFixed(1);
  }
}