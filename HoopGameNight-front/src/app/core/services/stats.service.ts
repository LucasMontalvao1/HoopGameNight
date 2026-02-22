import { Injectable, signal, inject, computed } from '@angular/core';
import { StatsApiService } from './stats-api.service';
import {
    GamePlayerStatsResponse,
    PlayerGamelogResponse,
    PlayerGameStatsDetailedResponse
} from '../interfaces/api.interface';

@Injectable({
    providedIn: 'root'
})
export class StatsService {
    private readonly apiService = inject(StatsApiService);

    // State
    private readonly _currentGameBoxscore = signal<GamePlayerStatsResponse | null>(null);
    private readonly _currentPlayerGamelog = signal<PlayerGamelogResponse | null>(null);
    private readonly _isLoading = signal<boolean>(false);
    private readonly _error = signal<string | null>(null);

    // Public State
    readonly currentGameBoxscore = this._currentGameBoxscore.asReadonly();
    readonly currentPlayerGamelog = this._currentPlayerGamelog.asReadonly();
    readonly isLoading = this._isLoading.asReadonly();
    readonly error = this._error.asReadonly();

    // Computed Totals
    readonly homeTeamTotals = computed(() => this.calculateTeamTotals(this.currentGameBoxscore()?.homeTeamStats || []));
    readonly visitorTeamTotals = computed(() => this.calculateTeamTotals(this.currentGameBoxscore()?.visitorTeamStats || []));

    private calculateTeamTotals(stats: PlayerGameStatsDetailedResponse[]): Partial<PlayerGameStatsDetailedResponse> {
        if (stats.length === 0) return {};

        return stats.reduce((acc, curr) => {
            if (curr.didNotPlay) return acc;

            return {
                points: (acc.points || 0) + (curr.points || 0),
                totalRebounds: (acc.totalRebounds || 0) + (curr.totalRebounds || 0),
                assists: (acc.assists || 0) + (curr.assists || 0),
                steals: (acc.steals || 0) + (curr.steals || 0),
                blocks: (acc.blocks || 0) + (curr.blocks || 0),
                turnovers: (acc.turnovers || 0) + (curr.turnovers || 0),
                personalFouls: (acc.personalFouls || 0) + (curr.personalFouls || 0),
                // Para shooting stats, somamos os made/attempted separadamente
                fieldGoalsMade: (acc.fieldGoalsMade || 0) + (curr.fieldGoalsMade || 0),
                fieldGoalsAttempted: (acc.fieldGoalsAttempted || 0) + (curr.fieldGoalsAttempted || 0),
                threePointersMade: (acc.threePointersMade || 0) + (curr.threePointersMade || 0),
                threePointersAttempted: (acc.threePointersAttempted || 0) + (curr.threePointersAttempted || 0),
                freeThrowsMade: (acc.freeThrowsMade || 0) + (curr.freeThrowsMade || 0),
                freeThrowsAttempted: (acc.freeThrowsAttempted || 0) + (curr.freeThrowsAttempted || 0),
            };
        }, {} as Partial<PlayerGameStatsDetailedResponse>);
    }

    async loadGameBoxscore(gameId: number): Promise<void> {
        this._isLoading.set(true);
        this._error.set(null);
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
        this._isLoading.set(true);
        this._error.set(null);
        try {
            const data = await this.apiService.getPlayerGamelog(playerId, season);
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
        this._error.set(null);
    }
}
