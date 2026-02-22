import { Component, OnInit, OnDestroy, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { PlayersService } from '../../../core/services/players.service';
import { PlayerStatsApiService, PlayerSeasonStats } from '../../../core/services/player-stats-api.service';
import { StatsService } from '../../../core/services/stats.service';

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

  // Cache de todos os dados de temporada do jogador
  private _allStatsData: PlayerSeasonStats[] = [];

  // Getters públicos
  readonly selectedSeason = this._selectedSeason.asReadonly();
  readonly seasonStats = this._seasonStats.asReadonly();
  readonly availableSeasons = this._availableSeasons.asReadonly();
  readonly loadingStats = this._loadingStats.asReadonly();

  constructor(
    public readonly playersService: PlayersService,
    private readonly playerStatsService: PlayerStatsApiService,
    protected readonly statsService: StatsService,
    private readonly route: ActivatedRoute,
    public readonly router: Router
  ) { }

  protected readonly activeTab = signal<'stats' | 'gamelog'>('stats');

  onSeasonChange(event: Event): void {
    const select = event.target as HTMLSelectElement;
    if (select) {
      this.selectSeason(+select.value);
    }
  }

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
    this._allStatsData = [];
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

      // Buscar todas as temporadas disponíveis via /career (ESPN Data - Mais completo)
      console.log(`→ Chamando playerStatsService.getPlayerAllSeasons(${playerId})`);
      const allSeasons = await this.playerStatsService.getPlayerAllSeasons(playerId);

      this._allStatsData = allSeasons || [];
      console.log(`✅ Recebido ${this._allStatsData.length} registros de temporada:`, this._allStatsData);

      if (this._allStatsData.length > 0) {
        // Extrair anos únicos das temporadas e ordenar (mais recente primeiro)
        const seasons = this._allStatsData
          .map(s => s.season)
          .filter((v, i, a) => a.indexOf(v) === i)
          .sort((a, b) => b - a);

        console.log(`📅 Temporadas únicas:`, seasons);
        this._availableSeasons.set(seasons);

        // Selecionar a temporada mais recente
        const latestSeason = seasons[0];
        console.log(`→ Selecionando temporada inicial: ${latestSeason}`);
        await this.selectSeason(latestSeason);
      } else {
        console.warn('⚠️ Nenhuma estatística disponível via API de carreira');
      }
    } catch (error) {
      console.error('❌ Erro ao carregar estatísticas:', error);
    } finally {
      this._loadingStats.set(false);
    }
  }

  async selectSeason(season: number): Promise<void> {
    console.log(`📅 Selecionando temporada: ${season}`);
    this._selectedSeason.set(season);

    // Prioridade 1: Procurar nos dados de carreira (ESPN) que já carregamos
    // Preferimos a Regular Season (isPlayoffs === false)
    const statsFromCareer = this._allStatsData.find(s => s.season === season && s.isPlayoffs === false)
      || this._allStatsData.find(s => s.season === season);

    if (statsFromCareer) {
      console.log('✅ Usando stats do cache de carreira (ESPN):', statsFromCareer);
      this._seasonStats.set(statsFromCareer);
    } else {
      // Prioridade 2: Buscar do nosso banco de dados (Calculado via View)
      console.log(`🔍 Stats não encontradas no cache. Buscando via API para season ${season}`);
      await this.loadSeasonStatsFromDb(season);
    }
  }

  async setTab(tab: 'stats' | 'gamelog'): Promise<void> {
    this.activeTab.set(tab);
    if (tab === 'gamelog') {
      await this.loadRecentGamelog();
    }
  }

  private async loadRecentGamelog(): Promise<void> {
    const playerId = this._currentPlayerId();
    if (playerId) {
      await this.statsService.loadRecentPlayerGames(playerId);
    }
  }

  /**
   * Busca estatísticas agregadas da nossa VIEW local no banco de dados.
   * Usado como fallback ou quando queremos dados locais granulares.
   */
  private async loadSeasonStatsFromDb(season: number): Promise<void> {
    const playerId = this._currentPlayerId();
    if (!playerId) return;

    try {
      this._loadingStats.set(true);
      const stats = await this.playerStatsService.getPlayerSeasonStats(playerId, season);
      console.log(`✅ Stats carregadas via DB View para season ${season}:`, stats);
      this._seasonStats.set(stats);
    } catch (error) {
      console.error(`❌ Erro ao carregar stats da season ${season} via DB:`, error);
      this._seasonStats.set(null);
    } finally {
      this._loadingStats.set(false);
    }
  }

  // ⚠️ FORMATAÇÃO DE PORCENTAGENS
  formatPercentage(value: number | null | undefined): string {
    if (value === null || value === undefined) return '-';

    const numValue = typeof value === 'string' ? parseFloat(value) : value;
    if (isNaN(numValue)) return '-';

    // O backend envia percentuais tanto como 0.55 quanto 55.0
    // Regra: se for < 1 (e não for zero puro), multiplicamos por 100
    let percentage = numValue;
    if (numValue > 0 && numValue < 1) {
      percentage = numValue * 100;
    }

    return percentage.toFixed(1) + '%';
  }

  formatDecimal(value: number | null | undefined): string {
    if (value === null || value === undefined) return '-';
    const numValue = typeof value === 'string' ? parseFloat(value) : value;
    if (isNaN(numValue)) return '-';
    return numValue.toFixed(1);
  }
}