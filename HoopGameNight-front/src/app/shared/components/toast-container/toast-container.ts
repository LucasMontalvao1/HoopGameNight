import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { NotificationService } from '../../../core/services/notification.service';

@Component({
    selector: 'app-toast-container',
    standalone: true,
    imports: [CommonModule],
    template: `
    <div class="toast-container">
      @for (toast of notificationService.notifications(); track toast.id) {
        <div 
          class="toast" 
          [class]="'toast--' + toast.type"
          role="alert"
          (click)="notificationService.dismiss(toast.id)"
        >
          <span class="material-icons toast__icon">
            @switch (toast.type) {
              @case ('success') { check_circle }
              @case ('error') { error }
              @case ('warning') { warning }
              @default { info }
            }
          </span>
          <span class="toast__message">{{ toast.message }}</span>
          <button class="toast__close" aria-label="Fechar">
            <span class="material-icons">close</span>
          </button>
        </div>
      }
    </div>
  `,
    styleUrls: ['./toast-container.scss']
})
export class ToastContainer {
    protected readonly notificationService = inject(NotificationService);
}
