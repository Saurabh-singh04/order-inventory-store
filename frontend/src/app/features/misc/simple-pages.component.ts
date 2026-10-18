import { Component } from '@angular/core';
import { RouterLink } from '@angular/router';
import { EmptyStateComponent } from '../../shared/components/state-views.component';

@Component({
  selector: 'app-forbidden',
  imports: [RouterLink, EmptyStateComponent],
  template: `
    <app-empty-state title="Access denied" message="Your account doesn't have permission to open this page.">
      <a routerLink="/products" class="btn btn-primary">Back to products</a>
    </app-empty-state>
  `,
})
export class ForbiddenComponent {}

@Component({
  selector: 'app-not-found',
  imports: [RouterLink, EmptyStateComponent],
  template: `
    <app-empty-state title="Page not found" message="The page you're looking for doesn't exist.">
      <a routerLink="/products" class="btn btn-primary">Back to products</a>
    </app-empty-state>
  `,
})
export class NotFoundComponent {}
