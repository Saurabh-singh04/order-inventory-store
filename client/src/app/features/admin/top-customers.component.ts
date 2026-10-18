import { CurrencyPipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ReportApiService } from '../../core/api/report-api.service';
import { toApiError } from '../../core/errors/api-error';
import { TopCustomer } from '../../core/models/api.models';
import { EmptyStateComponent, ErrorAlertComponent, LoadingComponent } from '../../shared/components/state-views.component';

@Component({
  selector: 'app-top-customers',
  imports: [CurrencyPipe, FormsModule, LoadingComponent, EmptyStateComponent, ErrorAlertComponent],
  template: `
    <header class="page-header">
      <h1>Top customers</h1>
      <label class="field inline">
        <span>Period</span>
        <select [ngModel]="days()" (ngModelChange)="changeDays($event)">
          @for (d of periods; track d) {
            <option [ngValue]="d">Last {{ d }} days</option>
          }
        </select>
      </label>
    </header>
    <p class="muted small">Spend on Confirmed and Shipped orders.</p>

    @if (loading()) {
      <app-loading label="Building report…" />
    } @else if (error()) {
      <app-error-alert [message]="error()!" (retry)="load()" />
    } @else if (rows().length === 0) {
      <app-empty-state title="No sales in this period" message="Confirm or ship some orders to see the ranking." />
    } @else {
      <div class="card chart" role="img" aria-label="Bar chart of customer spend">
        @for (row of rows(); track row.customerId) {
          <div class="bar-row">
            <span class="bar-label">{{ row.fullName }}</span>
            <div class="bar-track">
              <div class="bar" [style.width.%]="(row.totalSpend / maxSpend()) * 100"></div>
            </div>
            <span class="bar-value">{{ row.totalSpend | currency: 'INR' : 'symbol' : '1.0-0' }}</span>
          </div>
        }
      </div>

      <div class="table-wrap card">
        <table class="table">
          <thead>
            <tr>
              <th>#</th>
              <th>Customer</th>
              <th>Email</th>
              <th class="num">Orders</th>
              <th class="num">Total spend</th>
            </tr>
          </thead>
          <tbody>
            @for (row of rows(); track row.customerId; let i = $index) {
              <tr>
                <td data-label="Rank">{{ i + 1 }}</td>
                <td data-label="Customer">{{ row.fullName }}</td>
                <td data-label="Email">{{ row.email }}</td>
                <td data-label="Orders" class="num">{{ row.orderCount }}</td>
                <td data-label="Total spend" class="num">{{ row.totalSpend | currency: 'INR' }}</td>
              </tr>
            }
          </tbody>
        </table>
      </div>
    }
  `,
})
export class TopCustomersComponent {
  private readonly api = inject(ReportApiService);

  protected readonly periods = [7, 30, 90, 365];
  protected readonly days = signal(30);
  protected readonly rows = signal<TopCustomer[]>([]);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);
  protected readonly maxSpend = computed(() => Math.max(1, ...this.rows().map((r) => r.totalSpend)));

  constructor() {
    this.load();
  }

  protected changeDays(days: number): void {
    this.days.set(days);
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.error.set(null);
    this.api.topCustomers(this.days()).subscribe({
      next: (rows) => {
        this.rows.set(rows);
        this.loading.set(false);
      },
      error: (err) => {
        this.error.set(toApiError(err).message);
        this.loading.set(false);
      },
    });
  }
}
