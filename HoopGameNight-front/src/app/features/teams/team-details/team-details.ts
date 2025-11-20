import { Component, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router, RouterModule } from '@angular/router';

import { TeamsService } from '../../../core/services/teams.service';
import { GamesService } from '../../../core/services/games.service';
import { PlayersService } from '../../../core/services/players.service';
import { TeamResponse, GameResponse, TeamSummaryResponse, PlayerResponse } from '../../../core/interfaces/api.interface';

@Component({
  selector: 'app-team-details',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './team-details.html',
  styleUrls: ['./team-details.scss']
})
export class TeamDetails implements OnInit {
  private readonly _team = signal<TeamResponse | null>(null);
  private readonly _recentGames = signal<GameResponse[]>([]);
  private readonly _upcomingGames = signal<GameResponse[]>([]);
  private readonly _teamPlayers = signal<PlayerResponse[]>([]);
  private readonly _loading = signal<boolean>(true);
  private readonly _loadingPlayers = signal<boolean>(false);
  private readonly _error = signal<string | null>(null);

  readonly team = this._team.asReadonly();
  readonly recentGames = this._recentGames.asReadonly();
  readonly upcomingGames = this._upcomingGames.asReadonly();
  readonly teamPlayers = this._teamPlayers.asReadonly();
  readonly loading = this._loading.asReadonly();
  readonly loadingPlayers = this._loadingPlayers.asReadonly();
  readonly error = this._error.asReadonly();

  readonly teamDisplayName = computed(() => {
    const team = this._team();
    if (!team) return '';
    return team.displayName || `${team.city} ${team.name}`;
  });

  readonly wins = computed(() => {
    const games = this._recentGames();
    const team = this._team();
    if (!team) return 0;

    return games.filter(game => {
      const isHomeTeam = game.homeTeam.id === team.id;
      const isVisitorTeam = game.visitorTeam.id === team.id;

      if (isHomeTeam) {
        return (game.homeTeamScore ?? 0) > (game.visitorTeamScore ?? 0);
      }
      if (isVisitorTeam) {
        return (game.visitorTeamScore ?? 0) > (game.homeTeamScore ?? 0);
      }
      return false;
    }).length;
  });

  readonly losses = computed(() => {
    const games = this._recentGames();
    return games.length - this.wins();
  });

  constructor(
    private readonly route: ActivatedRoute,
    private readonly router: Router,
    protected readonly teamsService: TeamsService,
    protected readonly gamesService: GamesService,
    protected readonly playersService: PlayersService
  ) {}

  async ngOnInit(): Promise<void> {
    const abbreviation = this.route.snapshot.paramMap.get('abbreviation');

    if (!abbreviation) {
      this._error.set('Time não encontrado');
      this._loading.set(false);
      return;
    }

    console.log(`TeamDetails: Carregando time com abreviação: ${abbreviation}`);
    await this.loadTeamData(abbreviation);
  }

  private async loadTeamData(abbreviation: string): Promise<void> {
    try {
      this._loading.set(true);
      this._error.set(null);

      console.log(`Carregando dados do time: ${abbreviation}`);

      // Carregar todos os times se ainda não foram carregados
      if (this.teamsService.allTeams().length === 0) {
        console.log('Carregando lista de times...');
        await this.teamsService.loadAllTeams();
      }

      // Buscar time pela abreviação (case-insensitive)
      const upperAbbr = abbreviation.toUpperCase();
      const team = this.teamsService.getTeamByAbbreviation(upperAbbr);

      console.log(`Time encontrado:`, team);

      if (!team) {
        const availableTeams = this.teamsService.allTeams().map(t => t.abbreviation).join(', ');
        console.error(`Time "${abbreviation}" não encontrado. Times disponíveis: ${availableTeams}`);
        this._error.set(`Time "${abbreviation}" não encontrado`);
        this._loading.set(false);
        return;
      }

      this._team.set(team);
      console.log(`Time carregado: ${team.displayName || team.name} (ID: ${team.id})`);

      // Buscar jogos do time
      await this.loadTeamGames(team.id);

      // Buscar jogadores do time
      this.loadTeamPlayers(team.id);

    } catch (error) {
      console.error('Error loading team data:', error);
      this._error.set('Erro ao carregar dados do time');
    } finally {
      this._loading.set(false);
    }
  }

