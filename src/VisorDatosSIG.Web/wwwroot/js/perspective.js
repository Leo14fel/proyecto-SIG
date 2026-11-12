(() => {
    const status = document.getElementById('perspective-status');
    const state = document.getElementById('perspective-state');
    const pitch = document.getElementById('map-pitch');
    const toggles = [...document.querySelectorAll('[data-layer]')];
    const empty = { type: 'FeatureCollection', features: [] };
    const colors = ['#15803d', '#b45309', '#b91c1c', '#7e22ce', '#334155'];
    const names = ['Normal', 'Para corte', 'Cortado', 'Baja parcial', 'Baja total'];
    let map, request, timer;
    try {
        map = new maplibregl.Map({
            container: 'perspective-map', center: [-60.96, -16.38], zoom: 13, pitch: 55,
            style: {
                version: 8,
                sources: { osm: { type: 'raster', tiles: ['https://tile.openstreetmap.org/{z}/{x}/{y}.png'],
                    tileSize: 256, maxzoom: 19, attribution: '© OpenStreetMap contributors' } },
                layers: [{ id: 'osm', type: 'raster', source: 'osm' }]
            }
        });
        map.addControl(new maplibregl.NavigationControl());
    } catch {
        status.textContent = 'La vista 3D requiere WebGL. Puedes usar el visor 2D.';
        return;
    }
    function add(key, type, paint) {
        map.addSource(key, { type: 'geojson', data: empty });
        map.addLayer({ id: key, source: key, type, paint });
        map.on('click', key, event => {
            const item = event.features?.[0];
            if (!item || item.id == null) return;
            const body = document.createElement('div');
            if (key === 'codigos-fijos') {
                body.textContent = [item.properties.CodFijo, item.properties.Nombre, names[Number(item.properties.Estado) - 1]]
                    .filter(Boolean).join(' · ');
            }
            const link = document.createElement('a');
            link.textContent = 'Ver ficha y ubicación en 2D';
            link.href = '/Visor?' + new URLSearchParams({ capa: key, id: item.id });
            body.append(document.createElement('br'), link);
            new maplibregl.Popup().setLngLat(event.lngLat).setDOMContent(body).addTo(map);
        });
    }
    async function refresh() {
        request?.abort();
        const pending = new AbortController();
        request = pending;
        const b = map.getBounds();
        const bounds = new URLSearchParams({ minX: b.getWest(), minY: b.getSouth(), maxX: b.getEast(), maxY: b.getNorth() });
        const close = map.getZoom() >= 15;
        status.textContent = 'Consultando capas visibles…';
        try {
            await Promise.all(toggles.map(async toggle => {
                const key = toggle.dataset.layer;
                const source = map.getSource(key);
                if (!toggle.checked || (key === 'lotes' && !close)) return source.setData(empty);
                const params = new URLSearchParams(bounds);
                if (key === 'codigos-fijos' && state.value) params.set('estado', state.value);
                const response = await fetch('/api/capas/' + key + '/geojson?' + params, { signal: pending.signal, cache: 'no-store' });
                if (!response.ok) throw new Error('No se pudo consultar una capa.');
                const data = await response.json();
                if (!pending.signal.aborted) source.setData(data);
            }));
            if (!pending.signal.aborted) status.textContent = close ? '' : 'Acerca el mapa para mostrar Lotes.';
        } catch (error) { if (!pending.signal.aborted) status.textContent = error.message; }
    }
    map.on('load', async () => {
        add('manzanas', 'fill', { 'fill-color': '#14b8a6', 'fill-opacity': 0.25, 'fill-outline-color': '#0f766e' });
        add('lotes', 'fill', { 'fill-color': '#f59e0b', 'fill-opacity': 0.16, 'fill-outline-color': '#b45309' });
        add('vias', 'line', { 'line-color': '#1d4ed8', 'line-width': 2 });
        const stateColors = colors.flatMap((color, i) => [i + 1, color]);
        add('codigos-fijos', 'circle', { 'circle-radius': ['interpolate', ['linear'], ['zoom'], 10, 2, 17, 5],
            'circle-color': ['match', ['to-number', ['get', 'Estado']], ...stateColors, '#334155'] });
        try {
            const response = await fetch('/api/capas', { cache: 'no-store' });
            if (!response.ok) throw new Error();
            const layers = await response.json();
            const box = layers.find(x => x.key === 'manzanas')?.boundingBox;
            if (box) map.fitBounds([[box[0], box[1]], [box[2], box[3]]], { padding: 20, duration: 0, maxZoom: 15 });
        } catch { status.textContent = 'No se pudo obtener la extensión inicial.'; }
        map.on('moveend', () => { clearTimeout(timer); timer = setTimeout(refresh, 180); });
        state.addEventListener('change', refresh);
        toggles.forEach(toggle => toggle.addEventListener('change', refresh));
        refresh();
    });
    pitch.addEventListener('click', () => {
        const tilted = pitch.getAttribute('aria-pressed') !== 'true';
        pitch.setAttribute('aria-pressed', String(tilted));
        map.easeTo({ pitch: tilted ? 55 : 0, bearing: tilted ? map.getBearing() : 0 });
    });
})();
