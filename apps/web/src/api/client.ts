import type { ProblemDetails } from '../types/api';
import { clearSession, getSession } from './session';
/**
 * Where the API lives. In development this stays '/api' and the Vite dev server proxies it
 * to the backend; set VITE_API_BASE_URL to point a build somewhere else. See .env.example.
 */
const baseUrl = import.meta.env.VITE_API_BASE_URL ?? '/api';

/** A failed HTTP call, carrying the problem document the API returned. */
export class ApiError extends Error {
  readonly status: number;

  readonly problem?: ProblemDetails;

  constructor(status: number, message: string, problem?: ProblemDetails) {
    super(message);
    this.name = 'ApiError';
    this.status = status;
    this.problem = problem;
  }

  /** Field name to messages, when the failure was a validation failure. */
  get fieldErrors(): Record<string, string[]> {
    return this.problem?.errors ?? {};
  }
}

/**
 * The single place that talks to the API. Returns parsed JSON, or throws an
 * {@link ApiError} that callers can show to the user.
 */
interface RequestOptions {
  /** Send no token, and do not treat a 401 as an expired session (used by login). */
  anonymous?: boolean;
}

/**
 * The single place that talks to the API. Attaches the token, returns parsed JSON, or throws an
 * {@link ApiError}. A 401 on a signed-in call ends the session, which sends the user to /login.
 */
export async function request<T>(
  path: string,
  init?: RequestInit,
  options: RequestOptions = {},
): Promise<T> {
  const token = options.anonymous ? undefined : getSession()?.token;

  const response = await fetch(`${baseUrl}${path}`, {
    ...init,
    headers: {
      'Content-Type': 'application/json',
      ...(token ? { Authorization: `Bearer ${token}` } : {}),
      ...(init?.headers ?? {}),
    },
  });

  if (response.status === 401 && !options.anonymous) {
    clearSession();
  }

  if (!response.ok) {
    const problem = await readProblem(response);

    const message =
      response.status === 401 && !options.anonymous
        ? 'Your session has expired. Please sign in again.'
        : (problem?.detail ?? problem?.title ?? response.statusText);

    throw new ApiError(response.status, message, problem);
  }

  if (response.status === 204) {
    return undefined as T;
  }

  return (await response.json()) as T;
}

/** Turns anything thrown by a fetch into a message worth showing a user. */
export function toErrorMessage(error: unknown, fallback: string): string {
  if (error instanceof ApiError) {
    return error.message;
  }

  if (error instanceof Error) {
    return `${fallback} (${error.message})`;
  }

  return fallback;
}

async function readProblem(response: Response): Promise<ProblemDetails | undefined> {
  try {
    return (await response.json()) as ProblemDetails;
  } catch {
    // Not every failure has a JSON body - a dead server certainly will not.
    return undefined;
  }
}
