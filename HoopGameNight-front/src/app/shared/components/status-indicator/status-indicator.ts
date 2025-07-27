import { Component, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ApiStatus } from '../../../core/interfaces/api.interface';

@Component({
  selector: 'app-status-indicator',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './status-indicator.html',
  styleUrls: ['./status-indicator.scss']
})
export class StatusIndicator {
  @Input() status: ApiStatus = ApiStatus.LOADING;
  @Input() showText: boolean = true;

  getStatusText(): string {
    switch (this.status) {
      case ApiStatus.ONLINE: return 'Online';
      case ApiStatus.OFFLINE: return 'Offline';
      case ApiStatus.LOADING: return 'Verificando...';
      default: return 'Desconhecido';
    }
  }
}