declare const __ENV: Record<string, string | undefined>;

declare module 'k6' {
  export type Checkers<T> = Record<string, (val: T) => boolean>;
  export function check<T>(val: T, sets: Checkers<T>, tags?: Record<string, string>): boolean;
  export function sleep(t: number): void;
}

declare module 'k6/http' {
  export interface Params {
    headers?: Record<string, string>;
    tags?: Record<string, string>;
    timeout?: string | number;
  }

  export interface Response {
    status: number;
    body: string | null;
    headers: Record<string, string | undefined>;
    json<T = unknown>(): T;
  }

  export function get(url: string, params?: Params): Response;
  export function post(url: string, body?: string | null, params?: Params): Response;
  export function put(url: string, body?: string | null, params?: Params): Response;
  export function patch(url: string, body?: string | null, params?: Params): Response;
  export function del(url: string, body?: string | null, params?: Params): Response;

  const http: {
    get: typeof get;
    post: typeof post;
    put: typeof put;
    patch: typeof patch;
    del: typeof del;
  };
  export default http;
}

declare module 'k6/execution' {
  export const vu: {
    idInTest: number;
    idInInstance: number;
    iterationInInstance: number;
    iterationInScenario: number;
  };
  const exec: {
    vu: typeof vu;
  };
  export default exec;
}
