import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { environment } from '../../../environments/environment';
import { firstValueFrom } from 'rxjs';

export interface PlayerSeasonStats {
  season: number;
  teamName: string;
  gamesPlayed: number;
  gamesStarted: number;

  // Médias por jogo
  ppg: number;
  rpg: number;
  apg: number;
  spg: number;
  bpg: number;
  mpg: number;

  // ⚠️ CORREÇÃO: Porcentagens como number | null
  fgPercentage: number | null;
  threePointPercentage: number | null;
  ftPercentage: number | null;

  // Aliases para compatibilidade
  fieldGoalPercentage?: number | null;
  freeThrowPercentage?: number | null;

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

  // Aliases de médias
  avgPoints: number;
  avgRebounds: number;
  avgAssists: number;
  avgMinutes: number;
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

  constructor(private readonly http: HttpClient) {}

  async getPlayerSeasonStats(playerId: number, season: number): Promise<PlayerSeasonStats | null> {
    try {
      console.log(`📡 API: GET /playerstats/${playerId}/season/${season}`);
      
      const response = await firstValueFrom(
        this.http.get<ApiResponse<any>>(
          `${this.apiUrl}/${playerId}/season/${season}`
        )
      );

      console.log('📡 API Response COMPLETA:', JSON.stringify(response, null, 2));

      if (response.success && response.data) {
        const rawData = response.data;
        
        console.log('🔍 INSPECIONANDO PORCENTAGENS NO DATA:');
        console.log('   Todas as chaves:', Object.keys(rawData));
        console.log('   fieldGoalPercentage:', rawData.fieldGoalPercentage);
        console.log('   fgPercentage:', rawData.fgPercentage);
        console.log('   threePointPercentage:', rawData.threePointPercentage);
        console.log('   freeThrowPercentage:', rawData.freeThrowPercentage);
        console.log('   ftPercentage:', rawData.ftPercentage);
        
        // ⚠️ NORMALIZAR: Mapear TODAS as variantes possíveis
        const stats: PlayerSeasonStats = {
          ...rawData,
          // FG% - tentar TODAS as variantes
          fgPercentage: rawData.fgPercentage ?? rawData.fieldGoalPercentage ?? null,
          fieldGoalPercentage: rawData.fieldGoalPercentage ?? rawData.fgPercentage ?? null,
          // FT% - tentar TODAS as variantes
          ftPercentage: rawData.ftPercentage ?? rawData.freeThrowPercentage ?? null,
          freeThrowPercentage: rawData.freeThrowPercentage ?? rawData.ftPercentage ?? null,
          // 3P% - geralmente funciona, mas garantir
          threePointPercentage: rawData.threePointPercentage ?? null
        };

        console.log('✅ STATS NORMALIZADAS:', {
          season: stats.season,
          gamesPlayed: stats.gamesPlayed,
          ppg: stats.ppg,
          fgPercentage: stats.fgPercentage,
          fieldGoalPercentage: stats.fieldGoalPercentage,
          threePointPercentage: stats.threePointPercentage,
          ftPercentage: stats.ftPercentage,
          freeThrowPercentage: stats.freeThrowPercentage
        });

        return stats;
      }

      console.warn('⚠️ API retornou success=false ou data=null');
      return null;
    } catch (error) {
      console.error('❌ Erro ao buscar stats da temporada:', error);
      return null;
    }
  }

  async getPlayerAllSeasons(playerId: number): Promise<PlayerSeasonStats[] | null> {
    try {
      console.log(`📡 API: GET /playerstats/${playerId}/seasons`);
      
      const response = await firstValueFrom(
        this.http.get<ApiResponse<PlayerSeasonStats[]>>(
          `${this.apiUrl}/${playerId}/seasons`
        )
      );

      console.log('📡 API Response (all seasons):', response);

      if (response.success && response.data) {
        console.log(`✅ ${response.data.length} temporadas retornadas`);
        
        // Processar aliases para cada temporada
        response.data.forEach(stats => {
          if (stats.fieldGoalPercentage !== undefined && stats.fgPercentage === undefined) {
            stats.fgPercentage = stats.fieldGoalPercentage;
          }
          if (stats.freeThrowPercentage !== undefined && stats.ftPercentage === undefined) {
            stats.ftPercentage = stats.freeThrowPercentage;
          }
        });

        return response.data;
      }

      console.warn('⚠️ API retornou success=false ou data=null');
      return null;
    } catch (error) {
      console.error('❌ Erro ao buscar todas as temporadas:', error);
      return null;
    }
  }
}