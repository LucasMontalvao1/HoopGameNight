import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { firstValueFrom } from 'rxjs';

export interface PlayerSeasonStats {
  season: number;
  teamName: string;
  gamesPlayed: number;
  gamesStarted: number;
  seasonType?: number;
  isPlayoffs?: boolean;

  // Médias por jogo
  ppg: number;
  rpg: number;
  apg: number;
  spg: number;
  bpg: number;
  mpg: number;
  tpg: number;
  fpg: number;

  // ⚠️ CORREÇÃO: Porcentagens como number | null
  fgPercentage: number | null;
  threePointPercentage: number | null;
  ftPercentage: number | null;

  // Aliases para compatibilidade
  fieldGoalPercentage?: number | null;
  freeThrowPercentage?: number | null;
  avgPoints: number;
  avgRebounds: number;
  avgAssists: number;
  avgMinutes: number;
  avgTurnovers: number;
  avgFouls: number;

  // Totais
  totalPoints: number;
  totalRebounds: number;
  totalAssists: number;

  // Dados brutos de arremessos
  fieldGoalsMade: number;
  fieldGoalsAttempted: number;
  threePointersMade: number;
  threePointersAttempted: number;
  freeThrowsMade: number;
  freeThrowsAttempted: number;

  // Rebotes detalhados
  offensiveRebounds: number;
  defensiveRebounds: number;

  // Outras estatísticas
  steals: number;
  blocks: number;
  turnovers: number;
  personalFouls: number;
  minutesPlayed: number;
  points: number;
}

interface ApiResponse<T> {
  success: boolean;
  message: string;
  data: T;
  timestamp: string;
  requestId: string;
}

@Injectable({
  providedIn: 'root'
})
export class PlayerStatsApiService {
  private readonly apiUrl = `${environment.apiUrl}/api/v1/playerstats`;

  constructor(private readonly http: HttpClient) { }

  async getPlayerSeasonStats(playerId: number, season: number): Promise<PlayerSeasonStats | null> {
    try {
      console.log(`📡 API: GET /playerstats/${playerId}/season/${season}`);

      const response = await firstValueFrom(
        this.http.get<ApiResponse<any>>(
          `${this.apiUrl}/${playerId}/season/${season}`
        )
      );

      if (response.success && response.data) {
        return this.normalizeStats(response.data);
      }

      return null;
    } catch (error) {
      console.error('❌ Erro ao buscar stats da temporada:', error);
      return null;
    }
  }

  async getPlayerAllSeasons(playerId: number): Promise<PlayerSeasonStats[] | null> {
    try {
      console.log(`📡 API: GET /playerstats/${playerId}/career`);

      const response = await firstValueFrom(
        this.http.get<ApiResponse<any>>(
          `${this.apiUrl}/${playerId}/career`
        )
      );

      if (response.success && response.data && response.data.seasonStats) {
        const seasonStats = response.data.seasonStats as any[];
        return seasonStats.map(stats => this.normalizeStats(stats));
      }

      return null;
    } catch (error) {
      console.error('❌ Erro ao buscar temporadas via /career:', error);
      return null;
    }
  }

