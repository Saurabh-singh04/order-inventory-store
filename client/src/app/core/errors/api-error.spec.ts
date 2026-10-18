import { HttpErrorResponse, HttpHeaders } from '@angular/common/http';
import { toApiError } from './api-error';

describe('toApiError', () => {
  it('explains a 409 on checkout as out of stock', () => {
    const error = new HttpErrorResponse({
      status: 409,
      error: { status: 409, code: 'out_of_stock', detail: 'Not enough stock for: Apple iPad.' },
    });

    const result = toApiError(error, 'order');

    expect(result.status).toBe(409);
    expect(result.message).toContain('out of stock');
    expect(result.message).toContain('Apple iPad');
  });

  it('tells the user how long to wait after too many logins (429)', () => {
    const error = new HttpErrorResponse({
      status: 429,
      headers: new HttpHeaders({ 'Retry-After': '42' }),
      error: { status: 429 },
    });

    expect(toApiError(error, 'login').message).toBe('Too many login attempts. Please wait 42 seconds before trying again.');
  });

  it('uses the first validation error when there is no detail', () => {
    const error = new HttpErrorResponse({
      status: 400,
      error: { errors: { Email: ['The Email field is not a valid e-mail address.'] } },
    });

    expect(toApiError(error).message).toBe('The Email field is not a valid e-mail address.');
  });

  it('handles the server being unreachable', () => {
    expect(toApiError(new HttpErrorResponse({ status: 0 })).message).toContain('Cannot reach the server');
  });
});
