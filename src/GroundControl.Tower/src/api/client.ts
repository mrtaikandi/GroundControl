import { env } from '@/lib/env';
import type { operations } from './types';

type JsonContent<Response> = Response extends { content: { 'application/json': infer Content } } ? Content : undefined;

type OperationResponses<OperationName extends keyof operations> = operations[OperationName] extends { responses: infer Responses } ? Responses : never;

export type ApiResponse<OperationName extends keyof operations> = OperationResponses<OperationName> extends infer Responses
  ? 200 extends keyof Responses
    ? JsonContent<Responses[200]>
    : 201 extends keyof Responses
      ? JsonContent<Responses[201]>
      : 204 extends keyof Responses
        ? undefined
        : unknown
  : unknown;

export type ApiRequestBody<OperationName extends keyof operations> = operations[OperationName] extends { requestBody: { content: { 'application/json': infer Body } } } ? Body : never;

export type ApiQuery<OperationName extends keyof operations> = operations[OperationName] extends { parameters: { query?: infer Query } } ? Query : never;

type AccessTokenProvider = () => string | undefined;

interface ApiFetchOptions extends Omit<RequestInit, 'body'> {
  body?: unknown;
  expectType?: 'json' | 'text' | 'empty';
  query?: object;
  version?: string;
}

let accessTokenProvider: AccessTokenProvider | undefined;

export class ApiError extends Error {
  constructor(
    public readonly status: number,
    public readonly body: unknown,
    public readonly currentVersion?: string,
  ) {
    super(`GroundControl API request failed with status ${status}`);
    this.name = 'ApiError';
  }
}

export function setAccessTokenProvider(provider: AccessTokenProvider | undefined) {
  accessTokenProvider = provider;
}

export function getAccessToken() {
  return accessTokenProvider?.();
}

export async function apiFetch<T>(path: string, options: ApiFetchOptions = {}): Promise<T> {
  const { body, expectType = 'json', headers: optionHeaders, query, version, ...requestOptions } = options;
  const method = requestOptions.method?.toUpperCase() ?? 'GET';
  const headers = new Headers(optionHeaders);
  const token = accessTokenProvider?.();

  headers.set('api-version', '1.0');

  if (token) {
    headers.set('Authorization', `Bearer ${token}`);
  }

  if (version) {
    headers.set('If-Match', quoteVersion(version));
  }

  const requestBody = serializeBody(body);

  if (requestBody !== undefined && shouldSendJsonContentType(method, headers, body)) {
    headers.set('Content-Type', 'application/json');
  }

  const response = await fetch(buildUrl(path, query), {
    ...requestOptions,
    method,
    headers,
    body: requestBody,
  });

  if (!response.ok) {
    const errorBody = await readResponseBody(response);
    const currentVersion = response.status === 412 ? response.headers.get('ETag') ?? undefined : undefined;

    throw new ApiError(response.status, errorBody, currentVersion);
  }

  if (expectType === 'empty' || response.status === 204) {
    return undefined as T;
  }

  if (expectType === 'text') {
    return await response.text() as T;
  }

  return await response.json() as T;
}

function buildUrl(path: string, query?: object): string {
  const baseUrl = env.apiBaseUrl.replace(/\/$/, '');
  const url = new URL(`${baseUrl}${path.startsWith('/') ? path : `/${path}`}`);

  if (query) {
    for (const [key, value] of Object.entries(query as Record<string, unknown>)) {
      if (value === undefined || value === null) {
        continue;
      }

      url.searchParams.set(key, value instanceof Date ? value.toISOString() : String(value));
    }
  }

  return url.toString();
}

function quoteVersion(version: string): string {
  return version.startsWith('"') && version.endsWith('"') ? version : `"${version}"`;
}

function serializeBody(body: unknown): BodyInit | undefined {
  if (body === undefined || body === null) {
    return undefined;
  }

  if (typeof body === 'string' || body instanceof Blob || body instanceof FormData || body instanceof URLSearchParams || body instanceof ArrayBuffer) {
    return body;
  }

  return JSON.stringify(body);
}

function shouldSendJsonContentType(method: string, headers: Headers, body: unknown): boolean {
  return ['POST', 'PUT', 'PATCH'].includes(method) && !headers.has('Content-Type') && !(body instanceof FormData);
}

async function readResponseBody(response: Response): Promise<unknown> {
  if (response.status === 204) {
    return undefined;
  }

  const contentType = response.headers.get('Content-Type') ?? '';

  if (contentType.includes('application/json') || contentType.includes('application/problem+json')) {
    return await response.json();
  }

  return await response.text();
}