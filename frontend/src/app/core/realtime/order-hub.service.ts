import { Injectable, OnDestroy, effect, inject } from '@angular/core';
import { HubConnection, HubConnectionBuilder, HubConnectionState, LogLevel } from '@microsoft/signalr';
import { Subject } from 'rxjs';
import { AuthService } from '../auth/auth.service';
import { OrderPlacedEvent, OrderStatusChangedEvent } from '../models/api.models';

/**
 * Live order updates over SignalR. Connects when a user signs in and
 * disconnects on logout. Customers get their own orders, admins get all.
 */
@Injectable({ providedIn: 'root' })
export class OrderHubService implements OnDestroy {
  private readonly auth = inject(AuthService);
  private connection: HubConnection | null = null;

  readonly statusChanged$ = new Subject<OrderStatusChangedEvent>();
  readonly orderPlaced$ = new Subject<OrderPlacedEvent>();

  constructor() {
    effect(() => {
      if (this.auth.isLoggedIn()) {
        void this.start();
      } else {
        void this.stop();
      }
    });
  }

  private async start(): Promise<void> {
    if (this.connection) return;

    const connection = new HubConnectionBuilder()
      .withUrl('/hubs/orders', {
        // Called on every (re)connect; refreshes the JWT first if it is about to expire.
        accessTokenFactory: () => this.auth.freshAccessToken(),
      })
      .withAutomaticReconnect()
      .configureLogging(LogLevel.Warning)
      .build();

    connection.on('orderStatusChanged', (event: OrderStatusChangedEvent) => this.statusChanged$.next(event));
    connection.on('orderPlaced', (event: OrderPlacedEvent) => this.orderPlaced$.next(event));

    this.connection = connection;
    try {
      await connection.start();
    } catch (error) {
      // Live updates are a nice-to-have; the pages still work by reloading.
      console.warn('Live order updates are unavailable', error);
    }
  }

  private async stop(): Promise<void> {
    const connection = this.connection;
    this.connection = null;
    if (connection && connection.state !== HubConnectionState.Disconnected) {
      await connection.stop();
    }
  }

  ngOnDestroy(): void {
    void this.stop();
  }
}
