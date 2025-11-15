import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { timeout } from 'rxjs/operators';

import { environment } from '../../../environments/environment';
import { 
  ApiResponse, 
  PaginatedResponse, 
  GameResponse, 
  GetGamesRequest,
  SyncStatusResponse 
} from '../interfaces/api.interface';
import { APP_CONSTANTS } from '../constants/app.constants';

@Injectable({
  providedIn: 'root'
})
export class GamesApiService {
  private readonly baseUrl = `${environment.apiUrl}/api/v1/games`;

  constructor(private readonly http: HttpClient) {}

  // GET /api/v1/games/today
  async getTodayGames(): Promise<GameResponse[]> {
    const url = `${this.baseUrl}/today`;
    
    const response = await firstValueFrom(
      this.http.get<ApiResponse<GameResponse[]>>(url)
        .pipe(timeout(APP_CONSTANTS.REQUEST_TIMEOUT))
    );
    
    return response.data;
  }

  // GET /api/v1/games/date/{date}
  async getGamesByDate(date: Date): Promise<GameResponse[]> {
    const dateStr = date.toISOString().split('T')[0]; // yyyy-MM-dd
    const url = `${this.baseUrl}/date/${dateStr}`;
    
    const response = await firstValueFrom(
      this.http.get<ApiResponse<GameResponse[]>>(url)
        .pipe(timeout(APP_CONSTANTS.REQUEST_TIMEOUT))
    );
    
    return response.data;
  }

  // GET /api/v1/games (com filtros)
  async getGames(request: GetGamesRequest): Promise<PaginatedResponse<GameResponse>> {
    let params = new HttpParams();
    
    if (request.page) params = params.set('page', request.page.toString());
    if (request.pageSize) params = params.set('pageSize', request.pageSize.toString());
    if (request.date) params = params.set('date', request.date);
    if (request.startDate) params = params.set('startDate', request.startDate);
    if (request.endDate) params = params.set('endDate', request.endDate);
    if (request.teamId) params = params.set('teamId', request.teamId.toString());
    if (request.status) params = params.set('status', request.status);
    if (request.postSeason !== undefined) params = params.set('postSeason', request.postSeason.toString());
    if (request.season) params = params.set('season', request.season.toString());

    const response = await firstValueFrom(
      this.http.get<PaginatedResponse<GameResponse>>(this.baseUrl, { params })
        .pipe(timeout(APP_CONSTANTS.REQUEST_TIMEOUT))
    );
    
    return response;
  }

  // GET /api/v1/games/{id}
  async getGameById(id: number): Promise<GameResponse | null> {
    const url = `${this.baseUrl}/${id}`;
    
    try {
      const response = await firstValueFrom(
        this.http.get<ApiResponse<GameResponse>>(url)
          .pipe(timeout(APP_CONSTANTS.REQUEST_TIMEOUT))
      );
      
      return response.data;
    } catch (error: any) {
      if (error.status === 404) {
        return null;
      }
      throw error;
    }
  }

  // GET /api/v1/games/team/{teamId}
  async getGamesByTeam(teamId: number, page = 1, pageSize = 25): Promise<PaginatedResponse<GameResponse>> {
    const url = `${this.baseUrl}/team/${teamId}`;
    const params = new HttpParams()
      .set('page', page.toString())
      .set('pageSize', pageSize.toString());

    const response = await firstValueFrom(
      this.http.get<PaginatedResponse<GameResponse>>(url, { params })
        .pipe(timeout(APP_CONSTANTS.REQUEST_TIMEOUT))
    );
    
    return response;
  }

  // POST /api/v1/games/sync/today
  async syncTodayGames(): Promise<any> {
    const url = `${this.baseUrl}/sync/today`;
    
    const response = await firstValueFrom(
      this.http.post<ApiResponse<any>>(url, {})
        .pipe(timeout(APP_CONSTANTS.REQUEST_TIMEOUT))
    );
    
    return response.data;
  }

  // POST /api/v1/games/sync/date/{date}
  async syncGamesByDate(date: Date): Promise<any> {
    const dateStr = date.toISOString().split('T')[0];
    const url = `${this.baseUrl}/sync/date/${dateStr}`;
    
    const response = await firstValueFrom(
      this.http.post<ApiResponse<any>>(url, {})
        .pipe(timeout(APP_CONSTANTS.REQUEST_TIMEOUT))
    );
    
    return response.data;
  }

  // GET /api/v1/games/sync/status
  async getSyncStatus(): Promise<SyncStatusResponse> {
    const url = `${this.baseUrl}/sync/status`;
    
    const response = await firstValueFrom(
      this.http.get<ApiResponse<SyncStatusResponse>>(url)
        .pipe(timeout(APP_CONSTANTS.REQUEST_TIMEOUT))
    );
    
    return response.data;
  }

  // GET /api/v1/games/external/today
  async getTodayGamesFromExternal(): Promise<any[]> {
    const url = `${this.baseUrl}/external/today`;

    const response = await firstValueFrom(
      this.http.get<ApiResponse<any[]>>(url)
        .pipe(timeout(APP_CONSTANTS.REQUEST_TIMEOUT))
    );

    return response.data;
  }

  // GET /api/v1/games/teams/{teamId}/recent?days=X
  async getRecentGamesForTeam(teamId: number, days = 30): Promise<GameResponse[]> {
    const url = `${this.baseUrl}/teams/${teamId}/recent`;
    const params = new HttpParams().set('days', days.toString());

    const response = await firstValueFrom(
      this.http.get<ApiResponse<GameResponse[]>>(url, { params })
        .pipe(timeout(APP_CONSTANTS.REQUEST_TIMEOUT))
    );

    return response.data;
  }

  // GET /api/v1/games/teams/{teamId}/upcoming?days=X
  async getUpcomingGamesForTeam(teamId: number, days = 7): Promise<GameResponse[]> {
    const url = `${this.baseUrl}/teams/${teamId}/upcoming`;
    const params = new HttpParams().set('days', days.toString());

    const response = await firstValueFrom(
      this.http.get<ApiResponse<GameResponse[]>>(url, { params })
        .pipe(timeout(APP_CONSTANTS.REQUEST_TIMEOUT))
    );

    return response.data;
  }
}