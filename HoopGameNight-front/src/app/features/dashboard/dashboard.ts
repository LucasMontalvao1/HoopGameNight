import { Component, OnInit, OnDestroy, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { Subject } from 'rxjs';
import { debounceTime, distinctUntilChanged, takeUntil } from 'rxjs/operators';

import { HealthCheck } from '../../core/services/health-check';
import { GamesService } from '../../core/services/games.service';
import { TeamsService } from '../../core/services/teams.service';
import { PlayersService } from '../../core/services/players.service';
import { StatusIndicator } from '../../shared/components/status-indicator/status-indicator';
import { ApiStatus, PlayerResponse } from '../../core/interfaces/api.interface';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule, RouterModule, StatusIndicator, FormsModule],
  templateUrl: './dashboard.html',
  styleUrls: ['./dashboard.scss']
})
export class Dashboard implements OnInit {
  readonly ApiStatusEnum = ApiStatus;
  protected selectedDateOption: string = 'today';

  // Player search
  protected playerSearchQuery = signal<string>('');
  protected playerSearchResults = signal<PlayerResponse[]>([]);
  protected isSearchingPlayers = signal<boolean>(false);
  protected showPlayerResults = signal<boolean>(false);

  protected readonly myTeams = [
    { code: 'LAL', name: 'Lakers', city: 'Los Angeles', nextGame: 'vs Warriors hoje', color: '#552583' },
    { code: 'GSW', name: 'Warriors', city: 'Golden State', nextGame: 'vs Lakers hoje', color: '#1D428A' }
  ];

  protected readonly aiInsights = [
    { 
      type: 'highlight', 
      title: 'Destaque do Dia', 
      description: 'LeBron com 28 pts de média nos últimos 5 jogos' 
    },
    { 
      type: 'statistic', 
      title: 'Estatística', 
      description: 'Celtics: 87% de aproveitamento em casa' 
    }
  ];

  constructor(
    protected readonly healthCheck: HealthCheck,
    protected readonly gamesService: GamesService,
    protected readonly teamsService: TeamsService,
    protected readonly playersService: PlayersService,
    private readonly router: Router
  ) {}

  async ngOnInit(): Promise<void> {
    this.healthCheck.checkHealth();
    
    try {
      await this.gamesService.loadTodayGames();
      console.log('Dashboard: Jogos de hoje carregados');
    } catch (error) {
      console.error('Dashboard: Erro ao carregar jogos', error);
    }
  }

  getTodayGamesCount(): number {
    return this.getCurrentGames().length;
  }

  getLiveGamesCount(): number {
    return this.gamesService.liveGames().length;
  }

  hasLiveGames(): boolean {
    return this.gamesService.hasLiveGames();
  }

  getNextGame(): string {
    const games = this.gamesService.scheduledGames();
    if (games.length === 0) return 'Nenhum jogo agendado';
    
    const nextGame = games[0];
    const time = this.formatGameTime(nextGame.dateTime);
    return `${nextGame.gameTitle} às ${time}`;
  }

  formatGameTime(dateTime: string): string {
    return new Date(dateTime).toLocaleTimeString('pt-BR', {
      hour: '2-digit',
      minute: '2-digit'
    });
  }

  formatGameDate(dateTime: string): string {
    return new Date(dateTime).toLocaleDateString('pt-BR');
  }

  formatFullDateTime(dateTime: string): string {
    return new Date(dateTime).toLocaleString('pt-BR', {
      day: '2-digit',
      month: '2-digit',
      hour: '2-digit',
      minute: '2-digit'
    });
  }

  trackByGameId(index: number, game: any): number {
    return game.id;
  }

  getTeamLogoUrl(abbreviation: string): string {
    return this.teamsService.getTeamLogoUrl(abbreviation);
  }