  private normalizeStats(rawData: any): PlayerSeasonStats {
    // Função auxiliar que retorna undefined se não encontrar a chave, permitindo fallbacks
    const getValueRaw = (obj: any, key: string) => {
      if (!obj) return undefined;
      const pascalKey = key.charAt(0).toUpperCase() + key.slice(1);
      const camelKey = key.charAt(0).toLowerCase() + key.slice(1);
      return obj[camelKey] ?? obj[pascalKey] ?? obj[key];
    };

    const getNum = (obj: any, key: string, defaultValue = 0) => {
      const val = getValueRaw(obj, key);
      return (val !== null && val !== undefined) ? Number(val) : defaultValue;
    };

    const stats: PlayerSeasonStats = {
      season: getNum(rawData, 'season'),
      teamName: getValueRaw(rawData, 'teamName') || 'Unknown',
      gamesPlayed: getNum(rawData, 'gamesPlayed'),
      gamesStarted: getNum(rawData, 'gamesStarted'),
      seasonType: getNum(rawData, 'seasonType'),
      isPlayoffs: getValueRaw(rawData, 'isPlayoffs') ?? (getNum(rawData, 'seasonType') === 3),

      // Médias (Tenta ppg, depois avgPoints, etc)
      ppg: getNum(rawData, 'ppg') || getNum(rawData, 'avgPoints'),
      rpg: getNum(rawData, 'rpg') || getNum(rawData, 'avgRebounds'),
      apg: getNum(rawData, 'apg') || getNum(rawData, 'avgAssists'),
      spg: getNum(rawData, 'spg') || getNum(rawData, 'avgSteals'),
      bpg: getNum(rawData, 'bpg') || getNum(rawData, 'avgBlocks'),
      mpg: getNum(rawData, 'mpg') || getNum(rawData, 'avgMinutes'),
      tpg: getNum(rawData, 'tpg') || getNum(rawData, 'avgTurnovers'),
      fpg: getNum(rawData, 'fpg') || getNum(rawData, 'avgFouls'),

      // Porcentagens
      fgPercentage: getValueRaw(rawData, 'fgPercentage') ?? getValueRaw(rawData, 'fieldGoalPercentage') ?? null,
      fieldGoalPercentage: getValueRaw(rawData, 'fieldGoalPercentage') ?? getValueRaw(rawData, 'fgPercentage') ?? null,
      threePointPercentage: getValueRaw(rawData, 'threePointPercentage') ?? null,
      ftPercentage: getValueRaw(rawData, 'ftPercentage') ?? getValueRaw(rawData, 'freeThrowPercentage') ?? null,
      freeThrowPercentage: getValueRaw(rawData, 'freeThrowPercentage') ?? getValueRaw(rawData, 'ftPercentage') ?? null,

      // Totais
      totalPoints: getNum(rawData, 'totalPoints') || getNum(rawData, 'points'),
      totalRebounds: getNum(rawData, 'totalRebounds'),
      totalAssists: getNum(rawData, 'totalAssists') || getNum(rawData, 'assists'),

      // Arremessos
      fieldGoalsMade: getNum(rawData, 'fieldGoalsMade'),
      fieldGoalsAttempted: getNum(rawData, 'fieldGoalsAttempted'),
      threePointersMade: getNum(rawData, 'threePointersMade'),
      threePointersAttempted: getNum(rawData, 'threePointersAttempted'),
      freeThrowsMade: getNum(rawData, 'freeThrowsMade'),
      freeThrowsAttempted: getNum(rawData, 'freeThrowsAttempted'),

      // Detalhes extras
      offensiveRebounds: getNum(rawData, 'offensiveRebounds'),
      defensiveRebounds: getNum(rawData, 'defensiveRebounds'),
      steals: getNum(rawData, 'steals'),
      blocks: getNum(rawData, 'blocks'),
      turnovers: getNum(rawData, 'turnovers'),
      personalFouls: getNum(rawData, 'personalFouls'),
      minutesPlayed: getNum(rawData, 'minutesPlayed'),
      points: getNum(rawData, 'totalPoints') || getNum(rawData, 'points'), // Usar total se points for 0

      // Aliases redundantes para segurança
      avgPoints: getNum(rawData, 'avgPoints') || getNum(rawData, 'ppg'),
      avgRebounds: getNum(rawData, 'avgRebounds') || getNum(rawData, 'rpg'),
      avgAssists: getNum(rawData, 'avgAssists') || getNum(rawData, 'apg'),
      avgMinutes: getNum(rawData, 'avgMinutes') || getNum(rawData, 'mpg'),
      avgTurnovers: getNum(rawData, 'avgTurnovers') || getNum(rawData, 'tpg'),
      avgFouls: getNum(rawData, 'avgFouls') || getNum(rawData, 'fpg'),
    };

    return stats;
  }
}