import { Component, inject } from '@angular/core';
import { ToastService } from './toast.service';

@Component({
  selector: 'app-toast-container',
  template: `
    <div class="toast-stack" aria-live="polite">
      @for (toast of toasts.toasts(); track toast.id) {
        <div class="toast toast-{{ toast.kind }}" role="status">
          <span>{{ toast.message }}</span>
          <button type="button" class="toast-close" aria-label="Dismiss" (click)="toasts.dismiss(toast.id)">×</button>
        </div>
      }
    </div>
  `,
})
export class ToastContainerComponent {
  protected readonly toasts = inject(ToastService);
}
