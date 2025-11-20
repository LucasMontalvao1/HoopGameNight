import { Injectable, signal, computed } from '@angular/core';
import { PlayersApiService } from './players-api.service';
import { StorageService } from './storage.service';
import {
  PlayerResponse,
  SearchPlayerRequest,
  PaginatedResponse,
  PlayerPosition
} from '../interfaces/api.interface';
import { APP_CONSTANTS } from '../constants/app.constants';

interface CacheEntry<T> {
  data: T;
  timestamp: number;
  ttl: number;
}

@Injectable({
  providedIn: 'root'
})
export class PlayersService {
  // Private signals
  private readonly _searchResults = signal<PlayerResponse[]>([]);
  private readonly _featuredPlayers = signal<PlayerResponse[]>([]);
  private readonly _selectedPlayer = signal<PlayerResponse | null>(null);
  private readonly _teamPlayers = signal<PlayerResponse[]>([]);
  private readonly _isLoading = signal<boolean>(false);
  private readonly _error = signal<string | null>(null);
  private readonly _searchQuery = signal<string>('');
  private readonly _selectedPosition = signal<string | null>(null);
  private readonly _pagination = signal<{
    currentPage: number;
    pageSize: number;
    totalCount: number;
    totalPages: number;
  }>({
    currentPage: 1,
    pageSize: 20,
    totalCount: 0,
    totalPages: 0
  });
  private readonly _lastUpdate = signal<Date | null>(null);

  // Public readonly signals
  readonly searchResults = this._searchResults.asReadonly();
  readonly featuredPlayers = this._featuredPlayers.asReadonly();
  readonly selectedPlayer = this._selectedPlayer.asReadonly();
  readonly teamPlayers = this._teamPlayers.asReadonly();
  readonly isLoading = this._isLoading.asReadonly();
  readonly error = this._error.asReadonly();
  readonly searchQuery = this._searchQuery.asReadonly();
  readonly selectedPosition = this._selectedPosition.asReadonly();
  readonly pagination = this._pagination.asReadonly();
  readonly lastUpdate = this._lastUpdate.asReadonly();

  // Computed signals
  readonly hasSearchResults = computed(() => this._searchResults().length > 0);
  readonly hasMorePages = computed(() => this._pagination().currentPage < this._pagination().totalPages);
  readonly hasPreviousPage = computed(() => this._pagination().currentPage > 1);

  readonly playersByPosition = computed(() => {
    const players = this._searchResults();
    const groups: Record<string, PlayerResponse[]> = {};

    for (const player of players) {
      const pos = player.position || 'Unknown';
      if (!groups[pos]) {
        groups[pos] = [];
      }
      groups[pos].push(player);
    }

    return groups;
  });

  // Cache
  private cache = new Map<string, CacheEntry<any>>();

  constructor(
    private readonly playersApiService: PlayersApiService,
    private readonly storageService: StorageService
  ) {
    this.loadFeaturedPlayersFromCache();
  }

  // Search players
  async searchPlayers(query: string, page: number = 1, pageSize: number = 20): Promise<void> {
    if (!query || query.trim().length < 2) {
      this._error.set('Digite pelo menos 2 caracteres para buscar');
      return;
    }

    this._isLoading.set(true);
    this._error.set(null);
    this._searchQuery.set(query);

    try {
      const cacheKey = `search_${query}_${page}_${pageSize}`;
      const cached = this.getFromCache<PaginatedResponse<PlayerResponse>>(cacheKey);

      if (cached) {
        this._searchResults.set(cached.data);
        this._pagination.set({
          currentPage: cached.pagination.currentPage,
          pageSize: cached.pagination.pageSize,
          totalCount: cached.pagination.totalCount,
          totalPages: cached.pagination.totalPages
        });
        this._lastUpdate.set(new Date());
        return;
      }

      const response = await this.playersApiService.searchPlayers({
        search: query,
        page,
        pageSize
      });

      this._searchResults.set(response.data);
      this._pagination.set({
        currentPage: response.pagination.currentPage,
        pageSize: response.pagination.pageSize,
        totalCount: response.pagination.totalCount,
        totalPages: response.pagination.totalPages
      });

      this.setCache(cacheKey, response, APP_CONSTANTS.GAMES_CACHE_DURATION);
      this._lastUpdate.set(new Date());
    } catch (err: any) {
      console.error('Error searching players:', err);
      this._error.set(err.userMessage || 'Erro ao buscar jogadores');
    } finally {
      this._isLoading.set(false);
    }
  }

