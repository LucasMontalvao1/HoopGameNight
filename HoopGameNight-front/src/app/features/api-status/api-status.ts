import { Component, OnInit, OnDestroy, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';

import { HealthCheck } from '../../core/services/health-check';
import { StatusIndicator } from '../../shared/components/status-indicator/status-indicator';

@Component({
  selector: 'app-api-status',
  standalone: true,
  imports: [CommonModule, DatePipe, StatusIndicator],
  templateUrl: './api-status.html',
  styleUrls: ['./api-status.scss'],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class ApiStatus implements OnInit, OnDestroy {
  constructor(protected readonly healthCheck: HealthCheck) {}

  ngOnInit(): void {
    this.healthCheck.startMonitoring();
  }

  ngOnDestroy(): void {
    this.healthCheck.stopMonitoring();
  }

  onManualCheck(): void {
    this.healthCheck.checkHealth();
  }

  getHealthDuration(): string {
    const duration = this.healthCheck.getHealthDuration();
    if (!duration) return 'N/A';

    // A duração está em milissegundos
    if (duration < 1000) {
      return `${Math.round(duration)}ms`;
    }

    const seconds = duration / 1000;
    if (seconds < 60) {
      return `${seconds.toFixed(2)}s`;
    }

    return `${(seconds / 60).toFixed(2)}min`;
  }

  getHealthCheckCount(): string {
    const summary = this.healthCheck.getHealthSummary();
    if (!summary) return 'N/A';

    return `${summary.healthy}/${summary.total}`;
  }
}