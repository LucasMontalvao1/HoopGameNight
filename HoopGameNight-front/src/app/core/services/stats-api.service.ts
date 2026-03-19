import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { timeout } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import {
    ApiResponse,
    GamePlayerStatsResponse,
    PlayerGamelogResponse
} from '../interfaces/api.interface';
import { APP_CONSTANTS } from '../constants/app.constants';

@Injectable({
    providedIn: 'root'
})
export class StatsApiService {
    private readonly http = inject(HttpClient);
    private readonly gamesStatsUrl = `${environment.apiUrl}/api/v1/gamesstats`;
    private readonly playerStatsUrl = `${environment.apiUrl}/api/v1/playerstats`;

    /**
     * GET /api/v1/gamesstats/GetGameStats?gameId={id}
     */
    async getGameBoxscore(gameId: number): Promise<GamePlayerStatsResponse> {
        const params = new HttpParams().set('gameId', gameId.toString());
        const url = `${this.gamesStatsUrl}/GetGameStats`;

        const response = await firstValueFrom(
            this.http.get<ApiResponse<any>>(url, { params })
                .pipe(timeout(APP_CONSTANTS.REQUEST_TIMEOUT))
        );

        if (response.success && response.data) {
            const data = response.data;

            // Função auxiliar de normalização para itens de stats
            const normalizeStatItem = (item: any) => {
                const getValue = (obj: any, key: string) => {
                    if (!obj) return undefined;
                    const pascalKey = key.charAt(0).toUpperCase() + key.slice(1);
                    const camelKey = key.charAt(0).toLowerCase() + key.slice(1);
                    return obj[camelKey] ?? obj[pascalKey] ?? obj[key];
                };

                const getNum = (obj: any, key: string, defaultValue = 0) => {
                    const val = getValue(obj, key);
                    return (val !== null && val !== undefined) ? Number(val) : defaultValue;
                };

                return {
                    ...item,
                    id: getNum(item, 'id'),
                    playerId: getNum(item, 'playerId'),
                    gameId: getNum(item, 'gameId'),
                    playerFullName: getValue(item, 'playerFullName') || 'Unknown',
                    position: getValue(item, 'position') || '-',
                    didNotPlay: getValue(item, 'didNotPlay') || false,
                    minutesFormatted: getValue(item, 'minutesFormatted') || '-',
                    points: getNum(item, 'points'),
                    totalRebounds: getNum(item, 'totalRebounds') || getNum(item, 'rebounds'),
                    assists: getNum(item, 'assists'),
                    steals: getNum(item, 'steals'),
                    blocks: getNum(item, 'blocks'),
                    fieldGoalsFormatted: getValue(item, 'fieldGoalsFormatted') || '-',
                    threePointersFormatted: getValue(item, 'threePointersFormatted') || '-',
                    freeThrowsFormatted: getValue(item, 'freeThrowsFormatted') || '-',
                    plusMinus: getNum(item, 'plusMinus')
                };
            };

            // Normalizar nomes das listas de stats e seus itens
            const homeStats = data.homeTeamStats ?? data.HomeTeamStats ?? [];
            const visitorStats = data.visitorTeamStats ?? data.VisitorTeamStats ?? [];

            return {
                ...data,
                gameId: data.gameId ?? data.GameId,
                homeTeam: data.homeTeam ?? data.HomeTeam,
                visitorTeam: data.visitorTeam ?? data.VisitorTeam,
                homeScore: data.homeScore ?? data.HomeScore,
                visitorScore: data.visitorScore ?? data.VisitorScore,
                homeTeamStats: homeStats.map(normalizeStatItem),
                visitorTeamStats: visitorStats.map(normalizeStatItem)
            } as GamePlayerStatsResponse;
        }

        return response.data;
    }