  // Load players by team
  async loadPlayersByTeam(teamId: number, page: number = 1, pageSize: number = 20): Promise<void> {
    this._isLoading.set(true);
    this._error.set(null);

    try {
      const cacheKey = `team_${teamId}_${page}_${pageSize}`;
      const cached = this.getFromCache<PaginatedResponse<PlayerResponse>>(cacheKey);

      if (cached) {
        this._teamPlayers.set(cached.data);
        this._pagination.set({
          currentPage: cached.pagination.currentPage,
          pageSize: cached.pagination.pageSize,
          totalCount: cached.pagination.totalCount,
          totalPages: cached.pagination.totalPages
        });
        this._lastUpdate.set(new Date());
        return;
      }

      const response = await this.playersApiService.getPlayersByTeam(teamId, page, pageSize);

      this._teamPlayers.set(response.data);
      this._pagination.set({
        currentPage: response.pagination.currentPage,
        pageSize: response.pagination.pageSize,
        totalCount: response.pagination.totalCount,
        totalPages: response.pagination.totalPages
      });

      this.setCache(cacheKey, response, APP_CONSTANTS.GAMES_CACHE_DURATION);
      this._lastUpdate.set(new Date());
    } catch (err: any) {
      console.error('Error loading team players:', err);
      this._error.set(err.userMessage || 'Erro ao carregar jogadores do time');
    } finally {
      this._isLoading.set(false);
    }
  }

  // Load players by position
  async loadPlayersByPosition(position: string, page: number = 1, pageSize: number = 20): Promise<void> {
    console.log('loadPlayersByPosition:', { position, page, pageSize });
    this._isLoading.set(true);
    this._error.set(null);
    this._selectedPosition.set(position);

    try {
      const cacheKey = `position_${position}_${page}_${pageSize}`;
      const cached = this.getFromCache<PaginatedResponse<PlayerResponse>>(cacheKey);

      if (cached) {
        console.log('Cache hit para posição:', position, cached.data.length, 'jogadores');
        this._searchResults.set(cached.data);
        this._pagination.set({
          currentPage: cached.pagination.currentPage,
          pageSize: cached.pagination.pageSize,
          totalCount: cached.pagination.totalCount,
          totalPages: cached.pagination.totalPages
        });
        this._lastUpdate.set(new Date());
        return;
      }

      console.log('Chamando API para posição:', position);
      const response = await this.playersApiService.getPlayersByPosition(position, page, pageSize);
      console.log('API retornou:', response.data.length, 'jogadores, total:', response.pagination.totalCount);

      this._searchResults.set(response.data);
      this._pagination.set({
        currentPage: response.pagination.currentPage,
        pageSize: response.pagination.pageSize,
        totalCount: response.pagination.totalCount,
        totalPages: response.pagination.totalPages
      });

      this.setCache(cacheKey, response, APP_CONSTANTS.GAMES_CACHE_DURATION);
      this._lastUpdate.set(new Date());
    } catch (err: any) {
      console.error('Error loading players by position:', err);
      this._error.set(err.userMessage || 'Erro ao carregar jogadores por posição');
    } finally {
      this._isLoading.set(false);
    }
  }

  // Load player details
  async loadPlayerById(playerId: number): Promise<void> {
    this._isLoading.set(true);
    this._error.set(null);

    try {
      const cacheKey = `player_${playerId}`;
      const cached = this.getFromCache<PlayerResponse>(cacheKey);

      if (cached) {
        this._selectedPlayer.set(cached);
        return;
      }

      const player = await this.playersApiService.getPlayerById(playerId);
      this._selectedPlayer.set(player);

      if (player) {
        this.setCache(cacheKey, player, APP_CONSTANTS.GAMES_CACHE_DURATION * 2);
      }
    } catch (err: any) {
      console.error('Error loading player:', err);
      this._error.set(err.userMessage || 'Erro ao carregar jogador');
    } finally {
      this._isLoading.set(false);
    }
  }

  // Load all players (no filter)
  async loadAllPlayers(page: number = 1, pageSize: number = 20): Promise<void> {
    console.log('PlayersService.loadAllPlayers - page:', page, 'pageSize:', pageSize);
    this._isLoading.set(true);
    this._error.set(null);
    this._selectedPosition.set(null);
    this._searchQuery.set('');

    try {
      const cacheKey = `all_players_${page}_${pageSize}`;
      const cached = this.getFromCache<PaginatedResponse<PlayerResponse>>(cacheKey);

      if (cached) {
        console.log('Usando cache:', cached.data.length, 'jogadores');
        this._searchResults.set(cached.data);
        this._pagination.set({
          currentPage: cached.pagination.currentPage,
          pageSize: cached.pagination.pageSize,
          totalCount: cached.pagination.totalCount,
          totalPages: cached.pagination.totalPages
        });
        this._lastUpdate.set(new Date());
        return;
      }

      console.log('Chamando API getAllPlayers');
      const response = await this.playersApiService.getAllPlayers(page, pageSize);

      console.log('API retornou:', response.data.length, 'jogadores');
      console.log('Paginação:', response.pagination);

      this._searchResults.set(response.data);
      this._pagination.set({
        currentPage: response.pagination.currentPage,
        pageSize: response.pagination.pageSize,
        totalCount: response.pagination.totalCount,
        totalPages: response.pagination.totalPages
      });

      this.setCache(cacheKey, response, APP_CONSTANTS.GAMES_CACHE_DURATION);
      this._lastUpdate.set(new Date());
    } catch (err: any) {
      console.error('❌ Error loading all players:', err);
      this._error.set(err.userMessage || 'Erro ao carregar jogadores');
    } finally {
      this._isLoading.set(false);
    }
  }

