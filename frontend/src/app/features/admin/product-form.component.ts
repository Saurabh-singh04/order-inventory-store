import { Component, OnInit, inject, input, signal } from '@angular/core';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { ProductApiService } from '../../core/api/product-api.service';
import { toApiError } from '../../core/errors/api-error';
import { ErrorAlertComponent, LoadingComponent } from '../../shared/components/state-views.component';
import { ToastService } from '../../shared/toast/toast.service';

@Component({
  selector: 'app-product-form',
  imports: [ReactiveFormsModule, RouterLink, LoadingComponent, ErrorAlertComponent],
  template: `
    <header class="page-header">
      <h1>{{ isEdit() ? 'Edit product' : 'New product' }}</h1>
      <a routerLink="/admin/products" class="btn btn-link">← Back to products</a>
    </header>

    @if (loading()) {
      <app-loading label="Loading product…" />
    } @else if (loadError()) {
      <app-error-alert [message]="loadError()!" (retry)="loadProduct()" />
    } @else {
      <form class="card form-card" [formGroup]="form" (ngSubmit)="save()" novalidate>
        @if (saveError()) {
          <div class="alert alert-error" role="alert">
            {{ saveError() }}
            @if (conflict()) {
              <button type="button" class="btn btn-small" (click)="loadProduct()">Reload latest</button>
            }
          </div>
        }

        <div class="form-grid">
          <label class="field">
            <span>SKU</span>
            <input type="text" formControlName="sku" placeholder="e.g. ACC-4005" />
            @if (form.controls.sku.touched && form.controls.sku.invalid) {
              <small class="field-error">3–32 characters: letters, digits and "-".</small>
            }
          </label>

          <label class="field">
            <span>Name</span>
            <input type="text" formControlName="name" />
            @if (form.controls.name.touched && form.controls.name.invalid) {
              <small class="field-error">Name must be 2–200 characters.</small>
            }
          </label>

          <label class="field">
            <span>Price (₹)</span>
            <input type="number" step="0.01" min="0.01" formControlName="price" />
            @if (form.controls.price.touched && form.controls.price.invalid) {
              <small class="field-error">Price must be greater than 0.</small>
            }
          </label>

          <label class="field">
            <span>Stock quantity</span>
            <input type="number" step="1" min="0" formControlName="stockQuantity" />
            @if (form.controls.stockQuantity.touched && form.controls.stockQuantity.invalid) {
              <small class="field-error">Stock must be a whole number, 0 or more.</small>
            }
          </label>
        </div>

        <label class="checkbox">
          <input type="checkbox" formControlName="isActive" />
          <span>Active (visible in the catalogue)</span>
        </label>

        <div class="form-actions">
          <button type="submit" class="btn btn-primary" [disabled]="saving()">{{ saving() ? 'Saving…' : 'Save product' }}</button>
          <a routerLink="/admin/products" class="btn">Cancel</a>
        </div>
      </form>
    }
  `,
})
export class ProductFormComponent implements OnInit {
  /** Route param (withComponentInputBinding). Missing = create mode. */
  readonly id = input<string>();

  private readonly api = inject(ProductApiService);
  private readonly router = inject(Router);
  private readonly toast = inject(ToastService);

  protected readonly form = inject(FormBuilder).nonNullable.group({
    sku: ['', [Validators.required, Validators.minLength(3), Validators.maxLength(32), Validators.pattern(/^[A-Za-z0-9-]+$/)]],
    name: ['', [Validators.required, Validators.minLength(2), Validators.maxLength(200)]],
    price: [0, [Validators.required, Validators.min(0.01)]],
    stockQuantity: [0, [Validators.required, Validators.min(0), Validators.pattern(/^\d+$/)]],
    isActive: [true],
  });

  protected readonly loading = signal(false);
  protected readonly loadError = signal<string | null>(null);
  protected readonly saving = signal(false);
  protected readonly saveError = signal<string | null>(null);
  protected readonly conflict = signal(false);
  private rowVersion = '';

  protected isEdit(): boolean {
    return !!this.id() && this.id() !== 'new';
  }

  ngOnInit(): void {
    if (this.isEdit()) this.loadProduct();
  }

  loadProduct(): void {
    this.loading.set(true);
    this.loadError.set(null);
    this.saveError.set(null);
    this.conflict.set(false);

    this.api.get(Number(this.id())).subscribe({
      next: (p) => {
        this.rowVersion = p.rowVersion;
        this.form.setValue({ sku: p.sku, name: p.name, price: p.price, stockQuantity: p.stockQuantity, isActive: p.isActive });
        this.loading.set(false);
      },
      error: (err) => {
        this.loadError.set(toApiError(err).message);
        this.loading.set(false);
      },
    });
  }

  save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.saving.set(true);
    this.saveError.set(null);
    this.conflict.set(false);

    const value = this.form.getRawValue();
    const request$ = this.isEdit()
      ? this.api.update(Number(this.id()), { ...value, rowVersion: this.rowVersion })
      : this.api.create(value);

    request$.subscribe({
      next: (product) => {
        this.toast.success(`Saved ${product.name}`);
        this.router.navigate(['/admin/products']);
      },
      error: (err) => {
        const apiError = toApiError(err);
        // 409 on edit = someone (or an order) changed the product since it was loaded.
        this.conflict.set(apiError.status === 409 && apiError.code === 'concurrency');
        this.saveError.set(apiError.message);
        this.saving.set(false);
      },
    });
  }
}
