import { CurrencyPipe } from '@angular/common';
import { Component, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { OrderApiService } from '../../core/api/order-api.service';
import { AuthService } from '../../core/auth/auth.service';
import { CartService } from '../../core/cart/cart.service';
import { toApiError } from '../../core/errors/api-error';
import { EmptyStateComponent } from '../../shared/components/state-views.component';
import { ToastService } from '../../shared/toast/toast.service';

@Component({
  selector: 'app-cart',
  imports: [CurrencyPipe, RouterLink, EmptyStateComponent],
  template: `
    <header class="page-header">
      <h1>Your cart</h1>
    </header>

    @if (cart.isEmpty()) {
      <app-empty-state title="Your cart is empty" message="Browse the catalogue and add a few products.">
        <a routerLink="/products" class="btn btn-primary">Browse products</a>
      </app-empty-state>
    } @else {
      @if (error()) {
        <div class="alert" [class.alert-warning]="outOfStock()" [class.alert-error]="!outOfStock()" role="alert">
          {{ error() }}
          @if (outOfStock()) {
            <a routerLink="/products" [queryParams]="{ inStock: 'true' }">Check current stock</a>
          }
        </div>
      }

      <div class="cart-layout">
        <ul class="card cart-lines">
          @for (item of cart.items(); track item.productId) {
            <li class="cart-line">
              <div class="cart-line-info">
                <strong>{{ item.name }}</strong>
                <small class="muted">{{ item.sku }} · {{ item.price | currency: 'INR' }} each</small>
              </div>
              <div class="qty" role="group" [attr.aria-label]="'Quantity for ' + item.name">
                <button type="button" class="btn btn-small" [disabled]="placing()" (click)="cart.setQuantity(item.productId, item.quantity - 1)">−</button>
                <span>{{ item.quantity }}</span>
                <button
                  type="button"
                  class="btn btn-small"
                  [disabled]="placing() || item.quantity >= item.maxQuantity"
                  (click)="cart.setQuantity(item.productId, item.quantity + 1)"
                >+</button>
              </div>
              <span class="cart-line-total">{{ item.price * item.quantity | currency: 'INR' }}</span>
              <button type="button" class="btn btn-link danger" [disabled]="placing()" (click)="cart.remove(item.productId)">Remove</button>
            </li>
          }
        </ul>

        <aside class="card cart-summary">
          <dl class="order-totals">
            <div><dt>Items</dt><dd>{{ cart.count() }}</dd></div>
            <div class="grand"><dt>Sub total</dt><dd>{{ cart.subTotal() | currency: 'INR' }}</dd></div>
          </dl>
          <p class="muted small">
            Discounts (5% above ₹10,000 and 10% on every 5th order) are calculated when the order is placed.
          </p>

          @if (auth.isCustomer()) {
            <button type="button" class="btn btn-primary btn-block" [disabled]="placing()" (click)="placeOrder()">
              {{ placing() ? 'Placing order…' : 'Place order' }}
            </button>
          } @else if (auth.isAdmin()) {
            <p class="muted small">Admins can't place orders. Sign in with a customer account.</p>
          } @else {
            <a routerLink="/login" [queryParams]="{ returnUrl: '/cart' }" class="btn btn-primary btn-block">Sign in to checkout</a>
          }
        </aside>
      </div>
    }
  `,
})
export class CartComponent {
  protected readonly cart = inject(CartService);
  protected readonly auth = inject(AuthService);
  private readonly orders = inject(OrderApiService);
  private readonly router = inject(Router);
  private readonly toast = inject(ToastService);

  protected readonly placing = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly outOfStock = signal(false);

  placeOrder(): void {
    // Guard against a double click firing before the button re-renders as disabled.
    if (this.placing()) return;

    this.placing.set(true);
    this.error.set(null);

    const request = {
      items: this.cart.items().map((i) => ({ productId: i.productId, quantity: i.quantity })),
    };

    // Same cart = same key, so even a request that slips through twice returns one order.
    this.orders
      .place(request, this.cart.checkoutKey())
      .pipe(finalize(() => this.placing.set(false)))
      .subscribe({
        next: (response) => {
          const order = response.body!;
          this.cart.clear();
          this.toast.success(`Order #${order.id} placed. Total ₹${order.total.toFixed(2)}`);
          this.router.navigate(['/orders']);
        },
        error: (err) => {
          const apiError = toApiError(err, 'order');
          this.outOfStock.set(apiError.status === 409);
          this.error.set(apiError.message);
          if (apiError.status === 422) {
            this.cart.renewCheckoutKey();
          }
        },
      });
  }
}
