import { Injectable } from '@angular/core';
import { HttpClient, HttpParams } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { timeout } from 'rxjs/operators';

import { environment } from '../../../environments/environment';
import { 
  ApiResponse, 
  PaginatedResponse, 
  TeamResponse,
  GetTeamsRequest 
} from '../interfaces/api.interface';
import { APP_CONSTANTS } from '../constants/app.constants';

@Injectable({
  providedIn: 'root'
})
export class TeamsApiService {
  private readonly baseUrl = `${environment.apiUrl}/api/v1/teams`;

  constructor(private readonly http: HttpClient) {}

  // GET /api/v1/teams
  async getAllTeams(): Promise<TeamResponse[]> {
    const response = await firstValueFrom(
      this.http.get<ApiResponse<TeamResponse[]>>(this.baseUrl)
        .pipe(timeout(APP_CONSTANTS.REQUEST_TIMEOUT))
    );
    
    return response.data.map(team => ({
      ...team,
      displayName: team.displayName || `${team.city} ${team.name}`
    }));
  }

  // GET /api/v1/teams (com filtros)
  async getTeams(request: GetTeamsRequest): Promise<PaginatedResponse<TeamResponse>> {
    let params = new HttpParams();
    
    if (request.page) params = params.set('page', request.page.toString());
    if (request.pageSize) params = params.set('pageSize', request.pageSize.toString());
    if (request.conference) params = params.set('conference', request.conference);
    if (request.division) params = params.set('division', request.division);
    if (request.search) params = params.set('search', request.search);

    const response = await firstValueFrom(
      this.http.get<PaginatedResponse<TeamResponse>>(this.baseUrl, { params })
        .pipe(timeout(APP_CONSTANTS.REQUEST_TIMEOUT))
    );
    
    return response;
  }

  // GET /api/v1/teams/{id}
  async getTeamById(id: number): Promise<TeamResponse | null> {
    const url = `${this.baseUrl}/${id}`;
    
    try {
      const response = await firstValueFrom(
        this.http.get<ApiResponse<TeamResponse>>(url)
          .pipe(timeout(APP_CONSTANTS.REQUEST_TIMEOUT))
      );
      
      const team = response.data;
      return {
        ...team,
        displayName: team.displayName || `${team.city} ${team.name}`
      };
    } catch (error: any) {
      if (error.status === 404) {
        return null;
      }
      throw error;
    }
  }

  // GET /api/v1/teams/abbreviation/{abbreviation}
  async getTeamByAbbreviation(abbreviation: string): Promise<TeamResponse | null> {
    const url = `${this.baseUrl}/abbreviation/${abbreviation}`;
    
    try {
      const response = await firstValueFrom(
        this.http.get<ApiResponse<TeamResponse>>(url)
          .pipe(timeout(APP_CONSTANTS.REQUEST_TIMEOUT))
      );
      
      const team = response.data;
      return {
        ...team,
        displayName: team.displayName || `${team.city} ${team.name}`
      };
    } catch (error: any) {
      if (error.status === 404) {
        return null;
      }
      throw error;
    }
  }

  // GET /api/v1/teams/conference/{conference}
  async getTeamsByConference(conference: 'East' | 'West'): Promise<TeamResponse[]> {
    const url = `${this.baseUrl}/conference/${conference}`;
    
    const response = await firstValueFrom(
      this.http.get<ApiResponse<TeamResponse[]>>(url)
        .pipe(timeout(APP_CONSTANTS.REQUEST_TIMEOUT))
    );
    
    return response.data.map(team => ({
      ...team,
      displayName: team.displayName || `${team.city} ${team.name}`
    }));
  }

  // GET /api/v1/teams/division/{division}
  async getTeamsByDivision(division: string): Promise<TeamResponse[]> {
    const url = `${this.baseUrl}/division/${division}`;
    
    const response = await firstValueFrom(
      this.http.get<ApiResponse<TeamResponse[]>>(url)
        .pipe(timeout(APP_CONSTANTS.REQUEST_TIMEOUT))
    );
    
    return response.data.map(team => ({
      ...team,
      displayName: team.displayName || `${team.city} ${team.name}`
    }));
  }

  // POST /api/v1/teams/sync
  async syncTeams(): Promise<any> {
    const url = `${this.baseUrl}/sync`;
    
    const response = await firstValueFrom(
      this.http.post<ApiResponse<any>>(url, {})
        .pipe(timeout(APP_CONSTANTS.REQUEST_TIMEOUT))
    );
    
    return response.data;
  }

  // GET /api/v1/teams/external
  async getTeamsFromExternal(): Promise<any[]> {
    const url = `${this.baseUrl}/external`;
    
    const response = await firstValueFrom(
      this.http.get<ApiResponse<any[]>>(url)
        .pipe(timeout(APP_CONSTANTS.REQUEST_TIMEOUT))
    );
    
    return response.data;
  }
}