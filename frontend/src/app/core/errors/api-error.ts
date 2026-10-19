import { HttpErrorResponse } from '@angular/common/http';
import { ProblemDetails } from '../models/api.models';

export interface ApiError {
  status: number;
  message: string;
  code?: string;
  fieldErrors?: Record<string, string[]>;
}

/**
 * Converts any HTTP failure into a message that can be shown to the user.
 * 409 and 429 get dedicated wording because the user can act on them.
 */
export function toApiError(error: unknown, context: 'login' | 'order' | 'default' = 'default'): ApiError {
  if (!(error instanceof HttpErrorResponse)) {
    return { status: 0, message: 'Something went wrong. Please try again.' };
  }

  const problem = (typeof error.error === 'object' && error.error !== null ? error.error : {}) as ProblemDetails;
  const detail = problem.detail;

  switch (error.status) {
    case 0:
      return { status: 0, message: 'Cannot reach the server. Check your connection and try again.' };

    case 400:
      return {
        status: 400,
        message: detail ?? firstFieldError(problem.errors) ?? 'Please check the highlighted fields.',
        fieldErrors: problem.errors,
      };

    case 401:
      return {
        status: 401,
        message: context === 'login' ? 'Invalid email or password.' : 'Your session has expired. Please sign in again.',
      };

    case 403:
      return { status: 403, message: detail ?? 'You do not have permission to do that.' };

    case 404:
      return { status: 404, message: detail ?? 'The item you are looking for no longer exists.' };

    case 409:
      if (problem.code === 'out_of_stock' || (context === 'order' && !problem.code)) {
        return {
          status: 409,
          code: problem.code,
          message: `Sorry, some items are out of stock. ${detail ?? ''}`.trim(),
        };
      }
      return { status: 409, code: problem.code, message: detail ?? 'This item was changed by someone else. Reload and try again.' };

    case 422:
      return { status: 422, code: problem.code, message: detail ?? 'The request could not be processed.' };

    case 429: {
      const seconds = problem.retryAfterSeconds ?? Number(error.headers?.get('Retry-After') ?? 60);
      return {
        status: 429,
        message:
          context === 'login'
            ? `Too many login attempts. Please wait ${seconds} seconds before trying again.`
            : `Too many requests. Please wait ${seconds} seconds.`,
      };
    }

    default:
      return { status: error.status, message: 'The server had a problem handling the request. Please try again.' };
  }
}

function firstFieldError(errors?: Record<string, string[]>): string | undefined {
  if (!errors) return undefined;
  const first = Object.values(errors).find((messages) => messages.length > 0);
  return first?.[0];
}
