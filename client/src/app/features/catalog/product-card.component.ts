import { CurrencyPipe } from '@angular/common';
import { Component, input, output } from '@angular/core';
import { Product } from '../../core/models/api.models';

@Component({
  selector: 'app-product-card',
  imports: [CurrencyPipe],
  template: `
    <article class="card product-card">
      <div class="product-thumb" aria-hidden="true">{{ initials() }}</div>
      <div class="product-body">
        <small class="muted">{{ product().sku }}</small>
        <h3>{{ product().name }}</h3>
        <p class="price">{{ product().price | currency: 'INR' }}</p>
        @if (product().stockQuantity > 0) {
          <p class="stock in">{{ product().stockQuantity }} in stock</p>
        } @else {
          <p class="stock out">Out of stock</p>
        }
      </div>
      @if (canBuy()) {
        <button
          type="button"
          class="btn btn-primary btn-block"
          [disabled]="product().stockQuantity === 0 || inCart() >= product().stockQuantity"
          (click)="addToCart.emit(product())"
        >
          {{ inCart() > 0 ? 'Add another (' + inCart() + ' in cart)' : 'Add to cart' }}
        </button>
      }
    </article>
  `,
})
export class ProductCardComponent {
  readonly product = input.required<Product>();
  readonly inCart = input(0);
  readonly canBuy = input(true);
  readonly addToCart = output<Product>();

  protected initials(): string {
    return this.product()
      .name.split(' ')
      .slice(0, 2)
      .map((w) => w[0])
      .join('')
      .toUpperCase();
  }
}
