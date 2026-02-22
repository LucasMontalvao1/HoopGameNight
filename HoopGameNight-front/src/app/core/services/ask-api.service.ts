import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { firstValueFrom } from 'rxjs';
import { timeout } from 'rxjs/operators';
import { environment } from '../../../environments/environment';
import { AskRequest, AskResponse } from '../interfaces/api.interface';
import { APP_CONSTANTS } from '../constants/app.constants';

@Injectable({
    providedIn: 'root'
})
export class AskApiService {
    private readonly http = inject(HttpClient);
    private readonly baseUrl = `${environment.apiUrl}/api/Ask`;

    async ask(question: string): Promise<AskResponse> {
        const request: AskRequest = { question };

        return await firstValueFrom(
            this.http.post<AskResponse>(this.baseUrl, request)
                .pipe(timeout(60000)) 
        );
    }
}
