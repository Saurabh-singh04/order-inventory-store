import { Component, input, output } from '@angular/core';

/** Spinner shown while data loads. */
@Component({
  selector: 'app-loading',
  template: `
    <div class="state-view" role="status">
      <span class="spinner" aria-hidden="true"></span>
      <span>{{ label() }}</span>
    </div>
  `,
})
export class LoadingComponent {
  readonly label = input('Loading…');
}

/** Friendly message when a list has no rows. */
@Component({
  selector: 'app-empty-state',
  template: `
    <div class="state-view empty">
      <strong>{{ title() }}</strong>
      @if (message()) {
        <p>{{ message() }}</p>
      }
      <ng-content />
    </div>
  `,
})
export class EmptyStateComponent {
  readonly title = input.required<string>();
  readonly message = input<string>();
}

/** Error banner with an optional retry button. */
@Component({
  selector: 'app-error-alert',
  template: `
    <div class="alert alert-error" role="alert">
      <span>{{ message() }}</span>
      @if (retryable()) {
        <button type="button" class="btn btn-small" (click)="retry.emit()">Try again</button>
      }
    </div>
  `,
})
export class ErrorAlertComponent {
  readonly message = input.required<string>();
  readonly retryable = input(true);
  readonly retry = output<void>();
}
