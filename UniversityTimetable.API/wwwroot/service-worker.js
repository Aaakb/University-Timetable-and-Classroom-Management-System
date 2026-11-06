const CACHE = 'university-timetable-shell-v49-alternatives-workspace';
const ASSETS = ['/academic-lists.html', '/css/academic-lists.css', '/js/academic-lists.js', '/js/students.js', '/css/table-access.css', '/js/theme.js', '/css/theme.css', '/css/blue-neon.css', '/css/clinical-theme.css', '/fonts/thmanyah/thmanyahsans-Regular.woff2', '/fonts/thmanyah/thmanyahsans-Bold.woff2', '/fonts/thmanyah/thmanyahserifdisplay-Bold.woff2', '/', '/index.html', '/css/app.css', '/js/app.js', '/js/api.js', '/js/schema.js', '/preferences.html', '/preferences-advanced.html', '/js/day-preferences.js', '/js/preferences.js', '/css/preferences.css', '/alternatives.html', '/js/alternatives.js', '/data-transfer.html', '/js/data-transfer.js', '/css/data-transfer.css', '/offline.html', '/manifest.webmanifest', '/icons/icon.svg', '/icons/icon-192.png', '/icons/icon-512.png'];
self.addEventListener('install', event => event.waitUntil(caches.open(CACHE).then(cache => cache.addAll(ASSETS))));
self.addEventListener('activate', event => event.waitUntil((async () => {
  for (const key of await caches.keys()) if (key.startsWith('university-timetable-shell-') && key !== CACHE) await caches.delete(key);
  await self.clients.claim();
})()));
self.addEventListener('message', event => { if(event.data?.type === 'ACTIVATE') self.skipWaiting(); });
self.addEventListener('fetch', event => {
  const url = new URL(event.request.url);
  if(event.request.method !== 'GET' || url.origin !== self.location.origin || url.pathname.startsWith('/api/')) return;
  if(event.request.mode === 'navigate') {
    event.respondWith(fetch(event.request).catch(() => caches.match('/offline.html')));
    return;
  }
  if (ASSETS.includes(url.pathname)) event.respondWith((async () => {
    const cache = await caches.open(CACHE);
    try {
      const response = await fetch(event.request, { cache: 'no-cache' });
      if (response.ok) await cache.put(event.request, response.clone());
      return response;
    } catch {
      return (await cache.match(event.request)) ?? Response.error();
    }
  })());
});
