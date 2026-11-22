import {
  AngularNodeAppEngine,
  createNodeRequestHandler,
  isMainModule,
  writeResponseToNodeResponse,
} from '@angular/ssr/node';
import { RedisStore } from 'connect-redis';
import express from 'express';
import session from 'express-session';
import { createProxyMiddleware } from 'http-proxy-middleware';
import { join } from 'node:path';
import { createClient } from 'redis';

const browserDistFolder = join(import.meta.dirname, '../browser');

const app = express();
const angularApp = new AngularNodeAppEngine();

/**
 * Serve static files from /browser
 */
app.use(
  express.static(browserDistFolder, {
    maxAge: '1y',
    index: false,
    redirect: false,
  }),
);

let store: session.Store | undefined;
const sessionCacheUri = process.env['SESSION_CACHE_URI'];
if (process.env['NODE_ENV'] !== 'development' && sessionCacheUri) {
  const redisClient = createClient({
    url: sessionCacheUri,
    disableOfflineQueue: true,
    socket: {
      connectTimeout: 2_000,
      reconnectStrategy: (retries) => Math.min(retries * 100, 2_000),
    },
  });
  redisClient.on('error', (err) => console.error('[Redis]', err));
  redisClient.connect().catch((err) => console.error('[Redis]', err));
  store = new RedisStore({ client: redisClient, prefix: 'logistics:sess:' });
}

app.use(
  session({
    name: 'logistics.sid',
    secret: process.env['SESSION_SECRET'] ?? 'logistics-default-session-secret',
    store,
    resave: false,
    saveUninitialized: false,
    proxy: true,
    cookie: {
      httpOnly: true,
      sameSite: 'lax',
      secure: 'auto',
    },
  }),
);

app.use(
  '/api',
  createProxyMiddleware({
    target: process.env['BACKEND_HTTP'] ?? 'http://localhost:5045',
    changeOrigin: true,
  }),
);

/**
 * Handle all other requests by rendering the Angular application.
 */
app.use((req, res, next) => {
  angularApp
    .handle(req)
    .then((response) => (response ? writeResponseToNodeResponse(response, res) : next()))
    .catch(next);
});

/**
 * Start the server if this module is the main entry point, or it is ran via PM2.
 * The server listens on the port defined by the `PORT` environment variable, or defaults to 4000.
 */
if (isMainModule(import.meta.url) || process.env['pm_id']) {
  const port = process.env['PORT'] || 4000;
  app.listen(port, (error) => {
    if (error) {
      throw error;
    }

    console.log(`Node Express server listening on http://localhost:${port}`);
  });
}

/**
 * Request handler used by the Angular CLI (for dev-server and during build) or Firebase Cloud Functions.
 */
export const reqHandler = createNodeRequestHandler(app);
