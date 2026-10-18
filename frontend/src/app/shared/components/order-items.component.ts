import { CurrencyPipe } from '@angular/common';
import { Component, input } from '@angular/core';
import { Order } from '../../core/models/api.models';

/** Line items + totals of one order, reused by the customer and admin order lists. */
@Component({
  selector: 'app-order-items',
  imports: [CurrencyPipe],
  template: `
    <ul class="order-lines">
      @for (item of order().items; track item.productId) {
        <li>
          <span>{{ item.productName }} <small class="muted">× {{ item.quantity }}</small></span>
          <span>{{ item.lineTotal | currency: 'INR' }}</span>
        </li>
      }
    </ul>
    <dl class="order-totals">
      <div><dt>Sub total</dt><dd>{{ order().subTotal | currency: 'INR' }}</dd></div>
      @if (order().discount > 0) {
        <div class="discount"><dt>Discount</dt><dd>− {{ order().discount | currency: 'INR' }}</dd></div>
      }
      <div class="grand"><dt>Total</dt><dd>{{ order().total | currency: 'INR' }}</dd></div>
    </dl>
  `,
})
export class OrderItemsComponent {
  readonly order = input.required<Order>();
}
