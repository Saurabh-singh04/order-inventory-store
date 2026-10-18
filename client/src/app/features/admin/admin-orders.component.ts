import { CurrencyPipe, DatePipe } from '@angular/common';
import { Component, DestroyRef, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { OrderApiService } from '../../core/api/order-api.service';
import { toApiError } from '../../core/errors/api-error';
import { Order, OrderStatus, PagedResult } from '../../core/models/api.models';
import { OrderHubService } from '../../core/realtime/order-hub.service';
import { OrderItemsComponent } from '../../shared/components/order-items.component';
import { PaginationComponent } from '../../shared/components/pagination.component';
import { EmptyStateComponent, ErrorAlertComponent, LoadingComponent } from '../../shared/components/state-views.component';
import { StatusBadgeComponent } from '../../shared/components/status-badge.component';
import { ToastService } from '../../shared/toast/toast.service';

/** Mirrors the server-side state machine so only valid buttons are shown. */
const NEXT_STEPS: Record<OrderStatus, { status: OrderStatus; label: string; style: string }[]> = {
  Pending: [
    { status: 'Confirmed', label: 'Confirm', style: 'btn-primary' },
    { status: 'Cancelled', label: 'Cancel', style: 'btn-danger' },
  ],
  Confirmed: [
    { status: 'Shipped', label: 'Mark shipped', style: 'btn-primary' },
    { status: 'Cancelled', label: 'Cancel', style: 'btn-danger' },
  ],
  Shipped: [],
  Cancelled: [],
};

@Component({
  selector: 'app-admin-orders',
  imports: [
    CurrencyPipe,
    DatePipe,
    FormsModule,
    StatusBadgeComponent,
    OrderItemsComponent,
    PaginationComponent,
    LoadingComponent,
    EmptyStateComponent,
    ErrorAlertComponent,
  ],
  template: `
    <header class="page-header">
      <h1>Orders</h1>
      <label class="field inline">
        <span>Status</span>
        <select [ngModel]="status()" (ngModelChange)="filter($event)">
          <option value="">All</option>
          @for (s of statuses; track s) {
            <option [value]="s">{{ s }}</option>
          }
        </select>
      </label>
    </header>

    @if (newOrders() > 0) {
      <div class="alert alert-info">
        {{ newOrders() }} new order(s) placed.
        <button type="button" class="btn btn-small" (click)="load(1)">Refresh</button>
      </div>
    }

    @if (loading()) {
      <app-loading label="Loading orders…" />
    } @else if (error()) {
      <app-error-alert [message]="error()!" (retry)="load(page())" />
    } @else if (result()) {
      @let data = result()!;
      @if (data.items.length === 0) {
        <app-empty-state title="No orders" message="Nothing matches this filter." />
      } @else {
        <div class="order-list">
          @for (order of data.items; track order.id) {
            <article class="card order-card">
              <header class="order-card-header">
                <div>
                  <strong>#{{ order.id }} · {{ order.customerName }}</strong>
                  <small class="muted">{{ order.createdAt | date: 'medium' }}</small>
                </div>
                <app-status-badge [status]="order.status" />
              </header>

              <app-order-items [order]="order" />

              <footer class="order-card-footer">
                <span class="muted small">{{ order.total | currency: 'INR' }}</span>
                <div class="button-row">
                  @for (step of nextSteps(order.status); track step.status) {
                    <button
                      type="button"
                      class="btn btn-small {{ step.style }}"
                      [disabled]="busyId() === order.id"
                      (click)="changeStatus(order, step.status)"
                    >
                      {{ step.label }}
                    </button>
                  }
                </div>
              </footer>
            </article>
          }
        </div>
        <app-pagination [page]="data.page" [totalPages]="data.totalPages" [totalCount]="data.totalCount" (pageChange)="load($event)" />
      }
    }
  `,
})
export class AdminOrdersComponent {
  private readonly api = inject(OrderApiService);
  private readonly toast = inject(ToastService);
  private readonly hub = inject(OrderHubService);

  protected readonly statuses: OrderStatus[] = ['Pending', 'Confirmed', 'Shipped', 'Cancelled'];
  protected readonly status = signal<OrderStatus | ''>('');
  protected readonly result = signal<PagedResult<Order> | null>(null);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly page = signal(1);
  protected readonly busyId = signal<number | null>(null);
  protected readonly newOrders = signal(0);

  constructor() {
    this.load(1);
    const destroyRef = inject(DestroyRef);

    this.hub.orderPlaced$.pipe(takeUntilDestroyed(destroyRef)).subscribe(() => this.newOrders.update((n) => n + 1));
    this.hub.statusChanged$.pipe(takeUntilDestroyed(destroyRef)).subscribe((change) => {
      const current = this.result();
      if (!current) return;
      this.result.set({
        ...current,
        items: current.items.map((o) => (o.id === change.orderId ? { ...o, status: change.newStatus } : o)),
      });
    });
  }

  protected nextSteps(status: OrderStatus) {
    return NEXT_STEPS[status];
  }

  protected filter(status: OrderStatus | ''): void {
    this.status.set(status);
    this.load(1);
  }

  load(page: number): void {
    this.page.set(page);
    this.loading.set(true);
    this.error.set(null);
    this.newOrders.set(0);

    this.api.all(page, 10, this.status() || undefined).subscribe({
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

  changeStatus(order: Order, status: OrderStatus): void {
    if (status === 'Cancelled' && !confirm(`Cancel order #${order.id}? Stock will be restored.`)) return;

    this.busyId.set(order.id);
    this.api.changeStatus(order.id, status).subscribe({
      next: (updated) => {
        const current = this.result();
        if (current) {
          this.result.set({ ...current, items: current.items.map((o) => (o.id === updated.id ? updated : o)) });
        }
        this.busyId.set(null);
        this.toast.success(`Order #${order.id} is now ${updated.status}`);
      },
      error: (err) => {
        this.busyId.set(null);
        this.toast.error(toApiError(err).message);
        this.load(this.page());
      },
    });
  }
}
