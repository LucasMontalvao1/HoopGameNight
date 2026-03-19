import { Injectable, signal, inject, computed } from '@angular/core';
import { StatsApiService } from './stats-api.service';
import {
    GamePlayerStatsResponse,
    PlayerGamelogResponse,
    PlayerGameStatsDetailedResponse
} from '../interfaces/api.interface';

export interface TeamTotals {
    points: number;
    totalRebounds: number;
    assists: number;
    steals: number;
    blocks: number;
    turnovers: number;
    personalFouls: number;
    fieldGoalsMade: number;
    fieldGoalsAttempted: number;
    threePointersMade: number;
    threePointersAttempted: number;
    freeThrowsMade: number;
    freeThrowsAttempted: number;
}

@Injectable({
    providedIn: 'root'
})
export class StatsService {
    private readonly apiService = inject(StatsApiService);

    private readonly _currentGameBoxscore = signal<GamePlayerStatsResponse | null>(null);
    private readonly _currentPlayerGamelog = signal<PlayerGamelogResponse | null>(null);
    private readonly _currentGameSummary = signal<string | null>(null);
    private readonly _currentGameHighlights = signal<any[]>([]);
    private readonly _isLoading = signal<boolean>(false);
    private readonly _error = signal<string | null>(null);

    private readonly gamelogCache = new Map<string, PlayerGamelogResponse>();

    readonly currentGameBoxscore = this._currentGameBoxscore.asReadonly();
    readonly currentPlayerGamelog = this._currentPlayerGamelog.asReadonly();
    readonly currentGameSummary = this._currentGameSummary.asReadonly();
    readonly currentGameHighlights = this._currentGameHighlights.asReadonly();
    readonly isLoading = this._isLoading.asReadonly();
    readonly error = this._error.asReadonly();

    readonly homeTeamTotals = computed(() =>
        this.calculateTeamTotals(this.currentGameBoxscore()?.homeTeamStats || [])
    );
    readonly visitorTeamTotals = computed(() =>
        this.calculateTeamTotals(this.currentGameBoxscore()?.visitorTeamStats || [])
    );

    private readonly EMPTY_TOTALS: TeamTotals = {
        points: 0,
        totalRebounds: 0,
        assists: 0,
        steals: 0,
        blocks: 0,
        turnovers: 0,
        personalFouls: 0,
        fieldGoalsMade: 0,
        fieldGoalsAttempted: 0,
        threePointersMade: 0,
        threePointersAttempted: 0,
        freeThrowsMade: 0,
        freeThrowsAttempted: 0,
    };

    private calculateTeamTotals(stats: PlayerGameStatsDetailedResponse[]): TeamTotals {
        if (stats.length === 0) return { ...this.EMPTY_TOTALS };

        return stats.reduce((acc, curr) => {
            if (curr.didNotPlay) return acc;
            return {
                points:                  acc.points                  + (curr.points                  || 0),
                totalRebounds:           acc.totalRebounds           + (curr.totalRebounds           || 0),
                assists:                 acc.assists                  + (curr.assists                 || 0),
                steals:                  acc.steals                   + (curr.steals                  || 0),
                blocks:                  acc.blocks                   + (curr.blocks                  || 0),
                turnovers:               acc.turnovers               + (curr.turnovers               || 0),
                personalFouls:           acc.personalFouls           + (curr.personalFouls           || 0),
                fieldGoalsMade:          acc.fieldGoalsMade          + (curr.fieldGoalsMade          || 0),
                fieldGoalsAttempted:     acc.fieldGoalsAttempted     + (curr.fieldGoalsAttempted     || 0),
                threePointersMade:       acc.threePointersMade       + (curr.threePointersMade       || 0),
                threePointersAttempted:  acc.threePointersAttempted  + (curr.threePointersAttempted  || 0),
                freeThrowsMade:          acc.freeThrowsMade          + (curr.freeThrowsMade          || 0),
                freeThrowsAttempted:     acc.freeThrowsAttempted     + (curr.freeThrowsAttempted     || 0),
            };
        }, { ...this.EMPTY_TOTALS });
    }

    async loadGameBoxscore(gameId: number): Promise<void> {
        this._isLoading.set(true);
        this._error.set(null);
        
        // Se mudar o jogo, limpa o resumo anterior para não mostrar dado trocado
        if (this._currentGameBoxscore()?.gameId !== gameId) {
            this._currentGameSummary.set(null);
            this._currentGameHighlights.set([]);
        }

        try {
            const data = await this.apiService.getGameBoxscore(gameId);
            this._currentGameBoxscore.set(data);
        } catch (err) {
            console.error('Error loading boxscore:', err);
            this._error.set('Não foi possível carregar as estatísticas do jogo.');
            this._currentGameBoxscore.set(null);
        } finally {
            this._isLoading.set(false);
        }
    }

    async loadPlayerGamelog(playerId: number, season?: number): Promise<void> {
        const cacheKey = `${playerId}_${season || 'all'}`;
        if (this.gamelogCache.has(cacheKey)) {
            const cachedData = this.gamelogCache.get(cacheKey)!;
            this._currentPlayerGamelog.set({
                ...cachedData,
                games: cachedData.games.slice(0, 30)
            });
            return;
        }

        this._isLoading.set(true);
        this._error.set(null);
        try {
            const data = await this.apiService.getPlayerGamelog(playerId, season);
            if (data && data.games) {
                data.games = data.games.slice(0, 30);
            }
            this.gamelogCache.set(cacheKey, data);
            this._currentPlayerGamelog.set(data);
        } catch (err) {
            console.error('Error loading gamelog:', err);
            this._error.set('Não foi possível carregar o histórico de jogos.');
            this._currentPlayerGamelog.set(null);
        } finally {
            this._isLoading.set(false);
        }
    }

    async loadRecentPlayerGames(playerId: number): Promise<void> {
        this._isLoading.set(true);
        this._error.set(null);
        try {
            const data = await this.apiService.getPlayerRecentGames(playerId);
            this._currentPlayerGamelog.set(data);
        } catch (err) {
            console.error('Error loading recent games:', err);
            this._error.set('Não foi possível carregar os jogos recentes.');
            this._currentPlayerGamelog.set(null);
        } finally {
            this._isLoading.set(false);
        }
    }

    clearState(): void {
        this._currentGameBoxscore.set(null);
        this._currentPlayerGamelog.set(null);
        this._currentGameSummary.set(null);
        this._currentGameHighlights.set([]);
        this._error.set(null);
        // We do not clear the cache here to keep the data across typical navigation
    }

    setGameSummary(markdown: string | null, highlights: any[] = []): void {
        this._currentGameSummary.set(markdown);
        this._currentGameHighlights.set(highlights);
    }
}
