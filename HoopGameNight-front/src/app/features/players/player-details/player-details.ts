import { Component, OnInit, OnDestroy, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';
import { PlayersService } from '../../../core/services/players.service';
import { PlayerStatsApiService, PlayerSeasonStats } from '../../../core/services/player-stats-api.service';
import { StatsService } from '../../../core/services/stats.service';
import { SEOService } from '../../../core/services/seo.service';
import { PreferencesStore } from '../../../core/services/preferences.store';
import { PlayerPerformanceChartComponent } from '../player-performance-chart/player-performance-chart.component';

@Component({
  selector: 'app-player-details',
  standalone: true,
  imports: [CommonModule, RouterModule, PlayerPerformanceChartComponent],
  templateUrl: './player-details.html',
  styleUrl: './player-details.scss'
})
export class PlayerDetailsComponent implements OnInit, OnDestroy {
  private readonly _selectedSeason = signal<number>(2025);
  private readonly _seasonStats = signal<PlayerSeasonStats | null>(null);
  private readonly _availableSeasons = signal<number[]>([]);
  private readonly _loadingStats = signal<boolean>(false);
  private readonly _currentPlayerId = signal<number | null>(null);

  private _allStatsData: PlayerSeasonStats[] = [];

  get allCareerStats(): PlayerSeasonStats[] {
    return this._allStatsData.sort((a, b) => b.season - a.season);
  }

  readonly selectedSeason = this._selectedSeason.asReadonly();
  readonly seasonStats = this._seasonStats.asReadonly();
  readonly availableSeasons = this._availableSeasons.asReadonly();
  readonly loadingStats = this._loadingStats.asReadonly();

  constructor(
    public readonly playersService: PlayersService,
    private readonly playerStatsService: PlayerStatsApiService,
    protected readonly statsService: StatsService,
    private readonly seoService: SEOService,
    private readonly route: ActivatedRoute,
    public readonly router: Router,
    public readonly preferencesStore: PreferencesStore
  ) { }

  protected readonly activeTab = signal<'stats' | 'gamelog' | 'career'>('stats');

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

        const player = this.playersService.selectedPlayer();
        if (player) {
          this._currentPlayerId.set(player.id);
          this.updateSEO(player);
          await this.loadPlayerStats(player.id);
          await this.loadRecentGamelog(player.id);
        }
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
    return this.playersService.getTeamLogoUrl(player.team?.abbreviation || '');
  }

  onImageError(event: Event): void {
    const img = event.target as HTMLImageElement;
    img.src = 'assets/player-placeholder.png';
  }

  onTeamLogoError(event: Event): void {
    const img = event.target as HTMLImageElement;
    img.style.display = 'none';
  }

  private readonly TEAM_COLORS: Record<string, { primary: string; secondary: string }> = {
    'BOS': { primary: '#007A33', secondary: '#BA9653' },
    'MIA': { primary: '#98002E', secondary: '#F9A01B' },
    'CHI': { primary: '#CE1141', secondary: '#000000' },
    'NY': { primary: '#006BB6', secondary: '#F58426' },
    'GS': { primary: '#1D428A', secondary: '#FFC72C' },
    'LAL': { primary: '#552583', secondary: '#FDB927' },
    'MIL': { primary: '#00471B', secondary: '#EEE1C6' },
    'PHI': { primary: '#006BB6', secondary: '#ED174C' },
    'BKN': { primary: '#000000', secondary: '#FFFFFF' },
    'DAL': { primary: '#00538C', secondary: '#B8C4CA' },
    'DEN': { primary: '#0E2240', secondary: '#FEC524' },
    'PHX': { primary: '#1D1160', secondary: '#E56020' },
    'MEM': { primary: '#5D76A9', secondary: '#12173F' },
    'SAC': { primary: '#5A2D81', secondary: '#63727A' },
    'OKC': { primary: '#007AC1', secondary: '#EF3B24' },
    'MIN': { primary: '#0C2340', secondary: '#236192' },
    'HOU': { primary: '#CE1141', secondary: '#000000' },
    'LAC': { primary: '#C4CED4', secondary: '#1D428A' },
    'POR': { primary: '#E03A3E', secondary: '#000000' },
    'NO': { primary: '#0C2340', secondary: '#C8102E' },
    'SA': { primary: '#C4CED4', secondary: '#000000' },
    'UTA': { primary: '#002B5C', secondary: '#00471B' },
    'TOR': { primary: '#CE1141', secondary: '#000000' },
    'CLE': { primary: '#860038', secondary: '#041E42' },
    'IND': { primary: '#002D62', secondary: '#FDBB30' },
    'ATL': { primary: '#E03A3E', secondary: '#C1D32F' },
    'ORL': { primary: '#0077C0', secondary: '#C4CED4' },
    'WAS': { primary: '#002B5C', secondary: '#E31837' },
    'CHA': { primary: '#1D1160', secondary: '#00788C' },
    'DET': { primary: '#C8102E', secondary: '#1D428A' }
  };

  getTeamColor(abbreviation: string | undefined, type: 'primary' | 'secondary'): string {
    if (!abbreviation) return type === 'primary' ? '#202020' : '#101010';
    const cleanAbbr = abbreviation.replace(/[^A-Za-z]/g, '').toUpperCase();
    return this.TEAM_COLORS[cleanAbbr]?.[type] || (type === 'primary' ? '#202020' : '#101010');
  }

  private async loadPlayerStats(playerId: number): Promise<void> {
    try {
      this._loadingStats.set(true);

      const allSeasons = await this.playerStatsService.getPlayerAllSeasons(playerId);

      this._allStatsData = allSeasons || [];

      if (this._allStatsData.length > 0) {
        const seasons = this._allStatsData
          .map(s => s.season)
          .filter((v, i, a) => a.indexOf(v) === i)
          .sort((a, b) => b - a);

        this._availableSeasons.set(seasons);

        const latestSeason = seasons[0];
        await this.selectSeason(latestSeason);
      }
    } catch (error) {
      console.error('Erro ao carregar estatísticas:', error);
    } finally {
      this._loadingStats.set(false);
    }
  }

  async selectSeason(season: number): Promise<void> {
    this._selectedSeason.set(season);

    const statsFromCareer = this._allStatsData.find(s => s.season === season && s.isPlayoffs === false)
      || this._allStatsData.find(s => s.season === season);

    if (statsFromCareer) {
      this._seasonStats.set(statsFromCareer);
    } else {
      await this.loadSeasonStatsFromDb(season);
    }
  }

  async setTab(tab: 'stats' | 'gamelog' | 'career'): Promise<void> {
    this.activeTab.set(tab);
    if (tab === 'gamelog') {
      await this.loadRecentGamelog();
    }
  }

  private async loadRecentGamelog(idOverride?: number): Promise<void> {
    const playerId = idOverride || this._currentPlayerId();
    if (playerId) {
      await this.statsService.loadRecentPlayerGames(playerId);
    }
  }

  private async loadSeasonStatsFromDb(season: number): Promise<void> {
    const playerId = this._currentPlayerId();
    if (!playerId) return;

    try {
      this._loadingStats.set(true);
      const stats = await this.playerStatsService.getPlayerSeasonStats(playerId, season);
      console.log(`Stats carregadas via DB View para season ${season}:`, stats);
      this._seasonStats.set(stats);
    } catch (error) {
      console.error(`Erro ao carregar stats da season ${season} via DB:`, error);
      this._seasonStats.set(null);
    } finally {
      this._loadingStats.set(false);
    }
  }

  formatPercentage(value: number | null | undefined): string {
    if (value === null || value === undefined) return '-';

    const numValue = typeof value === 'string' ? parseFloat(value) : value;
    if (isNaN(numValue)) return '-';

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

  formatHeight(heightStr: string | undefined): string {
    if (!heightStr) return '-';
    const match = heightStr.match(/(\d+)'(\d+(?:\.\d+)?)"?/);
    if (match) {
      const feet = parseInt(match[1], 10);
      const inches = parseFloat(match[2]);
      const totalInches = (feet * 12) + inches;
      const meters = totalInches * 0.0254;
      return `${meters.toFixed(2).replace('.', ',')}m (${heightStr})`;
    }
    return heightStr;
  }

  formatWeight(weightStr: string | undefined): string {
    if (!weightStr) return '-';
    const match = weightStr.match(/(\d+(?:\.\d+)?)/);
    if (match) {
      const lbs = parseFloat(match[1]);
      const kg = lbs * 0.453592;
      return `${Math.round(kg)}kg (${weightStr})`;
    }
    return weightStr;
  }

  isFavorite(): boolean {
    const player = this.playersService.selectedPlayer();
    return player ? this.preferencesStore.isFavoritePlayer(player.id) : false;
  }

  toggleFavorite(): void {
    const player = this.playersService.selectedPlayer();
    if (player) {
      this.preferencesStore.toggleFavoritePlayer(player.id);
    }
  }

  private updateSEO(player: any): void {
    const title = `${player.firstName} ${player.lastName}`;
    const description = `Estatísticas completas, histórico de jogos e desempenho de ${title} (${player.team?.abbreviation || 'NBA'}). Confira médias de pontos, rebotes e assistências.`;
    const image = this.playersService.getPlayerPhotoUrl(player.espnId || player.externalId);

    this.seoService.updateTitle(title);
    this.seoService.updateMeta(description, undefined, image);
  }
}
