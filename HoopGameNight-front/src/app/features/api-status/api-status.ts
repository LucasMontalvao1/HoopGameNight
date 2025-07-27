import { Component, OnInit, OnDestroy } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';

import { HealthCheck } from '../../core/services/health-check';
import { StatusIndicator } from '../../shared/components/status-indicator/status-indicator';

@Component({
  selector: 'app-api-status',
  standalone: true,
  imports: [CommonModule, DatePipe, StatusIndicator],
  templateUrl: './api-status.html',
  styleUrls: ['./api-status.scss']
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

  getUptimeFormatted(): string {
    const data = this.healthCheck.healthData();
    if (!data?.uptime) return '0s';
    
    const uptime = data.uptime;
    const days = Math.floor(uptime / 86400);
    const hours = Math.floor((uptime % 86400) / 3600);
    const minutes = Math.floor((uptime % 3600) / 60);
    
    if (days > 0) return `${days}d ${hours}h ${minutes}m`;
    if (hours > 0) return `${hours}h ${minutes}m`;
    return `${minutes}m`;
  }
}