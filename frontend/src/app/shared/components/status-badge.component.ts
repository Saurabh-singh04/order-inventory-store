import { Component, input } from '@angular/core';
import { OrderStatus } from '../../core/models/api.models';

@Component({
  selector: 'app-status-badge',
  template: `<span class="badge badge-{{ status().toLowerCase() }}">{{ status() }}</span>`,
})
export class StatusBadgeComponent {
  readonly status = input.required<OrderStatus>();
}
