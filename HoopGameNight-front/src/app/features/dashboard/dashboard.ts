import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule } from '@angular/router';

import { HealthCheck } from '../../core/services/health-check';
import { GamesService } from '../../core/services/games.service';
import { TeamsService } from '../../core/services/teams.service';
import { StatusIndicator } from '../../shared/components/status-indicator/status-indicator';
import { ApiStatus } from '../../core/interfaces/api.interface';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule, RouterModule, StatusIndicator],
  templateUrl: './dashboard.html',
  styleUrls: ['./dashboard.scss']
})
export class Dashboard implements OnInit {
  readonly ApiStatusEnum = ApiStatus;
  protected selectedDateOption: string = 'today';

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

    console.log(`📅 Dashboard: Carregando jogos para ${targetDate.toLocaleDateString('pt-BR')}`);

    if (value === 'today') {
      await this.gamesService.loadTodayGames(true);
    } else {
      await this.gamesService.loadGamesByDate(targetDate, true);
    }
  }

  navigateToTeam(abbreviation: string): void {
    if (abbreviation) {
      console.log(`🏀 Navegando para detalhes do time: ${abbreviation}`);
      this.router.navigate(['/teams', abbreviation]);
    }
  }

  getCurrentGames() {
    return this.selectedDateOption === 'today'
      ? this.gamesService.todayGames()
      : this.gamesService.currentGames();
  }
}