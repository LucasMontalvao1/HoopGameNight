import { Component, Input, Output, EventEmitter, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
    selector: 'app-error-view',
    standalone: true,
    imports: [CommonModule],
    template: `
    <div class="error-view" [class]="className">
      <span class="material-icons error-view__icon">{{ icon }}</span>
      <h3 class="error-view__title">{{ title }}</h3>
      <p class="error-view__message">{{ message }}</p>
      @if (showRetry) {
        <button class="btn btn--primary" (click)="retry.emit()">
          <span class="material-icons">refresh</span>
          {{ retryLabel }}
        </button>
      }
    </div>
  `,
    styles: [`
    .error-view {
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      padding: var(--spacing-2xl);
      text-align: center;
      background: var(--surface-primary);
      border: 1px solid var(--border-primary);
      border-radius: var(--border-radius-xl);

      :root.dark & {
        background: rgba(255, 255, 255, 0.03);
        border-color: rgba(255, 255, 255, 0.1);
      }

      &__icon {
        font-size: 3rem;
        color: var(--error);
        margin-bottom: var(--spacing-md);
      }

      &__title {
        color: var(--text-primary);
        margin-bottom: var(--spacing-sm);
        font-size: var(--font-size-xl);
      }

      &__message {
        color: var(--text-secondary);
        margin-bottom: var(--spacing-xl);
        max-width: 400px;
        line-height: var(--line-height-relaxed);
      }

      .btn {
        display: inline-flex;
        align-items: center;
        gap: var(--spacing-xs);
        padding: var(--spacing-sm) var(--spacing-lg);
        border-radius: var(--border-radius-md);
        font-weight: var(--font-weight-medium);
        cursor: pointer;
        transition: all var(--transition-fast);
        
        &--primary {
          background: linear-gradient(135deg, #00d4ff 0%, #5b8def 100%);
          color: white;
          border: none;

          &:hover {
            transform: translateY(-2px);
            box-shadow: 0 4px 12px rgba(0, 212, 255, 0.3);
          }
        }
      }
    }
  `],
    changeDetection: ChangeDetectionStrategy.OnPush
})
export class ErrorView {
    @Input() title: string = 'Ops! Algo deu errado';
    @Input() message: string = 'Não foi possível carregar os dados. Por favor, tente novamente.';
    @Input() icon: string = 'error_outline';
    @Input() showRetry: boolean = true;
    @Input() retryLabel: string = 'Tentar Novamente';
    @Input() className: string = '';

    @Output() retry = new EventEmitter<void>();
}