    /**
     * GET /api/v1/playerstats/{playerId}/games?season={season}
     */
    async getPlayerGamelog(playerId: number, season?: number): Promise<PlayerGamelogResponse> {
        let params = new HttpParams();
        if (season) params = params.set('season', season.toString());

        // ⚠️ CORREÇÃO: Usar /gamelog em vez de /games, conforme testado no backend
        const url = `${this.playerStatsUrl}/${playerId}/gamelog`;

        const response = await firstValueFrom(
            this.http.get<ApiResponse<PlayerGamelogResponse>>(url, { params })
                .pipe(timeout(APP_CONSTANTS.REQUEST_TIMEOUT))
        );

        if (response.success && response.data) {
            if (Array.isArray(response.data)) {
                return {
                    playerId,
                    playerName: 'Unknown',
                    season: season || 2024,
                    games: response.data.map(game => this.normalizeRecentGame(game))
                } as PlayerGamelogResponse;
            } else if (response.data.games) {
                return {
                    ...response.data,
                    games: response.data.games.map(game => this.normalizeRecentGame(game))
                };
            }
        }
        return response.data;
    }

    /**
     * GET /api/v1/playerstats/{playerId}/recent-games
     */
    async getPlayerRecentGames(playerId: number): Promise<PlayerGamelogResponse> {
        const url = `${this.playerStatsUrl}/${playerId}/recent`;

        const response = await firstValueFrom(
            this.http.get<ApiResponse<PlayerGamelogResponse>>(url)
                .pipe(timeout(APP_CONSTANTS.REQUEST_TIMEOUT))
        );

        if (response.success && response.data) {
            if (Array.isArray(response.data)) {
                return {
                    playerId,
                    playerName: 'Unknown',
                    season: 2024,
                    games: response.data.map(game => this.normalizeRecentGame(game))
                } as PlayerGamelogResponse;
            } else if (response.data.games) {
                return {
                    ...response.data,
                    games: response.data.games.map(game => this.normalizeRecentGame(game))
                };
            }
        }

        return response.data;
    }

    private normalizeRecentGame(game: any): any {
        const safeGet = (obj: any, keys: string[], def: any = null) => {
            if (!obj) return def;
            for (const key of keys) {
                if (obj[key] !== undefined && obj[key] !== null) return obj[key];
            }
            // also try snake_case version of first key if provided
            const primaryKey = keys[0];
            const snakeKey = primaryKey.split(/(?=[A-Z])/).join('_').toLowerCase();
            if (obj[snakeKey] !== undefined && obj[snakeKey] !== null) return obj[snakeKey];

            return def;
        };

        return {
            ...game,
            gameId: safeGet(game, ['gameId', 'GameId', 'id', 'Id'], 0),
            gameDate: safeGet(game, ['gameDate', 'GameDate', 'date', 'Date', 'dateTime', 'DateTime', 'game_date'], null),
            opponent: safeGet(game, ['opponent', 'Opponent', 'opponentAbbreviation', 'OpponentAbbreviation'], 'Unknown'),
            isHome: safeGet(game, ['isHome', 'IsHome'], false),
            result: safeGet(game, ['result', 'Result'], '-'),
            points: Number(safeGet(game, ['points', 'Points'], 0)),
            rebounds: Number(safeGet(game, ['rebounds', 'Rebounds', 'totalRebounds', 'TotalRebounds', 'reb'], 0)),
            assists: Number(safeGet(game, ['assists', 'Assists', 'ast'], 0)),
            steals: Number(safeGet(game, ['steals', 'Steals', 'stl'], 0)),
            blocks: Number(safeGet(game, ['blocks', 'Blocks', 'blk'], 0)),
            minutes: safeGet(game, ['minutes', 'Minutes', 'minutesFormatted', 'min'], '-'),
            fieldGoals: safeGet(game, ['fieldGoals', 'FieldGoals', 'fieldGoalsFormatted', 'fg'], '-'),
            threePointers: safeGet(game, ['threePointers', 'ThreePointers', 'threePointersFormatted', 'tp'], '-'),
            freeThrows: safeGet(game, ['freeThrows', 'FreeThrows', 'freeThrowsFormatted', 'ft'], '-'),
            plusMinus: Number(safeGet(game, ['plusMinus', 'PlusMinus', 'plus_minus'], 0))
        };
    }
}
