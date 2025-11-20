import { Component, OnInit, OnDestroy, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterModule } from '@angular/router';

import { GamesService } from '../../core/services/games.service';
import { TeamsService } from '../../core/services/teams.service';
import { GameResponse } from '../../core/interfaces/api.interface';
import { DatePicker } from '../../shared/components/date-picker/date-picker';

@Component({
  selector: 'app-games',
  standalone: true,
  imports: [CommonModule, RouterModule, DatePicker],
  templateUrl: './games.html',
  styleUrls: ['./games.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class Games implements OnInit, OnDestroy {

  constructor(
    protected readonly gamesService: GamesService,
    protected readonly teamsService: TeamsService,
    private readonly router: Router
  ) {}

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

  async goToYesterday(): Promise<void> {
    const yesterday = new Date();
    yesterday.setDate(yesterday.getDate() - 1);
    await this.gamesService.loadGamesByDate(yesterday);
  }

  async loadCompletedGames(): Promise<void> {
    // Carregar jogos de 3 dias atrás 
    const date = new Date();
    date.setDate(date.getDate() - 3);

    console.log(`Carregando jogos de ${date.toLocaleDateString('pt-BR')}...`);
    await this.gamesService.loadGamesByDate(date);

    // Forçar sincronização
    await this.syncCurrentDate();

    const games = this.gamesService.currentGames();
    console.log(`Total de jogos: ${games.length}`);

    // Mostrar status de TODOS os jogos
    games.forEach(game => {
      console.log(`${game.gameTitle}:`, {
        status: game.status,
        statusDisplay: game.statusDisplay,
        isLive: game.isLive,
        isCompleted: game.isCompleted,
        score: `${game.visitorTeamScore ?? 0} x ${game.homeTeamScore ?? 0}`,
        winningTeam: game.winningTeam
      });
    });

    const completed = games.filter(g => g.isCompleted);
    console.log(`${completed.length} jogos finalizados encontrados!`);
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

  formatGamePeriod(game: GameResponse): string {
    if (!game.period) {
      return game.statusDisplay || 'AO VIVO';
    }

    const periodNames: { [key: number]: string } = {
      1: '1º Q',
      2: '2º Q',
      3: '3º Q',
      4: '4º Q',
      5: 'OT',
      6: '2OT',
      7: '3OT'
    };

    const periodName = periodNames[game.period] || `${game.period}Q`;

    if (game.timeRemaining) {
      return `${periodName} - ${game.timeRemaining}`;
    }

    return periodName;
  }

  trackByGameId(index: number, game: GameResponse): number {
    return game.id;
  }

  getTeamLogoUrl(abbreviation: string): string {
    return this.teamsService.getTeamLogoUrl(abbreviation);
  }

  getTeamColorPrimary(abbreviation: string): string {
    return this.teamsService.getTeamColorPrimary(abbreviation);
  }

  handleLogoError(event: Event, abbreviation: string): void {
    const img = event.target as HTMLImageElement;

    if (img.src.includes('nba-logo-fallback.svg')) {
      return;
    }

    console.warn(`Logo não encontrado para: ${abbreviation}`);
    img.src = 'data:image/svg+xml;base64,PHN2ZyB4bWxucz0iaHR0cDovL3d3dy53My5vcmcvMjAwMC9zdmciIHZpZXdCb3g9IjAgMCAxMDAgMTAwIiB3aWR0aD0iMTAwIiBoZWlnaHQ9IjEwMCI+CiAgPHJlY3Qgd2lkdGg9IjEwMCIgaGVpZ2h0PSIxMDAiIHJ4PSIxMCIgZmlsbD0iI2YzZjRmNiIvPgogIDx0ZXh0IHg9IjUwIiB5PSI1MCIgdGV4dC1hbmNob3I9Im1pZGRsZSIgZG9taW5hbnQtYmFzZWxpbmU9Im1pZGRsZSIgZm9udC1mYW1pbHk9IkFyaWFsLCBzYW5zLXNlcmlmIiBmb250LXNpemU9IjE0IiBmaWxsPSIjOWNhM2FmIj5OQkE8L3RleHQ+Cjwvc3ZnPg==';
    img.classList.add('logo-fallback');
  }

  navigateToTeam(abbreviation: string): void {
    if (abbreviation) {
      this.router.navigate(['/teams', abbreviation]);
    }
  }

  private async loadInitialData(): Promise<void> {
    try {
      await this.gamesService.loadTodayGames();

      if (!this.gamesService.isToday()) {
        await this.gamesService.loadGamesByDate(this.gamesService.selectedDate());
      }

      const games = this.gamesService.currentGames();
      const completedGames = games.filter(g => g.isCompleted);
      console.log(`Jogos finalizados: ${completedGames.length}`);
      completedGames.forEach(game => {
        console.log(`${game.gameTitle}:`, {
          completed: game.isCompleted,
          score: `${game.visitorTeamScore} x ${game.homeTeamScore}`,
          winningTeam: game.winningTeam,
          visitorId: game.visitorTeam.id,
          homeId: game.homeTeam.id
        });
      });

    } catch (error) {
      console.error('Erro ao carregar dados iniciais:', error);
    }
  }

  isFutureDate(): boolean {
    const today = new Date();
    today.setHours(0, 0, 0, 0);

    const selected = new Date(this.gamesService.selectedDate());
    selected.setHours(0, 0, 0, 0);

    return selected > today;
  }

  getSyncButtonTooltip(): string {
    if (this.isFutureDate()) {
      return 'Sincronização disponível apenas para jogos já realizados';
    }
    if (this.gamesService.isLoading()) {
      return 'Carregando...';
    }
    return 'Atualizar jogos com dados mais recentes da API externa';
  }
}
