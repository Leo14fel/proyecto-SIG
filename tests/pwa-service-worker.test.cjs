const test = require('node:test');
const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');

const origin = 'https://sig.example';
const source = fs.readFileSync(path.join(__dirname, '..', 'src', 'VisorDatosSIG.Web', 'wwwroot', 'service-worker.js'), 'utf8');

function reply(value, directive = 'public') {
  return {
    ok: true, type: 'basic',
    headers: { get: name => name.toLowerCase() === 'cache-control' ? directive : null },
    text: async () => value,
    clone: () => reply(value, directive)
  };
}

function setup() {
  const handlers = {};
  const stores = new Map();
  const network = { online: true, directive: 'public', calls: [] };
  const key = request => new URL(typeof request === 'string' ? request : request.url, origin).href;
  const caches = {
    open: async name => {
      if (!stores.has(name)) stores.set(name, new Map());
      const store = stores.get(name);
      return {
        addAll: async paths => paths.forEach(p => store.set(key(p), reply('precache:' + p))),
        put: async (req, res) => { store.set(key(req), res); },
        match: async req => store.get(key(req))
      };
    },
    keys: async () => [...stores.keys()],
    delete: async name => stores.delete(name),
    match: async req => {
      for (const store of stores.values()) if (store.has(key(req))) return store.get(key(req));
    }
  };
  const self = {
    location: { origin }, skipWaiting: async () => {},
    clients: { claim: async () => {} },
    addEventListener: (name, fn) => { handlers[name] = fn; }
  };
  vm.runInNewContext(source, {
    self, caches, URL,
    fetch: async req => {
      network.calls.push(key(req));
      if (!network.online) throw new Error('offline');
      return reply('network:' + key(req), network.directive);
    }
  });

  async function dispatch(type, options = {}) {
    const waiting = [];
    let response;
    handlers[type]({
      ...options,
      waitUntil: promise => waiting.push(Promise.resolve(promise)),
      respondWith: promise => { response = Promise.resolve(promise); }
    });
    const result = response ? await response : undefined;
    await Promise.all(waiting);
    return result;
  }
  const request = (url, options = {}) => dispatch('fetch', {
    request: { url: key(url), method: 'GET', mode: 'cors', cache: 'default', ...options }
  });
  return { dispatch, request, caches, network };
}

test('excluye rutas privadas y archivos ajenos a directorios públicos', async () => {
  const sw = setup();
  await sw.dispatch('install');
  for (const url of ['/api/capas', '/Operaciones/Evidencia/1', '/Registros/Detalle/1',
    '/Cuenta/Login', '/private/foto.png', '/reportes/foto.png', '/js/privado.json']) {
    assert.equal(await sw.request(url), undefined, url);
    assert.equal(await sw.caches.match(url), undefined, url);
  }
});

test('cachea archivos públicos y los sirve sin red', async () => {
  const sw = setup();
  await sw.dispatch('install');
  const url = '/css/nuevo.css?v=1';
  assert.match(await (await sw.request(url)).text(), /network:/);
  sw.network.online = false;
  assert.match(await (await sw.request(url)).text(), /network:/);
});

test('no almacena respuestas private o no-store', async () => {
  for (const directive of ['private, max-age=120', 'no-store', 'PUBLIC, NO-STORE']) {
    const sw = setup();
    sw.network.directive = directive;
    await sw.request('/css/privado.css');
    assert.equal(await sw.caches.match('/css/privado.css'), undefined, directive);
  }
});

test('una navegación sin conexión recibe la página offline', async () => {
  const sw = setup();
  await sw.dispatch('install');
  sw.network.online = false;
  const res = await sw.request('/Operaciones/Campo', { mode: 'navigate' });
  assert.equal(await res.text(), 'precache:/offline.html');
});

test('elimina únicamente versiones antiguas de su propia caché', async () => {
  const sw = setup();
  await sw.caches.open('visordatos-static-v4');
  await sw.caches.open('otra-app-v1');
  await sw.dispatch('install');
  await sw.dispatch('activate');
  assert.deepEqual((await sw.caches.keys()).sort(), ['otra-app-v1', 'visordatos-static-v6']);
});

test('no intercepta recursos de dominios externos', async () => {
  const sw = setup();
  assert.equal(await sw.request('https://otro.example/css/app.css'), undefined);
  assert.equal(sw.network.calls.length, 0);
});

test('no lee archivos desde la caché de otra aplicación', async () => {
  const sw = setup();
  const other = await sw.caches.open('otra-app-v1');
  await other.put('/css/extra.css', reply('otra app'));
  await sw.dispatch('install');
  assert.match(await (await sw.request('/css/extra.css')).text(), /network:/);
});
