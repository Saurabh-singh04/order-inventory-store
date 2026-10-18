import { Injectable, computed, effect, signal } from '@angular/core';
import { Product } from '../models/api.models';

export interface CartItem {
  productId: number;
  sku: string;
  name: string;
  price: number;
  quantity: number;
  /** Stock when the item was added - used to cap the quantity in the UI. */
  maxQuantity: number;
}

interface CartState {
  items: CartItem[];
  /**
   * Idempotency-Key for the next checkout. It stays the same until the cart
   * contents change, so retries / double clicks / a refresh mid-request all
   * send the same key and the API returns the same order.
   */
  checkoutKey: string;
}

export const CART_STORAGE_KEY = 'store.cart';

const newKey = (): string =>
  typeof crypto !== 'undefined' && 'randomUUID' in crypto
    ? crypto.randomUUID()
    : `${Date.now()}-${Math.random().toString(16).slice(2)}`;

@Injectable({ providedIn: 'root' })
export class CartService {
  private readonly state = signal<CartState>(this.load());

  readonly items = computed(() => this.state().items);
  readonly checkoutKey = computed(() => this.state().checkoutKey);
  readonly count = computed(() => this.items().reduce((sum, i) => sum + i.quantity, 0));
  readonly subTotal = computed(() =>
    Math.round(this.items().reduce((sum, i) => sum + i.price * i.quantity, 0) * 100) / 100,
  );
  readonly isEmpty = computed(() => this.items().length === 0);

  constructor() {
    effect(() => {
      try {
        localStorage.setItem(CART_STORAGE_KEY, JSON.stringify(this.state()));
      } catch {
        // ignore - cart still works for this tab
      }
    });

    // Keep several open tabs in sync.
    if (typeof window !== 'undefined') {
      window.addEventListener('storage', (event) => {
        if (event.key === CART_STORAGE_KEY) {
          this.state.set(this.load());
        }
      });
    }
  }

  quantityOf(productId: number): number {
    return this.items().find((i) => i.productId === productId)?.quantity ?? 0;
  }

  add(product: Product, quantity = 1): void {
    if (product.stockQuantity <= 0) return;

    this.mutate((items) => {
      const existing = items.find((i) => i.productId === product.id);
      if (existing) {
        return items.map((i) =>
          i.productId === product.id
            ? { ...i, maxQuantity: product.stockQuantity, quantity: Math.min(i.quantity + quantity, product.stockQuantity) }
            : i,
        );
      }
      return [
        ...items,
        {
          productId: product.id,
          sku: product.sku,
          name: product.name,
          price: product.price,
          maxQuantity: product.stockQuantity,
          quantity: Math.min(quantity, product.stockQuantity),
        },
      ];
    });
  }

  setQuantity(productId: number, quantity: number): void {
    if (quantity <= 0) {
      this.remove(productId);
      return;
    }
    this.mutate((items) =>
      items.map((i) => (i.productId === productId ? { ...i, quantity: Math.min(quantity, i.maxQuantity) } : i)),
    );
  }

  remove(productId: number): void {
    this.mutate((items) => items.filter((i) => i.productId !== productId));
  }

  clear(): void {
    this.state.set({ items: [], checkoutKey: newKey() });
  }

  /** Forces a fresh key, e.g. when the API says the old one was used for a different cart. */
  renewCheckoutKey(): void {
    this.state.update((s) => ({ ...s, checkoutKey: newKey() }));
  }

  private mutate(change: (items: CartItem[]) => CartItem[]): void {
    this.state.update((s) => {
      const items = change(s.items);
      const changed = JSON.stringify(items) !== JSON.stringify(s.items);
      return changed ? { items, checkoutKey: newKey() } : s;
    });
  }

  private load(): CartState {
    try {
      const raw = localStorage.getItem(CART_STORAGE_KEY);
      if (raw) {
        const parsed = JSON.parse(raw) as CartState;
        if (Array.isArray(parsed.items) && typeof parsed.checkoutKey === 'string') {
          return parsed;
        }
      }
    } catch {
      // corrupted value - start with an empty cart
    }
    return { items: [], checkoutKey: newKey() };
  }
}
