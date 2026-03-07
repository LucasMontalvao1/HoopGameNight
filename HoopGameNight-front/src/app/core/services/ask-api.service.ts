import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { timeout } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import { AskRequest, AskResponse, ApiResponse } from '../interfaces/api.interface';
import { APP_CONSTANTS } from '../constants/app.constants';

@Injectable({
    providedIn: 'root'
})
export class AskApiService {
    constructor(private readonly http: HttpClient) { }

    // GET /api/v1/ask/game/{gameId}
    async getGameSummary(gameId: number): Promise<AskResponse> {
        const url = `${this.baseUrl}/game/${gameId}`;

        const response = await firstValueFrom(
            this.http.get<ApiResponse<AskResponse>>(url)
                .pipe(timeout(APP_CONSTANTS.REQUEST_TIMEOUT))
        );

        return response.data;
    }
    private readonly baseUrl = `${environment.apiUrl}/api/v1/Ask`;

    async ask(question: string): Promise<AskResponse> {
        const request: AskRequest = { question };

        const response = await firstValueFrom(
            this.http.post<ApiResponse<AskResponse>>(this.baseUrl, request)
                .pipe(timeout(60000))
        );

        return response.data;
    }
}
