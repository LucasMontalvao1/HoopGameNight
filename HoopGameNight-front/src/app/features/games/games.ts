import { Component, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';

import { GamesService } from '../../core/services/games.service';
import { GameResponse } from '../../core/interfaces/api.interface';
import { DatePicker } from '../../shared/components/date-picker/date-picker';

@Component({
  selector: 'app-games',
  standalone: true,
  imports: [CommonModule, RouterModule, DatePicker],
  templateUrl: './games.html',
  styleUrls: ['./games.scss']
})
export class Games implements OnInit, OnDestroy {
  
  constructor(protected readonly gamesService: GamesService) {}

  async ngOnInit(): Promise<void> {
    console.log('Games component inicializado');
    await this.loadInitialData();
    await this.gamesService.checkSyncStatus();
  }

  ngOnDestroy(): void {
  }
  
  async goToPreviousDay(): Promise<void> {
    await this.gamesService.navigateToDate('prev');
  }

  async goToNextDay(): Promise<void> {
    await this.gamesService.navigateToDate('next');
  }

  async goToToday(): Promise<void> {
    await this.gamesService.navigateToDate('today');
  }

  async onDateChange(date: Date): Promise<void> {
    await this.gamesService.loadGamesByDate(date);
  }
 
  async syncCurrentDate(): Promise<void> {
    await this.gamesService.syncGamesForDate();
  }

  async refreshGames(): Promise<void> {
    if (this.gamesService.isToday()) {
      await this.gamesService.loadTodayGames(true);
    } else {
      await this.gamesService.loadGamesByDate(this.gamesService.selectedDate(), true);
    }
  }
  
  getCurrentDate(): string {
    return this.gamesService.selectedDate().toLocaleDateString('pt-BR', {
      weekday: 'long',
      year: 'numeric',
      month: 'long',
      day: 'numeric'
    });
  }

  getShortDate(): string {
    return this.gamesService.selectedDate().toLocaleDateString('pt-BR');
  }

  formatGameTime(dateTime: string): string {
    return new Date(dateTime).toLocaleTimeString('pt-BR', {
      hour: '2-digit',
      minute: '2-digit'
    });
  }

  formatGameDate(dateTime: string): string {
    return new Date(dateTime).toLocaleDateString('pt-BR', {
      day: '2-digit',
      month: 'short'
    });
  }

  trackByGameId(index: number, game: GameResponse): number {
    return game.id;
  }

  
  getTeamLogoUrl(abbreviation: string): string {
  // Usando NBA CDN oficial
  const teamId = this.getTeamIdByAbbreviation(abbreviation);
  return `https://cdn.nba.com/logos/nba/${teamId}/primary/L/logo.svg`;
}

getTeamLogoUrlFallback(abbreviation: string): string {
  // URL alternativa se a primeira não funcionar
  return `https://www.nba.com/.element/img/1.0/teamsites/logos/teamlogos_500x500/${abbreviation.toLowerCase()}.png`;
}

  getTeamColorPrimary(abbreviation: string): string {
    const teamColors: Record<string, string> = {
      'LAL': '#552583', 'GSW': '#1D428A', 'BOS': '#007A33',
      'MIA': '#98002E', 'CHI': '#CE1141', 'NYK': '#006BB6',
      'LAC': '#C8102E', 'BKN': '#000000', 'PHI': '#006BB6',
      'MIL': '#00471B', 'PHX': '#E56020', 'UTA': '#002B5C',
      'ATL': '#E03A3E', 'DEN': '#0E2240', 'IND': '#002D62',
      'CLE': '#860038', 'MEM': '#5D76A9', 'DAL': '#00538C',
      'TOR': '#CE1141', 'CHA': '#1D1160', 'SAS': '#C4CED4',
      'MIN': '#0C2340', 'OKC': '#007AC1', 'NOP': '#0C2340',
      'ORL': '#0077C0', 'WAS': '#002B5C', 'SAC': '#5A2D81',
      'DET': '#C8102E', 'HOU': '#CE1141', 'POR': '#E03A3E'
    };

    return teamColors[abbreviation] || '#6B7280';
  }

  private getTeamIdByAbbreviation(abbreviation: string): number {
  const teamIds: Record<string, number> = {
    'ATL': 1610612737, 'BOS': 1610612738, 'BKN': 1610612751,
    'CHA': 1610612766, 'CHI': 1610612741, 'CLE': 1610612739,
    'DAL': 1610612742, 'DEN': 1610612743, 'DET': 1610612765,
    'GSW': 1610612744, 'HOU': 1610612745, 'IND': 1610612754,
    'LAC': 1610612746, 'LAL': 1610612747, 'MEM': 1610612763,
    'MIA': 1610612748, 'MIL': 1610612749, 'MIN': 1610612750,
    'NOP': 1610612740, 'NYK': 1610612752, 'OKC': 1610612760,
    'ORL': 1610612753, 'PHI': 1610612755, 'PHX': 1610612756,
    'POR': 1610612757, 'SAC': 1610612758, 'SAS': 1610612759,
    'TOR': 1610612761, 'UTA': 1610612762, 'WAS': 1610612764
  };

  return teamIds[abbreviation] || 1610612737; 
}

handleLogoError(event: any, abbreviation: string): void {
  const img = event.target;
  
  // Primeira tentativa: URL alternativa
  if (!img.src.includes('teamlogos_500x500')) {
    img.src = this.getTeamLogoUrlFallback(abbreviation);
    return;
  }
  
  // Segunda tentativa: Logo local personalizado
  if (!img.src.includes('assets')) {
    img.src = `assets/images/teams/${abbreviation.toLowerCase()}.svg`;
    return;
  }
  
  // Última tentativa: Fallback geral
  img.src = 'assets/images/nba-logo-fallback.svg';
}

  private async loadInitialData(): Promise<void> {
    try {
      await this.gamesService.loadTodayGames();
      
      if (!this.gamesService.isToday()) {
        await this.gamesService.loadGamesByDate(this.gamesService.selectedDate());
      }
    } catch (error) {
      console.error('Erro ao carregar dados iniciais:', error);
    }
  }
}