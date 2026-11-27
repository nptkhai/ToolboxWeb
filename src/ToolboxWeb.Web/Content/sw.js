// Service worker of the installed app (PWA), registered by site.js as /sw.js?build=<build>.
// It stores only the app's own files that never change under the same address (versioned CSS
// and JS, fonts, icons, libraries) and the offline page. Pages and Jira or account data always
// come from the network and are never stored. A new build gets a new cache; old ones are removed.
'use strict';

const BUILD = new URL(self.location.href).searchParams.get('build') || 'dev';
const PREFIX = 'toolkit-static-';
const CACHE = PREFIX + BUILD;
const OFFLINE = '/offline.html';

self.addEventListener('install', function (event) {
    event.waitUntil((async function () {
        const cache = await caches.open(CACHE);
        // The offline page and the files it links to, so it can be shown with no network.
        const response = await fetch(OFFLINE, { cache: 'reload', credentials: 'same-origin' });
        if (response.ok) {
            const html = await response.clone().text();
            await cache.put(OFFLINE, response);
            const files = Array.from(html.matchAll(/(?:href|src)="(\/[^"]+)"/g), function (m) { return m[1].replace(/&amp;/g, '&'); });
            await Promise.all(files.map(function (file) {
                return cache.add(file).catch(function () { /* the page still shows without it */ });
            }));
        }
        await self.skipWaiting();
    })());
});

self.addEventListener('activate', function (event) {
    event.waitUntil((async function () {
        const names = await caches.keys();
        await Promise.all(names.filter(function (name) { return name.startsWith(PREFIX) && name !== CACHE; })
            .map(function (name) { return caches.delete(name); }));
        // The browser fetches pages itself (navigation preload), so the server still sees whether a
        // page is the window's own document or a frame of the screen shell (Sec-Fetch-Dest);
        // a page fetched again from here would lose that (Infrastructure/Web/ScreenFrames.cs).
        if (self.registration.navigationPreload) {
            await self.registration.navigationPreload.enable();
        }
        await self.clients.claim();
    })());
});

function isStaticFile(url) {
    return url.searchParams.has('v')
        || url.pathname.startsWith('/vendor/')
        || url.pathname.startsWith('/lib/')
        || url.pathname.startsWith('/brand/')
        || url.pathname === '/favicon.ico';
}

self.addEventListener('fetch', function (event) {
    const request = event.request;
    if (request.method !== 'GET') {
        return;
    }

    const url = new URL(request.url);
    if (url.origin !== self.location.origin) {
        return;
    }

    if (request.mode === 'navigate') {
        // Pages always come from the network; the offline page only when there is none.
        event.respondWith((async function () {
            try {
                const preloaded = await event.preloadResponse;
                return preloaded || await fetch(request);
            } catch (error) {
                return (await caches.match(OFFLINE)) || Response.error();
            }
        })());
        return;
    }

    if (isStaticFile(url)) {
        event.respondWith((async function () {
            const cache = await caches.open(CACHE);
            const stored = await cache.match(request);
            if (stored) {
                return stored;
            }
            const response = await fetch(request);
            if (response.ok && response.type === 'basic') {
                cache.put(request, response.clone());
            }
            return response;
        })());
    }
    // Everything else (Jira data, forms, API calls) goes to the network as usual.
});