  // Load featured players (random sample from different teams)
  async loadFeaturedPlayers(): Promise<void> {
    this._isLoading.set(true);
    this._error.set(null);

    try {
      const cacheKey = 'featured_players';
      const cached = this.getFromCache<PlayerResponse[]>(cacheKey);

      if (cached && cached.length > 0) {
        this._featuredPlayers.set(cached);
        return;
      }

      // Load players from different positions as "featured"
      const positions = [PlayerPosition.PG, PlayerPosition.SG, PlayerPosition.SF, PlayerPosition.PF, PlayerPosition.C];
      const featured: PlayerResponse[] = [];

      for (const position of positions) {
        try {
          const response = await this.playersApiService.getPlayersByPosition(position, 1, 2);
          if (response.data.length > 0) {
            featured.push(...response.data);
          }
        } catch {
          // Continue with next position
        }
      }

      this._featuredPlayers.set(featured);
      this.setCache(cacheKey, featured, APP_CONSTANTS.GAMES_CACHE_DURATION * 4);

      // Save to storage for faster initial load
      this.storageService.setAppData('players', 'featured', featured);
    } catch (err: any) {
      console.error('Error loading featured players:', err);
      this._error.set(err.userMessage || 'Erro ao carregar jogadores em destaque');
    } finally {
      this._isLoading.set(false);
    }
  }

  // Pagination
  async nextPage(): Promise<void> {
    if (!this.hasMorePages()) return;

    const currentPagination = this._pagination();
    const nextPage = currentPagination.currentPage + 1;

    if (this._searchQuery()) {
      await this.searchPlayers(this._searchQuery(), nextPage, currentPagination.pageSize);
    } else if (this._selectedPosition()) {
      await this.loadPlayersByPosition(this._selectedPosition()!, nextPage, currentPagination.pageSize);
    } else {
      await this.loadAllPlayers(nextPage, currentPagination.pageSize);
    }
  }

  async previousPage(): Promise<void> {
    if (!this.hasPreviousPage()) return;

    const currentPagination = this._pagination();
    const prevPage = currentPagination.currentPage - 1;

    if (this._searchQuery()) {
      await this.searchPlayers(this._searchQuery(), prevPage, currentPagination.pageSize);
    } else if (this._selectedPosition()) {
      await this.loadPlayersByPosition(this._selectedPosition()!, prevPage, currentPagination.pageSize);
    } else {
      await this.loadAllPlayers(prevPage, currentPagination.pageSize);
    }
  }

  async goToPage(page: number): Promise<void> {
    const currentPagination = this._pagination();
    if (page < 1 || page > currentPagination.totalPages) return;

    if (this._searchQuery()) {
      await this.searchPlayers(this._searchQuery(), page, currentPagination.pageSize);
    } else if (this._selectedPosition()) {
      await this.loadPlayersByPosition(this._selectedPosition()!, page, currentPagination.pageSize);
    } else {
      await this.loadAllPlayers(page, currentPagination.pageSize);
    }
  }

  // Clear state
  clearSearch(): void {
    this._searchResults.set([]);
    this._searchQuery.set('');
    this._selectedPosition.set(null);
    this._pagination.set({
      currentPage: 1,
      pageSize: 20,
      totalCount: 0,
      totalPages: 0
    });
    this._error.set(null);
  }

  clearSelectedPlayer(): void {
    this._selectedPlayer.set(null);
  }

  clearTeamPlayers(): void {
    this._teamPlayers.set([]);
  }

  // Get player photo URL (ESPN CDN)
  getPlayerPhotoUrl(playerId: number | string): string {
    return `https://a.espncdn.com/i/headshots/nba/players/full/${playerId}.png`;
  }

  // Get team logo URL
  getTeamLogoUrl(abbreviation: string): string {
    return `https://a.espncdn.com/i/teamlogos/nba/500/${abbreviation.toLowerCase()}.png`;
  }

  // Private methods
  private loadFeaturedPlayersFromCache(): void {
    const cached = this.getFromCache<PlayerResponse[]>('featured');
    if (cached && cached.length > 0) {
      this._featuredPlayers.set(cached);
    }
  }

  private getFromCache<T>(key: string): T | null {
    const entry = this.cache.get(key);
    if (!entry) return null;

    const now = Date.now();
    if (now - entry.timestamp > entry.ttl) {
      this.cache.delete(key);
      return null;
    }

    return entry.data as T;
  }

  private setCache<T>(key: string, data: T, ttl: number): void {
    this.cache.set(key, {
      data,
      timestamp: Date.now(),
      ttl
    });
  }

  clearCache(): void {
    this.cache.clear();
    this.storageService.clearAppData('players');
  }
}