  handleLogoError(event: Event, abbreviation: string): void {
    const img = event.target as HTMLImageElement;
    if (img.src.includes('fallback')) return;
    console.warn(`Logo não encontrado para: ${abbreviation}`);
    img.src = 'data:image/svg+xml;base64,PHN2ZyB4bWxucz0iaHR0cDovL3d3dy53My5vcmcvMjAwMC9zdmciIHZpZXdCb3g9IjAgMCAxMDAgMTAwIj48cmVjdCB3aWR0aD0iMTAwIiBoZWlnaHQ9IjEwMCIgZmlsbD0iI2YzZjRmNiIvPjx0ZXh0IHg9IjUwIiB5PSI1MCIgdGV4dC1hbmNob3I9Im1pZGRsZSIgZG9taW5hbnQtYmFzZWxpbmU9Im1pZGRsZSIgZm9udC1mYW1pbHk9IkFyaWFsIiBmb250LXNpemU9IjE0IiBmaWxsPSIjOWNhM2FmIj5OQkE8L3RleHQ+PC9zdmc+';
  }

  async onDateChange(event: Event): Promise<void> {
    const select = event.target as HTMLSelectElement;
    const value = select.value;
    this.selectedDateOption = value;

    const today = new Date();
    let targetDate: Date;

    switch (value) {
      case 'yesterday':
        targetDate = new Date(today);
        targetDate.setDate(today.getDate() - 1);
        break;
      case 'tomorrow':
        targetDate = new Date(today);
        targetDate.setDate(today.getDate() + 1);
        break;
      case 'today':
      default:
        targetDate = today;
        break;
    }

    console.log(`Dashboard: Carregando jogos para ${targetDate.toLocaleDateString('pt-BR')}`);

    if (value === 'today') {
      await this.gamesService.loadTodayGames(true);
    } else {
      await this.gamesService.loadGamesByDate(targetDate, true);
    }
  }

  navigateToTeam(abbreviation: string): void {
    if (abbreviation) {
      console.log(`Navegando para detalhes do time: ${abbreviation}`);
      this.router.navigate(['/teams', abbreviation]);
    }
  }

  getCurrentGames() {
    return this.selectedDateOption === 'today'
      ? this.gamesService.todayGames()
      : this.gamesService.currentGames();
  }

  // Player search methods
  updatePlayerSearch(value: string): void {
    this.playerSearchQuery.set(value);
    if (value.length === 0) {
      this.playerSearchResults.set([]);
      this.showPlayerResults.set(false);
    }
  }

  async searchPlayers(): Promise<void> {
    const query = this.playerSearchQuery();
    if (query.trim().length < 2) {
      return;
    }

    this.isSearchingPlayers.set(true);
    this.showPlayerResults.set(true);

    try {
      await this.playersService.searchPlayers(query, 1, 5);
      this.playerSearchResults.set(this.playersService.searchResults());
    } catch (error) {
      console.error('Erro ao buscar jogadores:', error);
      this.playerSearchResults.set([]);
    } finally {
      this.isSearchingPlayers.set(false);
    }
  }

  selectPlayer(player: PlayerResponse): void {
    this.showPlayerResults.set(false);
    this.playerSearchQuery.set('');
    this.router.navigate(['/players', player.id]);
  }

  getPlayerPhotoUrl(player: PlayerResponse): string {
    return this.playersService.getPlayerPhotoUrl(player.espnId || player.externalId);
  }

  onPlayerImageError(event: Event): void {
    const img = event.target as HTMLImageElement;
    if (!img.dataset['fallback']) {
      img.dataset['fallback'] = 'true';
      img.src = 'data:image/svg+xml;base64,PHN2ZyB4bWxucz0iaHR0cDovL3d3dy53My5vcmcvMjAwMC9zdmciIHdpZHRoPSI0MCIgaGVpZ2h0PSI0MCIgdmlld0JveD0iMCAwIDI0IDI0IiBmaWxsPSIjOTk5OTk5Ij48cGF0aCBkPSJNMTIgMTJjMi43NiAwIDUtMi4yNCA1LTVzLTIuMjQtNS01LTUtNSAyLjI0LTUgNSAyLjI0IDUgNSA1em0wIDJjLTIuNjcgMC04IDEuMzQtOCA0djJINHYyaDE2di0yaDR2LTJjMC0yLjY2LTUuMzMtNC04LTR6Ii8+PC9zdmc+';
    }
  }

  closePlayerResults(): void {
    this.showPlayerResults.set(false);
  }
}
