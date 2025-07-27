import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';

import { HealthCheck } from '../../core/services/health-check';
import { GamesService } from '../../core/services/games.service';
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
    protected readonly gamesService: GamesService
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
    return this.gamesService.todayGames().length;
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
}