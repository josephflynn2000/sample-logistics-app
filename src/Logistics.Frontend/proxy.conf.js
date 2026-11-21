const { Agent } = require('node:http');

const devProxyAgent = new Agent({
  keepAlive: true,
  proxyEnv: {
    HTTP_PROXY: process.env.HTTP_PROXY ?? 'http://127.0.0.1:8000',
  },
});

module.exports = {
  '/api': {
    target: 'http://localhost:5045',
    secure: false,
    changeOrigin: true,
    pathRewrite: { '^/api': '' },
    agent: devProxyAgent,
    configure(proxy) {
      queueMicrotask(() => {
        proxy.removeAllListeners('error');
        proxy.on('error', (_err, _req, res) => {
          if (res && 'writeHead' in res && !res.headersSent && !res.writableEnded) {
            res.writeHead(502, { 'Content-Type': 'text/plain' }).end();
          }
        });
      });
    },
  },
};
