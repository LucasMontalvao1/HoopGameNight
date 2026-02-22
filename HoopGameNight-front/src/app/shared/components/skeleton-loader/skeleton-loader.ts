import { Component, Input, ChangeDetectionStrategy } from '@angular/core';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-skeleton-loader',
  standalone: true,
  imports: [CommonModule],
  template: `
    <div 
      [class]="'skeleton ' + className" 
      [style.width]="width" 
      [style.height]="height"
      [style.borderRadius]="borderRadius"
      [class.skeleton--circle]="variant === 'circle'"
      [class.skeleton--text]="variant === 'text'"
      [attr.aria-hidden]="true"
    ></div>
  `,
  styles: [`
    :host {
      display: inline-block;
      vertical-align: middle;
      line-height: 1;
    }

    .skeleton {
      display: block;
    }
  `],
  changeDetection: ChangeDetectionStrategy.OnPush
})
export class SkeletonLoader {
  @Input() width: string = '100%';
  @Input() height: string = '1rem';
  @Input() borderRadius: string = '';
  @Input() variant: 'text' | 'rect' | 'circle' = 'rect';
  @Input() className: string = '';
}
