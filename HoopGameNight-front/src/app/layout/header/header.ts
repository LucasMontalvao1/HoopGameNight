import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';

import { ThemeService } from '../../core/services/theme';
import { NavigationService } from '../../core/services/navigation';
import { HealthCheck } from '../../core/services/health-check';
import { StatusIndicator } from '../../shared/components/status-indicator/status-indicator';

@Component({
  selector: 'app-header',
  standalone: true,
  imports: [CommonModule, RouterModule, StatusIndicator],
  templateUrl: './header.html',
  styleUrls: ['./header.scss']
})
export class Header {
  constructor(
    protected readonly themeService: ThemeService,
    protected readonly navigationService: NavigationService,
    protected readonly healthCheck: HealthCheck
  ) {}

  onMobileMenuToggle(): void {
    this.navigationService.toggleMobileMenu();
  }

  onThemeToggle(): void {
    this.themeService.toggleTheme();
  }

  onRefreshStatus(): void {
    this.healthCheck.checkHealth();
  }

  getThemeIcon(): string {
    const theme = this.themeService.theme();
    switch (theme) {
      case 'light': return 'light_mode';
      case 'dark': return 'dark_mode';
      default: return 'contrast';
    }
  }
}