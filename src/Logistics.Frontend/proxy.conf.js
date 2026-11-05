const proxyConf = require('./proxy.conf.json');

const target = process.env.BACKEND_PORT
  ? `http://localhost:${process.env.BACKEND_PORT}`
  : proxyConf['/v1'].target;

module.exports = {
  '/v1': {
    ...proxyConf['/v1'],
    target,
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
