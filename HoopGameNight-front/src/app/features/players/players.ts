import { Component, OnInit, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { PlayersService } from '../../core/services/players.service';
import { PlayerResponse, PlayerPosition, POSITION_NAMES } from '../../core/interfaces/api.interface';

@Component({
  selector: 'app-players',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './players.html',
  styleUrl: './players.scss'
})
export class PlayersComponent implements OnInit {
  // Local signals
  private readonly _searchInput = signal<string>('');
  private readonly _selectedPositionFilter = signal<string>('');
  private readonly _viewMode = signal<'grid' | 'list'>('grid');

  // Public readonly
  readonly searchInput = this._searchInput.asReadonly();
  readonly selectedPositionFilter = this._selectedPositionFilter.asReadonly();
  readonly viewMode = this._viewMode.asReadonly();

  // Constants
  readonly positions = Object.values(PlayerPosition);
  readonly positionNames = POSITION_NAMES;

  // Computed
  readonly displayPlayers = computed(() => {
    return this.playersService.searchResults();
  });

  readonly showingFeatured = computed(() => false); // Sempre mostra resultados paginados

  constructor(
    public readonly playersService: PlayersService,
    private readonly router: Router
  ) {}

  ngOnInit(): void {
    this.loadInitialPlayers();
  }

  async loadInitialPlayers(): Promise<void> {
    // Carrega todos os jogadores ao iniciar
    if (this.playersService.searchResults().length === 0) {
      await this.playersService.loadAllPlayers();
    }
  }

  async onSearch(): Promise<void> {
    const query = this._searchInput();
    if (query.trim().length >= 2) {
      this._selectedPositionFilter.set('');
      await this.playersService.searchPlayers(query);
    }
  }

  async onPositionFilter(position: string): Promise<void> {
    console.log('onPositionFilter chamado:', position);
    this._selectedPositionFilter.set(position);
    this._searchInput.set('');

    if (position) {
      console.log('Carregando jogadores por posição:', position);
      await this.playersService.loadPlayersByPosition(position);
    } else {
      // "Todos" - carrega todos os jogadores
      console.log('Carregando TODOS os jogadores');
      await this.playersService.loadAllPlayers();
    }
    console.log('Resultados:', this.playersService.searchResults().length);
  }

  updateSearchInput(value: string): void {
    this._searchInput.set(value);
  }

  clearFilters(): void {
    this._searchInput.set('');
    this._selectedPositionFilter.set('');
    this.playersService.clearSearch();
  }

  setViewMode(mode: 'grid' | 'list'): void {
    this._viewMode.set(mode);
  }

  selectPlayer(player: PlayerResponse): void {
    this.router.navigate(['/players', player.id]);
  }

  viewTeam(event: Event, abbreviation: string): void {
    event.stopPropagation();
    this.router.navigate(['/teams', abbreviation]);
  }

  getPlayerPhotoUrl(player: PlayerResponse): string {
    return this.playersService.getPlayerPhotoUrl(player.espnId || player.externalId);
  }

  getPositionName(position: string | null): string {
    if (!position) return '';
    return this.positionNames[position as PlayerPosition] || position;
  }

  getTeamLogoUrl(abbreviation: string): string {
    return this.playersService.getTeamLogoUrl(abbreviation);
  }

  onImageError(event: Event): void {
    const img = event.target as HTMLImageElement;
    // Evitar loop infinito verificando se já tentou o fallback
    if (!img.dataset['fallback']) {
      img.dataset['fallback'] = 'true';
      // Usar um ícone de pessoa genérico do Material Icons via data URI
      img.src = 'data:image/svg+xml;base64,PHN2ZyB4bWxucz0iaHR0cDovL3d3dy53My5vcmcvMjAwMC9zdmciIHdpZHRoPSIyMDAiIGhlaWdodD0iMjAwIiB2aWV3Qm94PSIwIDAgMjQgMjQiIGZpbGw9IiM5OTk5OTkiPjxwYXRoIGQ9Ik0xMiAxMmMyLjc2IDAgNS0yLjI0IDUtNXMtMi4yNC01LTUtNS01IDIuMjQtNSA1IDIuMjQgNSA1IDV6bTAgMmMtMi42NyAwLTggMS4zNC04IDR2Mkg0djJoMTZ2LTJoNHYtMmMwLTIuNjYtNS4zMy00LTgtNHoiLz48L3N2Zz4=';
    }
  }

  onTeamLogoError(event: Event): void {
    const img = event.target as HTMLImageElement;
    img.style.display = 'none';
  }

  async nextPage(): Promise<void> {
    console.log('Próxima página');
    await this.playersService.nextPage();
    this.scrollToTop();
  }

  async previousPage(): Promise<void> {
    console.log('Página anterior');
    await this.playersService.previousPage();
    this.scrollToTop();
  }

  private scrollToTop(): void {
    window.scrollTo({ top: 0, behavior: 'smooth' });
  }
}
