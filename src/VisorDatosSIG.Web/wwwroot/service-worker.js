const STATIC_CACHE = 'visordatos-static-v6';
const OFFLINE_URL = '/offline.html';
const STATIC_ASSETS = [
  OFFLINE_URL, '/css/site.css', '/css/interface.css', '/js/site.js', '/js/geographic-search.js', '/js/operations.js', '/js/perspective.js', '/icons/ui.svg',
  '/lib/bootstrap/dist/css/bootstrap.min.css', '/lib/bootstrap/dist/js/bootstrap.bundle.min.js',
  '/icons/visordatos-192.png', '/icons/visordatos-512.png', '/icons/visordatos-512-maskable.png'
];

self.addEventListener('install', event => {
  event.waitUntil(caches.open(STATIC_CACHE).then(cache => cache.addAll(STATIC_ASSETS)).then(() => self.skipWaiting()));
});
self.addEventListener('activate', event => {
  event.waitUntil(caches.keys().then(keys => Promise.all(keys.filter(key => key.startsWith(CACHE_PREFIX) && key !== STATIC_CACHE).map(key => caches.delete(key)))).then(() => self.clients.claim()));
});
function isPrivatePath(pathname) {
  if (/^\/(operaciones|registros)(\/|$)/i.test(pathname)) return true;
  return pathname === '/Cuenta' || pathname.startsWith('/Cuenta/') ||
    pathname === '/Administracion' || pathname.startsWith('/Administracion/') ||
    pathname === '/api' || pathname.startsWith('/api/') ||
    pathname === '/Visor' || pathname.startsWith('/Visor/');
}
const PUBLIC_STATIC_PATH = /^\/(?:css\/.+\.css|js\/.+\.js|icons\/.+\.(?:svg|png|webp|ico)|lib\/.+\.(?:css|js|woff2?|ttf))$/i;
function mayStore(response) {
  return response.ok && response.type === 'basic' && !/(?:^|,)\s*(?:private|no-store)\b/i.test(response.headers.get('Cache-Control') || '');
}
function isCacheableStatic(request, url) {
  if (url.origin !== self.location.origin || isPrivatePath(url.pathname)) return false;
  return PUBLIC_STATIC_PATH.test(url.pathname) && request.cache !== 'no-store';
}
self.addEventListener('fetch', event => {
  const request = event.request;
  if (request.method !== 'GET') return;
  const url = new URL(request.url);
  if (request.mode === 'navigate') {
    event.respondWith(fetch(request).catch(() => caches.match(OFFLINE_URL)));
    return;
  }
  if (!isCacheableStatic(request, url)) return;
  event.respondWith(caches.match(request).then(cached => {
    const network = fetch(request).then(response => {
      if (mayStore(response)) caches.open(STATIC_CACHE).then(cache => cache.put(request, response.clone()));
      return response;
    });
    return cached || network;
  }));
});
