import { Injectable, signal, computed, effect } from '@angular/core';
import { GameResponse, GetGamesRequest, SyncStatusResponse } from '../interfaces/api.interface';
import { GamesApiService } from './games-api.service';
import { APP_CONSTANTS } from '../constants/app.constants';

@Injectable({
  providedIn: 'root'
})
export class GamesService {
  private readonly _todayGames = signal<GameResponse[]>([]);
  private readonly _selectedDateGames = signal<GameResponse[]>([]);
  private readonly _selectedDate = signal<Date>(new Date());
  private readonly _isLoading = signal<boolean>(false);
  private readonly _error = signal<string | null>(null);
  private readonly _syncStatus = signal<SyncStatusResponse | null>(null);
  private readonly _lastUpdate = signal<Date | null>(null);

  
  readonly todayGames = this._todayGames.asReadonly();
  readonly selectedDateGames = this._selectedDateGames.asReadonly();
  readonly selectedDate = this._selectedDate.asReadonly();
  readonly isLoading = this._isLoading.asReadonly();
  readonly error = this._error.asReadonly();
  readonly syncStatus = this._syncStatus.asReadonly();
  readonly lastUpdate = this._lastUpdate.asReadonly();

  readonly liveGames = computed(() => 
    this._todayGames().filter(game => game.isLive)
  );

  readonly completedGames = computed(() => 
    this._todayGames().filter(game => game.isCompleted)
  );

  readonly scheduledGames = computed(() => 
    this._todayGames().filter(game => !game.isLive && !game.isCompleted)
  );

  readonly hasLiveGames = computed(() => 
    this.liveGames().length > 0
  );

  readonly selectedDateString = computed(() => 
    this._selectedDate().toISOString().split('T')[0]
  );

  readonly isToday = computed(() => {
    const today = new Date().toISOString().split('T')[0];
    return this.selectedDateString() === today;
  });

  private readonly cache = new Map<string, { data: any; timestamp: number }>();

  constructor(private readonly gamesApi: GamesApiService) {
    effect(() => {
      if (this.hasLiveGames()) {
        this.startAutoRefresh();
      }
    });

    console.log('GamesService inicializado');
  }

  async loadTodayGames(forceRefresh = false): Promise<void> {
    await this.executeWithLoading(async () => {
      const cacheKey = APP_CONSTANTS.CACHE_KEYS.TODAY_GAMES;
      
      if (!forceRefresh && this.isCacheValid(cacheKey)) {
        const cached = this.getFromCache<GameResponse[]>(cacheKey);
        if (cached) {
          this._todayGames.set(cached);
          return;
        }
      }

      const games = await this.gamesApi.getTodayGames();
      this._todayGames.set(games);
      this.setCache(cacheKey, games);
      this._lastUpdate.set(new Date());
      
      console.log(`Carregados ${games.length} jogos de hoje`);
    });
  }

  async loadGamesByDate(date: Date, forceRefresh = false): Promise<void> {
    this._selectedDate.set(date);
    
    await this.executeWithLoading(async () => {
      const dateStr = date.toISOString().split('T')[0];
      const cacheKey = APP_CONSTANTS.CACHE_KEYS.GAMES_BY_DATE.replace('{date}', dateStr);
      
      if (!forceRefresh && this.isCacheValid(cacheKey)) {
        const cached = this.getFromCache<GameResponse[]>(cacheKey);
        if (cached) {
          this._selectedDateGames.set(cached);
          return;
        }
      }

      const games = await this.gamesApi.getGamesByDate(date);
      this._selectedDateGames.set(games);
      this.setCache(cacheKey, games);
      this._lastUpdate.set(new Date());
      
      console.log(`Carregados ${games.length} jogos para ${dateStr}`);
    });
  }

  async navigateToDate(direction: 'prev' | 'next' | 'today'): Promise<void> {
    const currentDate = this._selectedDate();
    let newDate: Date;

    switch (direction) {
      case 'prev':
        newDate = new Date(currentDate);
        newDate.setDate(currentDate.getDate() - 1);
        break;
      case 'next':
        newDate = new Date(currentDate);
        newDate.setDate(currentDate.getDate() + 1);
        break;
      case 'today':
        newDate = new Date();
        break;
    }

    await this.loadGamesByDate(newDate);
  }

  async syncGamesForDate(date?: Date): Promise<void> {
    await this.executeWithLoading(async () => {
      const targetDate = date || this._selectedDate();
      
      if (this.isToday() && !date) {
        await this.gamesApi.syncTodayGames();
        await this.loadTodayGames(true);
      } else {
        await this.gamesApi.syncGamesByDate(targetDate);
        await this.loadGamesByDate(targetDate, true);
      }
      
      await this.checkSyncStatus();
      console.log(`Sincronização concluída para ${targetDate.toISOString().split('T')[0]}`);
    });
  }

  async checkSyncStatus(): Promise<void> {
    try {
      const status = await this.gamesApi.getSyncStatus();
      this._syncStatus.set(status);
    } catch (error) {
      console.error('Erro ao verificar status de sincronização:', error);
    }
  }

  getCurrentGames(): GameResponse[] {
    return this.isToday() ? this._todayGames() : this._selectedDateGames();
  }

  getGameById(id: number): GameResponse | undefined {
    const allGames = [...this._todayGames(), ...this._selectedDateGames()];
    return allGames.find(game => game.id === id);
  }

  clearCache(): void {
    this.cache.clear();
    console.log('Cache de jogos limpo');
  }

  private async executeWithLoading<T>(fn: () => Promise<T>): Promise<T> {
    this._isLoading.set(true);
    this._error.set(null);
    
    try {
      const result = await fn();
      return result;
    } catch (error: any) {
      const errorMessage = this.getErrorMessage(error);
      this._error.set(errorMessage);
      console.error('Erro no GamesService:', error);
      throw error;
    } finally {
      this._isLoading.set(false);
    }
  }

  private getErrorMessage(error: any): string {
    if (error.status === 0) {
      return 'Servidor não disponível';
    }
    
    if (error.status === 404) {
      return 'Dados não encontrados';
    }
    
    if (error.status >= 500) {
      return 'Erro interno do servidor';
    }
    
    return error.message || 'Erro desconhecido';
  }

  private setCache<T>(key: string, data: T): void {
    this.cache.set(key, {
      data,
      timestamp: Date.now()
    });
  }

  private getFromCache<T>(key: string): T | null {
    const cached = this.cache.get(key);
    if (!cached) return null;
    
    return cached.data as T;
  }

  private isCacheValid(key: string): boolean {
    const cached = this.cache.get(key);
    if (!cached) return false;
    
    const isValid = (Date.now() - cached.timestamp) < APP_CONSTANTS.GAMES_CACHE_DURATION;
    return isValid;
  }

  private autoRefreshTimer?: number;

  private startAutoRefresh(): void {
    this.stopAutoRefresh();
    
    this.autoRefreshTimer = window.setInterval(async () => {
      if (this.hasLiveGames()) {
        console.log('Auto-refresh: atualizando jogos ao vivo...');
        await this.loadTodayGames(true);
      } else {
        this.stopAutoRefresh();
      }
    }, APP_CONSTANTS.AUTO_REFRESH_INTERVAL);
  }

  private stopAutoRefresh(): void {
    if (this.autoRefreshTimer) {
      clearInterval(this.autoRefreshTimer);
      this.autoRefreshTimer = undefined;
    }
  }
}