  private async loadTeamGames(teamId: number): Promise<void> {
    try {
      console.log(`Buscando jogos recentes para o time ID: ${teamId}`);

      // Buscar jogos recentes (últimos 30 dias) - otimizado com endpoint específico
      const recentGames = await this.gamesService.getRecentGamesForTeam(teamId, 30);

      console.log(`Jogos recentes carregados:`, recentGames.length);
      console.log('Primeiros jogos recentes:', recentGames.slice(0, 3));

      // Filtrar jogos completados e ordenar por data (mais recente primeiro)
      const completedGames = recentGames
        .filter(g => g.isCompleted)
        .sort((a, b) => new Date(b.date).getTime() - new Date(a.date).getTime());

      // Pegar apenas os últimos 5 jogos para exibição
      const last5Games = completedGames.slice(0, 5);
      this._recentGames.set(last5Games);

      console.log(`Exibindo ${last5Games.length} jogos recentes completados`);

      // Buscar próximos jogos (próximos 7 dias)
      console.log(`Buscando próximos jogos para o time ID: ${teamId}`);
      const upcomingGames = await this.gamesService.getUpcomingGamesForTeam(teamId, 7);
      this._upcomingGames.set(upcomingGames);

      console.log(`${upcomingGames.length} próximos jogos carregados`);

    } catch (error) {
      console.error('Erro ao carregar jogos do time:', error);
    }
  }

  getTeamLogoUrl(abbreviation: string): string {
    return this.teamsService.getTeamLogoUrl(abbreviation);
  }

  getTeamColorPrimary(abbreviation: string): string {
    return this.teamsService.getTeamColorPrimary(abbreviation);
  }

  getTeamColorSecondary(abbreviation: string): string {
    return this.teamsService.getTeamColorSecondary(abbreviation);
  }

  handleLogoError(event: Event, abbreviation: string): void {
    const img = event.target as HTMLImageElement;

    if (img.src.includes('nba-logo-fallback.svg')) {
      return;
    }

    console.warn(`Logo não encontrado para: ${abbreviation}`);
    img.src = 'assets/images/nba-logo-fallback.svg';
    img.classList.add('logo-fallback');
  }

  getGameResult(game: GameResponse): 'win' | 'loss' | 'scheduled' {
    const team = this._team();
    if (!team) return 'scheduled';

    if (game.status === 'Scheduled') return 'scheduled';

    const isHomeTeam = game.homeTeam.id === team.id;
    const homeScore = game.homeTeamScore ?? 0;
    const visitorScore = game.visitorTeamScore ?? 0;

    if (isHomeTeam) {
      return homeScore > visitorScore ? 'win' : 'loss';
    } else {
      return visitorScore > homeScore ? 'win' : 'loss';
    }
  }

  getOpponentTeam(game: GameResponse): TeamSummaryResponse | null {
    const team = this._team();
    if (!team) return null;

    const isHomeTeam = game.homeTeam.id === team.id;
    return isHomeTeam ? game.visitorTeam : game.homeTeam;
  }

  getGameLocation(game: GameResponse): 'home' | 'away' {
    const team = this._team();
    if (!team) return 'away';
    return game.homeTeam.id === team.id ? 'home' : 'away';
  }

  formatDate(date: string | Date): string {
    const gameDate = new Date(date);
    return gameDate.toLocaleDateString('pt-BR', {
      day: '2-digit',
      month: '2-digit',
      year: 'numeric'
    });
  }

  goBack(): void {
    this.router.navigate(['/teams']);
  }

  navigateToGames(): void {
    this.router.navigate(['/games']);
  }

  trackByGameId(index: number, game: GameResponse): number {
    return game.id;
  }

  trackByPlayerId(index: number, player: PlayerResponse): number {
    return player.id;
  }

  private async loadTeamPlayers(teamId: number): Promise<void> {
    try {
      this._loadingPlayers.set(true);
      console.log(`Buscando jogadores do time ID: ${teamId}`);

      // Carregar todos os jogadores do time (máximo 100)
      await this.playersService.loadPlayersByTeam(teamId, 1, 100);
      this._teamPlayers.set(this.playersService.teamPlayers());

      console.log(`${this._teamPlayers().length} jogadores carregados`);
    } catch (error) {
      console.error('Erro ao carregar jogadores:', error);
    } finally {
      this._loadingPlayers.set(false);
    }
  }

  getPlayerPhotoUrl(player: PlayerResponse): string {
    return this.playersService.getPlayerPhotoUrl(player.espnId || player.externalId);
  }

  navigateToPlayer(playerId: number): void {
    this.router.navigate(['/players', playerId]);
  }

  navigateToAllPlayers(): void {
    const team = this._team();
    if (team) {
      this.router.navigate(['/players'], { queryParams: { teamId: team.id } });
    } else {
      this.router.navigate(['/players']);
    }
  }
}
