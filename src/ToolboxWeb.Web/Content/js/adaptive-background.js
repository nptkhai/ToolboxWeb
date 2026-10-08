(function () {
    'use strict';

    const root = document.getElementById('adaptiveBackground');
    const starsHost = document.getElementById('adaptiveBackgroundStars');
    const cloudsHost = document.getElementById('adaptiveBackgroundClouds');
    const particlesHost = document.getElementById('adaptiveBackgroundParticles');
    const environmentSummary = document.querySelector('[data-environment-summary]');
    const currentTimeElement = environmentSummary?.querySelector('[data-current-time]');
    const currentDateElement = environmentSummary?.querySelector('[data-current-date]');
    const currentWeatherElement = environmentSummary?.querySelector('[data-current-weather]');
    if (!root || !starsHost || !cloudsHost || !particlesHost) {
        return;
    }

    const CACHE_KEY = 'toolbox.adaptive-background.weather.v1';
    const CACHE_TTL = 60 * 60 * 1000;
    const MAX_STALE_AGE = 24 * 60 * 60 * 1000;
    const API_URL = 'https://api.open-meteo.com/v1/forecast';
    const state = { coords: null, weather: null, savedAt: 0, refreshTimer: null, lastThemeKey: '' };

    const TIMES = {
        dawn: { colors: ['#26375f', '#9c6685', '#efb391'], glow: '#ffd09c', orb: ['74%', '18%', '.82'], horizon: '.62' },
        morning: { colors: ['#377fbd', '#84bedb', '#efd7aa'], glow: '#fff2bd', orb: ['76%', '18%', '.95'], horizon: '.18' },
        midday: { colors: ['#237fbe', '#75bee1', '#d8eced'], glow: '#fff8d5', orb: ['67%', '13%', '1'], horizon: '.12' },
        sunset: { colors: ['#312a56', '#b85e67', '#f1a55f'], glow: '#ffd07e', orb: ['73%', '47%', '.88'], horizon: '.52' },
        evening: { colors: ['#14233f', '#4b466c', '#8e6573'], glow: '#edf2ff', orb: ['76%', '22%', '.78'], horizon: '.68' },
        night: { colors: ['#071226', '#14294a', '#344366'], glow: '#e9efff', orb: ['77%', '19%', '.82'], horizon: '.78' }
    };

    const SEASONS = {
        spring: ['#8d657f', '#f0b0ae'],
        summer: ['#267db1', '#efc06c'],
        autumn: ['#74506b', '#d78353'],
        winter: ['#385b7b', '#9bb4c6']
    };

    const WEATHER_KIND = {
        0: 'clear', 1: 'clear', 2: 'cloudy', 3: 'cloudy', 45: 'fog', 48: 'fog',
        51: 'rain', 53: 'rain', 55: 'rain', 56: 'rain', 57: 'rain', 61: 'rain', 63: 'rain', 65: 'rain', 66: 'rain', 67: 'rain',
        71: 'snow', 73: 'snow', 75: 'snow', 77: 'snow', 80: 'rain', 81: 'rain', 82: 'rain', 85: 'snow', 86: 'snow',
        95: 'storm', 96: 'storm', 99: 'storm'
    };

    function getTimeBucket(date) {
        const current = date || new Date();
        const hour = current.getHours() + current.getMinutes() / 60;
        if (hour >= 5 && hour < 7) return 'dawn';
        if (hour >= 7 && hour < 11) return 'morning';
        if (hour >= 11 && hour < 16) return 'midday';
        if (hour >= 16 && hour < 18.75) return 'sunset';
        if (hour >= 18.75 && hour < 22) return 'evening';
        return 'night';
    }

    function getSeason(month, southernHemisphere) {
        const northernSeason = month <= 1 || month === 11
            ? 'winter'
            : month <= 4
                ? 'spring'
                : month <= 7
                    ? 'summer'
                    : 'autumn';

        if (!southernHemisphere) {
            return northernSeason;
        }

        return { spring: 'autumn', summer: 'winter', autumn: 'spring', winter: 'summer' }[northernSeason];
    }

    function mixHex(firstColor, secondColor, amount) {
        const parseHex = function (hex) {
            return hex.replace('#', '').match(/.{2}/g).map(function (part) {
                return parseInt(part, 16);
            });
        };
        const first = parseHex(firstColor);
        const second = parseHex(secondColor);
        return '#' + first.map(function (value, index) {
            return Math.round(value + (second[index] - value) * amount).toString(16).padStart(2, '0');
        }).join('');
    }

    function readCache() {
        try {
            const data = JSON.parse(localStorage.getItem(CACHE_KEY) || 'null');
            if (!data || data.version !== 1 || !data.coords || !data.weather || !data.savedAt) {
                return null;
            }
            return Date.now() - data.savedAt <= MAX_STALE_AGE ? data : null;
        } catch {
            return null;
        }
    }

    function writeCache() {
        try {
            localStorage.setItem(CACHE_KEY, JSON.stringify({
                version: 1,
                coords: state.coords,
                weather: state.weather,
                savedAt: state.savedAt
            }));
        } catch {
            // The time-based background still works when storage is unavailable.
        }
    }

    function createScene() {
        const stars = document.createDocumentFragment();
        for (let index = 0; index < 58; index += 1) {
            const star = document.createElement('i');
            star.className = 'adaptive-background__star';
            star.style.setProperty('--x', `${(index * 47.3) % 100}%`);
            star.style.setProperty('--y', `${4 + (index * 29.7) % 65}%`);
            star.style.setProperty('--size', `${1 + index % 3}px`);
            star.style.setProperty('--alpha', `${.28 + (index % 7) * .09}`);
            star.style.setProperty('--speed', `${1.4 + (index % 5) * .7}s`);
            stars.appendChild(star);
        }
        starsHost.replaceChildren(stars);

        const clouds = document.createDocumentFragment();
        for (let index = 0; index < 6; index += 1) {
            const cloud = document.createElement('i');
            cloud.className = 'adaptive-background__cloud';
            cloud.style.setProperty('--x', `${-55 - index * 24}%`);
            cloud.style.setProperty('--y', `${12 + (index * 13) % 48}%`);
            cloud.style.setProperty('--w', `${180 + (index % 3) * 90}px`);
            cloud.style.setProperty('--speed', `${48 + index * 9}s`);
            clouds.appendChild(cloud);
        }
        cloudsHost.replaceChildren(clouds);
    }

    function createParticles(kind) {
        if (particlesHost.dataset.kind === kind) {
            return;
        }

        particlesHost.dataset.kind = kind;
        particlesHost.replaceChildren();
        const count = kind === 'rain' || kind === 'storm' ? 64 : kind === 'snow' ? 42 : 0;
        const particles = document.createDocumentFragment();

        for (let index = 0; index < count; index += 1) {
            const particle = document.createElement('i');
            const particleKind = kind === 'storm' ? 'rain' : kind;
            particle.className = `adaptive-background__particle adaptive-background__particle--${particleKind}`;
            particle.style.setProperty('--x', `${(index * 37.7) % 104}%`);
            particle.style.setProperty('--alpha', `${.25 + (index % 6) * .09}`);
            particle.style.setProperty('--delay', `${-(index % 17) * .17}s`);

            if (kind === 'snow') {
                particle.style.setProperty('--size', `${3 + index % 5}px`);
                particle.style.setProperty('--speed', `${6 + (index % 7) * .7}s`);
            } else {
                particle.style.setProperty('--length', `${13 + index % 10}px`);
                particle.style.setProperty('--speed', `${.65 + (index % 7) * .045}s`);
            }

            particles.appendChild(particle);
        }

        particlesHost.appendChild(particles);
    }

    function getLocale() {
        return document.documentElement.lang || 'vi-VN';
    }

    function updateClock() {
        if (!currentTimeElement || !currentDateElement) {
            return;
        }

        const now = new Date();
        currentTimeElement.textContent = new Intl.DateTimeFormat(getLocale(), {
            hour: '2-digit',
            minute: '2-digit',
            hour12: false
        }).format(now);
        currentTimeElement.dateTime = now.toISOString();
        currentDateElement.textContent = new Intl.DateTimeFormat(getLocale(), {
            weekday: 'short',
            day: '2-digit',
            month: '2-digit',
            year: 'numeric'
        }).format(now);
    }

    function updateWeatherSummary(kind, unavailable) {
        if (!environmentSummary || !currentWeatherElement) {
            return;
        }

        if (!state.weather) {
            environmentSummary.dataset.weatherKind = unavailable ? 'unavailable' : 'loading';
            currentWeatherElement.textContent = unavailable
                ? environmentSummary.dataset.weatherUnavailable
                : environmentSummary.dataset.weatherLoading;
            return;
        }

        const labelKey = `weather${kind.charAt(0).toUpperCase()}${kind.slice(1)}`;
        const weatherLabel = environmentSummary.dataset[labelKey] || kind;
        const temperature = Number(state.weather.temperature_2m);
        const temperatureText = Number.isFinite(temperature)
            ? `${new Intl.NumberFormat(getLocale(), { maximumFractionDigits: 1 }).format(temperature)}°C`
            : '';

        environmentSummary.dataset.weatherKind = kind;
        currentWeatherElement.textContent = temperatureText
            ? `${temperatureText} · ${weatherLabel}`
            : weatherLabel;
    }

    function render() {
        const now = new Date();
        const timeKey = getTimeBucket(now);
        const seasonKey = getSeason(now.getMonth(), (state.coords?.latitude || 0) < 0);
        const kind = state.weather ? WEATHER_KIND[Number(state.weather.weather_code)] || 'cloudy' : 'clear';
        const timeTheme = TIMES[timeKey];
        const seasonTint = SEASONS[seasonKey];
        const weatherBlend = kind === 'storm' ? .42 : kind === 'rain' || kind === 'fog' ? .27 : kind === 'cloudy' ? .16 : 0;
        const top = mixHex(mixHex(timeTheme.colors[0], seasonTint[0], .11), '#2d3748', weatherBlend);
        const middle = mixHex(mixHex(timeTheme.colors[1], seasonTint[0], .11), '#667080', weatherBlend);
        const bottom = mixHex(mixHex(timeTheme.colors[2], seasonTint[1], .11), '#8a929b', weatherBlend * .72);
        const documentStyle = document.documentElement.style;

        documentStyle.setProperty('--adaptive-sky-top', top);
        documentStyle.setProperty('--adaptive-sky-mid', middle);
        documentStyle.setProperty('--adaptive-sky-bottom', bottom);
        documentStyle.setProperty('--adaptive-glow', timeTheme.glow);
        documentStyle.setProperty('--adaptive-orb-x', timeTheme.orb[0]);
        documentStyle.setProperty('--adaptive-orb-y', timeTheme.orb[1]);
        documentStyle.setProperty('--adaptive-orb-opacity', kind === 'storm' || Number(state.weather?.cloud_cover || 0) > 85 ? '.18' : timeTheme.orb[2]);
        documentStyle.setProperty('--adaptive-horizon', timeTheme.horizon);
        document.body.dataset.adaptiveNight = String(timeKey === 'night' || timeKey === 'evening');
        document.body.dataset.adaptiveWeather = kind;

        const themeMeta = document.querySelector('meta[name="theme-color"]');
        if (themeMeta) {
            themeMeta.content = top;
        }

        createParticles(kind);
        updateWeatherSummary(kind, false);
        state.lastThemeKey = `${timeKey}-${seasonKey}-${state.weather?.weather_code ?? 'none'}`;
    }

    function requestPosition() {
        return new Promise(function (resolve, reject) {
            if (!navigator.geolocation) {
                reject(new Error('Geolocation unavailable'));
                return;
            }

            navigator.geolocation.getCurrentPosition(function (position) {
                resolve({ latitude: position.coords.latitude, longitude: position.coords.longitude });
            }, reject, {
                enableHighAccuracy: false,
                timeout: 9000,
                maximumAge: CACHE_TTL
            });
        });
    }

    async function fetchWeather(coords) {
        const controller = new AbortController();
        const timeoutId = window.setTimeout(function () {
            controller.abort();
        }, 12000);
        const params = new URLSearchParams({
            latitude: coords.latitude.toFixed(4),
            longitude: coords.longitude.toFixed(4),
            current: 'temperature_2m,weather_code,cloud_cover',
            timezone: 'auto',
            forecast_days: '1'
        });

        try {
            const response = await fetch(`${API_URL}?${params}`, { signal: controller.signal });
            if (!response.ok) {
                throw new Error(`Weather API ${response.status}`);
            }

            const data = await response.json();
            if (!data.current) {
                throw new Error('Invalid weather data');
            }

            return data.current;
        } finally {
            window.clearTimeout(timeoutId);
        }
    }

    function scheduleRefresh() {
        window.clearTimeout(state.refreshTimer);
        const dueAt = state.savedAt ? state.savedAt + CACHE_TTL : Date.now() + CACHE_TTL;
        state.refreshTimer = window.setTimeout(updateWeather, Math.max(1000, dueAt - Date.now()));
    }

    async function updateWeather() {
        const latest = readCache();
        if (latest && Date.now() - latest.savedAt < CACHE_TTL) {
            state.coords = latest.coords;
            state.weather = latest.weather;
            state.savedAt = latest.savedAt;
            render();
            scheduleRefresh();
            return;
        }

        try {
            if (!state.coords) {
                state.coords = await requestPosition();
            }
            state.weather = await fetchWeather(state.coords);
            state.savedAt = Date.now();
            writeCache();
            render();
        } catch (error) {
            console.warn('[Adaptive background]', error);
            state.savedAt = state.savedAt || Date.now();
            if (!state.weather) {
                updateWeatherSummary('clear', true);
            }
        } finally {
            scheduleRefresh();
        }
    }

    function initialize() {
        createScene();
        updateClock();
        window.setInterval(updateClock, 1000);
        const cached = readCache();
        if (cached) {
            state.coords = cached.coords;
            state.weather = cached.weather;
            state.savedAt = cached.savedAt;
        }

        render();
        if (!cached || Date.now() - cached.savedAt >= CACHE_TTL) {
            updateWeather();
        } else {
            scheduleRefresh();
        }

        window.setInterval(function () {
            const now = new Date();
            const key = `${getTimeBucket(now)}-${getSeason(now.getMonth(), (state.coords?.latitude || 0) < 0)}-${state.weather?.weather_code ?? 'none'}`;
            if (key !== state.lastThemeKey) {
                render();
            }
        }, 30000);

        document.addEventListener('visibilitychange', function () {
            if (!document.hidden && Date.now() - state.savedAt >= CACHE_TTL) {
                updateWeather();
            }
        });
        window.addEventListener('online', function () {
            if (Date.now() - state.savedAt >= CACHE_TTL) {
                updateWeather();
            }
        });
        window.addEventListener('storage', function (event) {
            if (event.key !== CACHE_KEY) {
                return;
            }

            const fresh = readCache();
            if (!fresh) {
                return;
            }

            state.coords = fresh.coords;
            state.weather = fresh.weather;
            state.savedAt = fresh.savedAt;
            render();
            scheduleRefresh();
        });
    }

    initialize();
})();
