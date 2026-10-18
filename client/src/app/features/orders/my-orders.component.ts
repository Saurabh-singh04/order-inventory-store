import { CurrencyPipe, DatePipe } from '@angular/common';
import { Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import { OrderApiService } from '../../core/api/order-api.service';
import { toApiError } from '../../core/errors/api-error';
import { Order, PagedResult } from '../../core/models/api.models';
import { OrderHubService } from '../../core/realtime/order-hub.service';
import { OrderItemsComponent } from '../../shared/components/order-items.component';
import { PaginationComponent } from '../../shared/components/pagination.component';
import { EmptyStateComponent, ErrorAlertComponent, LoadingComponent } from '../../shared/components/state-views.component';
import { StatusBadgeComponent } from '../../shared/components/status-badge.component';
import { ToastService } from '../../shared/toast/toast.service';

@Component({
  selector: 'app-my-orders',
  imports: [
    CurrencyPipe,
    DatePipe,
    RouterLink,
    StatusBadgeComponent,
    OrderItemsComponent,
    PaginationComponent,
    LoadingComponent,
    EmptyStateComponent,
    ErrorAlertComponent,
  ],
  template: `
    <header class="page-header">
      <h1>My orders</h1>
      <span class="muted small">Status updates appear live.</span>
    </header>

    @if (loading()) {
      <app-loading label="Loading your orders…" />
    } @else if (error()) {
      <app-error-alert [message]="error()!" (retry)="load(page())" />
    } @else if (result()) {
      @let data = result()!;
      @if (data.items.length === 0) {
        <app-empty-state title="No orders yet" message="When you place an order it will show up here.">
          <a routerLink="/products" class="btn btn-primary">Start shopping</a>
        </app-empty-state>
      } @else {
        <div class="order-list">
          @for (order of data.items; track order.id) {
            <article class="card order-card">
              <header class="order-card-header">
                <div>
                  <strong>Order #{{ order.id }}</strong>
                  <small class="muted">{{ order.createdAt | date: 'medium' }}</small>
                </div>
                <app-status-badge [status]="order.status" />
              </header>

              <app-order-items [order]="order" />

              <footer class="order-card-footer">
                <span class="muted small">{{ order.items.length }} item(s) · {{ order.total | currency: 'INR' }}</span>
                <!-- Customers may only cancel while the order is still Pending -->
                @if (order.status === 'Pending') {
                  <button type="button" class="btn btn-danger btn-small" [disabled]="busyId() === order.id" (click)="cancel(order)">
                    {{ busyId() === order.id ? 'Cancelling…' : 'Cancel order' }}
                  </button>
                }
              </footer>
            </article>
          }
        </div>
        <app-pagination [page]="data.page" [totalPages]="data.totalPages" [totalCount]="data.totalCount" (pageChange)="load($event)" />
      }
    }
  `,
})
export class MyOrdersComponent {
  private readonly api = inject(OrderApiService);
  private readonly toast = inject(ToastService);
  private readonly hub = inject(OrderHubService);

  protected readonly result = signal<PagedResult<Order> | null>(null);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly page = signal(1);
  protected readonly busyId = signal<number | null>(null);

  constructor() {
    this.load(1);

    this.hub.statusChanged$.pipe(takeUntilDestroyed(inject(DestroyRef))).subscribe((change) => {
      const current = this.result();
      if (!current?.items.some((o) => o.id === change.orderId)) return;

      this.result.set({
        ...current,
        items: current.items.map((o) => (o.id === change.orderId ? { ...o, status: change.newStatus } : o)),
      });
      this.toast.info(`Order #${change.orderId} is now ${change.newStatus}`);
    });
  }

  load(page: number): void {
    this.page.set(page);
    this.loading.set(true);
    this.error.set(null);

    this.api.myOrders(page, 10).subscribe({
      next: (data) => {
        this.result.set(data);
        this.loading.set(false);
      },
      error: (err) => {
        this.error.set(toApiError(err).message);
        this.loading.set(false);
      },
    });
  }

  cancel(order: Order): void {
    if (!confirm(`Cancel order #${order.id}? The items go back into stock.`)) return;

    this.busyId.set(order.id);
    this.api.changeStatus(order.id, 'Cancelled').subscribe({
      next: (updated) => {
        this.replace(updated);
        this.busyId.set(null);
        this.toast.success(`Order #${order.id} cancelled`);
      },
      error: (err) => {
        this.busyId.set(null);
        this.toast.error(toApiError(err).message);
        this.load(this.page());
      },
    });
  }

  private replace(updated: Order): void {
    const current = this.result();
    if (!current) return;
    this.result.set({ ...current, items: current.items.map((o) => (o.id === updated.id ? updated : o)) });
  }
}
