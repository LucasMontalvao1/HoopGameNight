import { Injectable, signal } from '@angular/core';
import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { interval, BehaviorSubject, EMPTY } from 'rxjs';
import { catchError, map, switchMap, takeWhile, timeout } from 'rxjs/operators';

import { environment } from '../../../environments/environment';
import { HealthCheckResponse, ApiStatus } from '../interfaces/api.interface';
import { APP_CONSTANTS } from '../constants/app.constants';

@Injectable({
  providedIn: 'root'
})
export class HealthCheck {
  private readonly _status = signal<ApiStatus>(ApiStatus.LOADING);
  private readonly _lastCheck = signal<Date | null>(null);
  private readonly _healthData = signal<HealthCheckResponse | null>(null);
  private readonly _errorMessage = signal<string | null>(null);

  readonly status = this._status.asReadonly();
  readonly lastCheck = this._lastCheck.asReadonly();
  readonly healthData = this._healthData.asReadonly();
  readonly errorMessage = this._errorMessage.asReadonly();

  private readonly _isMonitoring = new BehaviorSubject<boolean>(false);
  private readonly healthUrl = `${environment.apiUrl}${environment.healthCheckEndpoint}`;

  constructor(private readonly http: HttpClient) {
    console.log('HealthCheck Service inicializado');
    console.log('URL da API:', this.healthUrl);
  }

  startMonitoring(): void {
    console.log('Iniciando monitoramento...');
    
    if (this._isMonitoring.value) {
      console.log('Monitoramento já está ativo');
      return;
    }

    this._isMonitoring.next(true);
    this.checkHealth(); 

    interval(APP_CONSTANTS.HEALTH_CHECK_INTERVAL)
      .pipe(
        takeWhile(() => this._isMonitoring.value),
        switchMap(() => this.performHealthCheck())
      )
      .subscribe();
  }

  stopMonitoring(): void {
    console.log('Parando monitoramento...');
    this._isMonitoring.next(false);
  }

  checkHealth(): void {
    console.log('Verificando saúde da API...');
    this.performHealthCheck().subscribe();
  }

  private performHealthCheck() {
    console.log('Fazendo requisição para:', this.healthUrl);

    this._status.set(ApiStatus.LOADING);
    this._lastCheck.set(new Date());

    return this.http.get<HealthCheckResponse>(this.healthUrl)
      .pipe(
        timeout(120000), // 2 minutos para dar tempo das APIs externas responderem
        map((response: HealthCheckResponse) => {
          console.log('✅ Resposta da API recebida:', response);

          // Mapear status do health check para ApiStatus
          const apiStatus = this.mapHealthStatus(response.status);
          this._status.set(apiStatus);
          this._healthData.set(response);
          this._errorMessage.set(null);

          // Log detalhado
          console.log(`Status: ${response.status} (${response.summary.healthy}/${response.summary.total} checks healthy)`);

          return response;
        }),
        catchError((error: HttpErrorResponse) => {
          console.error('Erro na requisição:', error);

          this._status.set(ApiStatus.OFFLINE);
          this._healthData.set(null);

          const errorMessage = this.getErrorMessage(error);
          this._errorMessage.set(errorMessage);

          return EMPTY;
        })
      );
  }

  private mapHealthStatus(status: string): ApiStatus {
    const normalizedStatus = status.toLowerCase();
    switch (normalizedStatus) {
      case 'healthy':
        return ApiStatus.ONLINE;
      case 'degraded':
        return ApiStatus.ERROR;
      case 'unhealthy':
        return ApiStatus.OFFLINE;
      default:
        return ApiStatus.ERROR;
    }
  }

  private getErrorMessage(error: any): string {
  if (error.name === 'TimeoutError' || error.message?.includes('timeout')) {
    return 'Timeout na requisição (servidor muito lento)';
  }

  if (error.status !== undefined) {
    if (error.status === 0) {
      return 'Servidor não disponível (CORS ou servidor offline)';
    }
    
    if (error.status === 404) {
      return 'Endpoint /health não encontrado';
    }
    
    if (error.status === 403) {
      return 'Acesso negado pela API';
    }
    
    if (error.status >= 500) {
      return 'Erro interno do servidor';
    }
  }
  
  return error.message || 'Erro desconhecido';
}

  isOnline(): boolean {
    return this._status() === ApiStatus.ONLINE;
  }

  isOffline(): boolean {
    return this._status() === ApiStatus.OFFLINE;
  }

  isLoading(): boolean {
    return this._status() === ApiStatus.LOADING;
  }

  getStatusText(): string {
    switch (this._status()) {
      case ApiStatus.ONLINE:
        return 'Online';
      case ApiStatus.OFFLINE:
        return 'Offline';
      case ApiStatus.LOADING:
        return 'Verificando...';
      case ApiStatus.ERROR:
        return 'Erro';
      default:
        return 'Desconhecido';
    }
  }

  getTimeSinceLastCheck(): string {
    const lastCheck = this._lastCheck();
    if (!lastCheck) return 'Nunca verificado';

    const now = new Date();
    const diffMs = now.getTime() - lastCheck.getTime();
    const diffSeconds = Math.floor(diffMs / 1000);

    if (diffSeconds < 60) {
      return `${diffSeconds} segundos atrás`;
    }

    const diffMinutes = Math.floor(diffSeconds / 60);
    if (diffMinutes < 60) {
      return `${diffMinutes} minutos atrás`;
    }

    return lastCheck.toLocaleTimeString('pt-BR');
  }

  // Métodos auxiliares para acessar dados do health check
  getHealthSummary() {
    return this._healthData()?.summary;
  }

  getHealthChecks() {
    return this._healthData()?.checks || [];
  }

  getHealthDuration() {
    return this._healthData()?.duration;
  }

  isDatabaseHealthy(): boolean {
    const dbCheck = this.getHealthChecks().find(check =>
      check.tags.includes('database')
    );
    return dbCheck?.status.toLowerCase() === 'healthy';
  }

  areExternalApisHealthy(): boolean {
    const apiChecks = this.getHealthChecks().filter(check =>
      check.tags.includes('external')
    );
    return apiChecks.every(check => check.status.toLowerCase() === 'healthy');
  }
}