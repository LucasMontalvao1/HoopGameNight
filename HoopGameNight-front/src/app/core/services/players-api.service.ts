import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { timeout } from 'rxjs/operators';

import { environment } from '../../../environments/environment';
import {
  ApiResponse,
  PaginatedResponse,
  PlayerResponse,
  SearchPlayerRequest
} from '../interfaces/api.interface';
import { APP_CONSTANTS } from '../constants/app.constants';

@Injectable({
  providedIn: 'root'
})
export class PlayersApiService {
  private readonly baseUrl = `${environment.apiUrl}/api/v1/players`;

  constructor(private readonly http: HttpClient) {}

  // GET /api/v1/players/search
  async searchPlayers(request: SearchPlayerRequest): Promise<PaginatedResponse<PlayerResponse>> {
    let params = new HttpParams();

    if (request.search) params = params.set('search', request.search);
    if (request.teamId) params = params.set('teamId', request.teamId.toString());
    if (request.position) params = params.set('position', request.position);
    if (request.page) params = params.set('page', request.page.toString());
    if (request.pageSize) params = params.set('pageSize', request.pageSize.toString());

    const response = await firstValueFrom(
      this.http.get<PaginatedResponse<PlayerResponse>>(`${this.baseUrl}/search`, { params })
        .pipe(timeout(APP_CONSTANTS.REQUEST_TIMEOUT))
    );

    return response;
  }

  // GET /api/v1/players (todos os jogadores sem filtro)
  async getAllPlayers(page: number = 1, pageSize: number = 20): Promise<PaginatedResponse<PlayerResponse>> {
    let params = new HttpParams()
      .set('page', page.toString())
      .set('pageSize', pageSize.toString());

    const response = await firstValueFrom(
      this.http.get<PaginatedResponse<PlayerResponse>>(this.baseUrl, { params })
        .pipe(timeout(APP_CONSTANTS.REQUEST_TIMEOUT))
    );

    return response;
  }

  // GET /api/v1/players/{id}
  async getPlayerById(id: number): Promise<PlayerResponse | null> {
    const url = `${this.baseUrl}/${id}`;

    try {
      const response = await firstValueFrom(
        this.http.get<ApiResponse<PlayerResponse>>(url)
          .pipe(timeout(APP_CONSTANTS.REQUEST_TIMEOUT))
      );

      return response.data;
    } catch (error) {
      console.error(`Error fetching player ${id}:`, error);
      return null;
    }
  }

  // GET /api/v1/players/team/{teamId}
  async getPlayersByTeam(teamId: number, page: number = 1, pageSize: number = 20): Promise<PaginatedResponse<PlayerResponse>> {
    let params = new HttpParams()
      .set('page', page.toString())
      .set('pageSize', pageSize.toString());

    const url = `${this.baseUrl}/team/${teamId}`;

    const response = await firstValueFrom(
      this.http.get<PaginatedResponse<PlayerResponse>>(url, { params })
        .pipe(timeout(APP_CONSTANTS.REQUEST_TIMEOUT))
    );

    return response;
  }

  // GET /api/v1/players/position/{position}
  async getPlayersByPosition(position: string, page: number = 1, pageSize: number = 20): Promise<PaginatedResponse<PlayerResponse>> {
    let params = new HttpParams()
      .set('page', page.toString())
      .set('pageSize', pageSize.toString());

    const url = `${this.baseUrl}/position/${position}`;

    const response = await firstValueFrom(
      this.http.get<PaginatedResponse<PlayerResponse>>(url, { params })
        .pipe(timeout(APP_CONSTANTS.REQUEST_TIMEOUT))
    );

    return response;
  }
}
