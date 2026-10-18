import { TestBed } from '@angular/core/testing';
import { Product } from '../models/api.models';
import { CART_STORAGE_KEY, CartService } from './cart.service';

const product = (id: number, stock: number, price = 100): Product => ({
  id,
  sku: `SKU-${id}`,
  name: `Product ${id}`,
  price,
  stockQuantity: stock,
  isActive: true,
  createdAt: '2026-01-01T00:00:00Z',
  rowVersion: 'AAAAAAAAB9E=',
});

describe('CartService', () => {
  beforeEach(() => localStorage.clear());
  afterEach(() => localStorage.clear());

  const create = () => TestBed.inject(CartService);

  it('merges repeated adds and never exceeds the stock', () => {
    const cart = create();

    cart.add(product(1, 3));
    cart.add(product(1, 3));
    cart.add(product(1, 3));
    cart.add(product(1, 3));

    expect(cart.items().length).toBe(1);
    expect(cart.quantityOf(1)).toBe(3);
  });

  it('ignores out-of-stock products and computes the sub total', () => {
    const cart = create();

    cart.add(product(1, 0));
    cart.add(product(2, 5, 249.5), 2);

    expect(cart.count()).toBe(2);
    expect(cart.subTotal()).toBe(499);
  });

  it('keeps the same idempotency key until the cart changes', () => {
    const cart = create();
    cart.add(product(1, 5));
    const key = cart.checkoutKey();

    // nothing changed -> same key (a retried checkout must reuse it)
    cart.setQuantity(1, 1);
    expect(cart.checkoutKey()).toBe(key);

    cart.setQuantity(1, 2);
    expect(cart.checkoutKey()).not.toBe(key);
  });

  it('survives a page refresh via localStorage', () => {
    const cart = create();
    cart.add(product(7, 4), 2);
    TestBed.flushEffects();

    const stored = JSON.parse(localStorage.getItem(CART_STORAGE_KEY)!);
    expect(stored.items[0].productId).toBe(7);

    // A fresh injector simulates a reload.
    TestBed.resetTestingModule();
    const reloaded = TestBed.inject(CartService);
    expect(reloaded.quantityOf(7)).toBe(2);
    expect(reloaded.checkoutKey()).toBe(stored.checkoutKey);
  });
});
