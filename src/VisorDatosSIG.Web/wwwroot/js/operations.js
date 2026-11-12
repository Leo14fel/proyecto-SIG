(() => {
    const root = document.querySelector('[data-operation]');
    if (!root) return;
    const get = id => document.getElementById(id);
    const status = message => get('operation-status').textContent = message;
    const token = root.querySelector('input[name="__RequestVerificationToken"]')?.value;
    const endpoint = '/Operaciones/';
    const label = reconnect => reconnect ? 'Reconexión' : 'Corte';
    const coords = point => [point.lat ?? point.Lat, point.lng ?? point.Lng];
    const node = (tag, text, classes) => {
        const element = document.createElement(tag);
        element.textContent = text;
        if (classes) element.className = classes;
        return element;
    };
    async function request(path, body) {
        const form = body instanceof FormData;
        const headers = body ? { RequestVerificationToken: token, ...(!form && { 'Content-Type': 'application/json' }) } : {};
        const response = await fetch(endpoint + path, {
            method: body ? 'POST' : 'GET', headers,
            body: body ? (form ? body : JSON.stringify(body)) : undefined, cache: 'no-store'
        });
        if (response.status === 204) return null;
        const data = response.headers.get('content-type')?.includes('application/json') ? await response.json() : null;
        if (!response.ok || !data) {
            throw new Error(data?.message || (response.redirected || response.status === 401
                ? 'Inicia sesión de nuevo.' : 'No se pudo completar la operación. Comprueba la conexión y la configuración de operaciones.'));
        }
        return data;
    }
    function mapView() {
        if (!window.L) throw new Error('No se pudo cargar el mapa.');
        const map = L.map('operation-map').setView([-16.38, -60.96], 13);
        L.tileLayer('https://tile.openstreetmap.org/{z}/{x}/{y}.png', { maxZoom: 19, attribution: '© OpenStreetMap contributors' }).addTo(map);
        const element = document.getElementById('operation-map');
        let userMoved = false;
        map.on('dragstart zoomstart click', () => { userMoved = true; });
        fetch('/api/capas', { cache: 'no-store' }).then(response => {
            if (!response.ok) throw new Error('No se pudo consultar la extensión del mapa.');
            return response.json();
        }).then(items => {
            const cadastralBounds = items.find(item => item.key === 'manzanas')?.boundingBox;
            const streetBounds = items.find(item => item.key === 'vias')?.boundingBox;
            const box = cadastralBounds ?? streetBounds;
            if (box && !userMoved) {
                map.invalidateSize({ pan: false });
                map.fitBounds([[box[1], box[0]], [box[3], box[2]]], { padding: [24, 24], maxZoom: 16 });
            }
        }).catch(() => { /* El mapa base permanece disponible. */ });
        if ('ResizeObserver' in window) {
            const observer = new ResizeObserver(() => map.invalidateSize({ debounceMoveend: true }));
            observer.observe(element);
            map.on('unload', () => observer.disconnect());
        }
        return map;
    }
    function assignments() {
        const map = mapView();
        const route = L.featureGroup().addTo(map);
        const kind = get('operation-kind');
        const staff = get('operation-worker');
        const calculate = get('calculate-assignment');
        const save = get('save-assignment');
        const form = get('assignment-form');
        let start, marker, draft, busy = false, revision = 0, staffRequest = 0;
        function syncButtons() {
            calculate.disabled = busy || !staff.value || !start;
            save.disabled = busy || !draft;
        }
        function invalidate() {
            revision++;
            draft = null;
            syncButtons();
            route.clearLayers();
            get('operation-stops').replaceChildren();
        }
        async function workers() {
            const version = ++staffRequest;
            const currentKind = kind.value;
            staff.replaceChildren(new Option('Cargando trabajadores…', ''));
            staff.disabled = true;
            invalidate();
            try {
                const list = await request('Trabajadores?reconnect=' + (currentKind === 'reconnect'));
                if (version !== staffRequest) return;
                staff.replaceChildren(new Option(list.length ? 'Selecciona un trabajador' : 'Sin trabajadores disponibles', ''), ...list.map(u => new Option(u.nombre, u.id)));
                staff.disabled = list.length === 0;
                status(list.length ? 'Selecciona un trabajador y marca el punto de partida.' : 'No hay trabajadores activos con el rol de ' + (currentKind === 'reconnect' ? 'Reconector.' : 'Cortador.'));
            } catch (error) {
                if (version !== staffRequest) return;
                staff.replaceChildren(new Option('Error al cargar trabajadores', ''));
                status(error.message);
            } finally { syncButtons(); }
        }
        kind.addEventListener('change', workers);
        staff.addEventListener('change', invalidate);
        get('operation-count').addEventListener('input', invalidate);
        map.on('click', e => {
            start = { lat: e.latlng.lat, lng: e.latlng.lng };
            invalidate();
            if (marker) marker.setLatLng(e.latlng);
            else marker = L.marker(e.latlng).addTo(map);
            status('Punto de partida seleccionado.');
        });
        async function plan(persist) {
            if (busy) return;
            if (!start) return status('Marca el punto de partida en el mapa.');
            if (!staff.value) return status('Selecciona un trabajador.');
            if (!form.reportValidity()) return;
            if (persist && !draft) return;
            if (!persist) invalidate();
            const version = revision;
            const input = { workerId: Number(staff.value), reconnect: kind.value === 'reconnect',
                count: Number(get('operation-count').value), start, preview: persist ? draft : null };
            busy = true;
            syncButtons();
            status(persist ? 'Guardando…' : 'Calculando sobre la red de vías…');
            try {
                const data = await request('Ruta', input);
                if (persist) {
                    invalidate();
                    return status('Asignación guardada #' + data.id);
                }
                if (version !== revision) return;
                draft = data.preview;
                L.polyline(data.path.map(coords), { weight: 5 }).addTo(route);
                data.stops.forEach((stop, i) => {
                    L.circleMarker(coords(stop.point)).bindTooltip(String(i + 1), { permanent: true }).addTo(route);
                    get('operation-stops').append(node('li', [stop.code, stop.nombre].filter(Boolean).join(' · ') || '#' + stop.id));
                });
                map.fitBounds(route.getBounds(), { padding: [24, 24], maxZoom: 17 });
                status(data.stops.length + ' tareas · ' + (data.meters / 1000).toFixed(2) + ' km sobre la red');
            } catch (error) { if (version === revision) status(error.message); }
            finally { busy = false; syncButtons(); }
        }
        form.addEventListener('submit', event => { event.preventDefault(); plan(false); });
        save.addEventListener('click', () => plan(true));
        workers();
    }
    function monitor() {
        const map = mapView();
        const paths = L.featureGroup().addTo(map);
        const playback = L.layerGroup().addTo(map);
        let tracks = [], playing, busy = false, fit = true;
        const worker = get('monitor-worker');
        const play = get('monitor-play');
        const playLabel = play.querySelector('span');
        play.disabled = true;
        function stop() {
            clearInterval(playing);
            playing = undefined;
            playback.clearLayers();
            playLabel.textContent = 'Reproducir recorrido';
        }
        async function refresh() {
            if (busy || playing) return;
            busy = true;
            try {
                const params = new URLSearchParams({ day: get('monitor-day').value });
                if (worker.value) params.set('worker', worker.value);
                if (get('monitor-kind').value) params.set('reconnect', get('monitor-kind').value);
                const data = await request('Recorridos?' + params);
                paths.clearLayers();
                tracks = [];
                const cards = get('operation-stops');
                cards.replaceChildren();
                const names = new Map([...worker.options].slice(1).map(option => [option.value, option.text]));
                for (const a of data.assignments) {
                    if (!names.has(String(a.workerId))) {
                        const name = a.name || '#' + a.workerId;
                        names.set(String(a.workerId), name);
                        worker.add(new Option(name, a.workerId));
                    }
                    const samples = data.positions.filter(p => p.assignmentId === a.id).reverse();
                    const actual = samples.map(p => [p.latitude, p.longitude]);
                    const planned = JSON.parse(a.routeJson).map(coords);
                    if (planned.length) L.polyline(planned, { color: '#2563eb', dashArray: '5 8' }).addTo(paths);
                    if (actual.length) {
                        tracks.push(actual);
                        L.polyline(actual, { color: '#0f766e', weight: 4 }).addTo(paths);
                        const last = samples.at(-1);
                        L.circleMarker(actual.at(-1), { color: '#0f766e' })
                            .bindTooltip(node('span', (a.name || 'Trabajador') + ' · ' + new Date(last.recordedUtc + (last.recordedUtc.endsWith('Z') ? '' : 'Z')).toLocaleTimeString('es-BO', { timeZone: 'America/La_Paz' }))).addTo(paths);
                    }
                    const card = node('details', '', 'record-item mb-2');
                    card.append(node('summary', label(a.reconnect) + ' #' + a.id + ' · ' + (a.name || 'Trabajador')));
                    const list = node('ol', '', 'mt-2');
                    a.tasks.forEach(t => {
                        const row = node('li', t.completedUtc ? (t.observed ? 'Observado' : 'Realizado') : 'Pendiente');
                        if (t.completedUtc) {
                            const utc = new Date(t.completedUtc + (t.completedUtc.endsWith('Z') ? '' : 'Z'));
                            row.append(' · ' + utc.toLocaleString('es-BO', { timeZone: 'America/La_Paz' }));
                            if (t.meter) row.append(' · Medidor ' + t.meter + ' / ' + t.reading);
                            if (t.observation) row.append(' · ' + t.observation);
                            if (t.reportLat != null) {
                                const gps = [t.reportLat, t.reportLng];
                                const locate = node('button', 'Ubicar reporte', 'btn btn-sm btn-link');
                                locate.onclick = () => map.setView(gps, 17);
                                row.append(' ', locate);
                                L.circleMarker(gps, { radius: 4, color: '#b45309' }).addTo(paths);
                            }
                        }
                        t.evidence.forEach((id, index) => {
                            const link = node('a', 'Foto ' + (index + 1), 'ms-2');
                            link.href = endpoint + 'Evidencia/' + id;
                            row.append(link);
                        });
                        list.append(row);
                    });
                    card.append(list);
                    cards.append(card);
                }
                if (fit && paths.getBounds().isValid()) {
                    map.fitBounds(paths.getBounds(), { padding: [20, 20], maxZoom: 17 });
                    fit = false;
                }
                play.disabled = !tracks.length;
                status(data.assignments.length ? 'Actualizado ' + new Date().toLocaleTimeString() + ' · azul planificado, verde registrado' : 'Sin recorridos para esos filtros.');
            } catch (error) { status(error.message); }
            finally { busy = false; }
        }
        get('monitor-filter').addEventListener('submit', event => { event.preventDefault(); stop(); fit = true; refresh(); });
        play.addEventListener('click', () => {
            if (playing) return stop();
            playLabel.textContent = 'Detener reproducción';
            const markers = tracks.map(track => L.circleMarker(track[0], { radius: 8, color: '#c2410c' }).addTo(playback));
            let index = 0;
            playing = setInterval(() => {
                index++;
                tracks.forEach((track, i) => markers[i].setLatLng(track[Math.min(index, track.length - 1)]));
                if (tracks.every(track => index >= track.length)) stop();
            }, 550);
        });
        refresh();
        const timer = setInterval(() => { if (!document.hidden) refresh(); }, 8000);
        window.addEventListener('pagehide', () => { clearInterval(timer); stop(); });
    }
    function field() {
        const form = get('field-report');
        const holder = get('field-assignments');
        const native = navigator.userAgent.includes('VisorDatosSIGMobile');
        let task, position, tracking, trackTimer, nativePending, saving = false;
        let photos = [];
        function showPhotos() {
            get('photo-status').textContent = photos.length ? photos.length + '/3 fotos listas: ' + photos.map(photo => photo.name).join(', ') : 'Sin fotos seleccionadas.';
            get('clear-photos').hidden = !photos.length;
        }
        get('clear-photos').onclick = () => { photos = []; showPhotos(); };
        form.elements.Photos.onchange = () => {
            if (form.elements.Photos.files.length + photos.length > 3) status('Puedes adjuntar hasta tres fotos.');
            else photos.push(...form.elements.Photos.files);
            form.elements.Photos.value = '';
            showPhotos();
        };
        const mode = get('travel-mode');
        const navigate = get('field-navigate');
        function nativeRequest(action) {
            if (nativePending) return Promise.reject(new Error('Espera a que termine la solicitud anterior.'));
            return new Promise((resolve, reject) => {
                const timeout = setTimeout(() => { nativePending = null; reject(new Error('La solicitud al teléfono expiró.')); }, 90000);
                nativePending = result => {
                    clearTimeout(timeout);
                    nativePending = null;
                    if (result.error) reject(new Error(result.error));
                    else resolve(result);
                };
                location.href = 'visordatos-field://' + action;
            });
        }
        if (native) {
            navigate.removeAttribute('target');
            window.visorNativeResult = result => nativePending?.(result);
            get('native-photo').hidden = false;
            form.elements.Photos.closest('label').hidden = true;
            get('native-photo').onclick = async () => {
                if (photos.length >= 3) return status('Puedes adjuntar hasta tres fotos.');
                const selected = task;
                try {
                    const result = await nativeRequest('photo');
                    if (task !== selected) return;
                    const bytes = Uint8Array.from(atob(result.photo), c => c.charCodeAt(0));
                    photos.push(new File([bytes], result.name, { type: result.type }));
                    showPhotos();
                } catch (error) { status(error.message); }
            };
        }
        function locate() {
            if (native) return nativeRequest('gps');
            if (!window.isSecureContext || !navigator.geolocation)
                return Promise.reject(new Error('Abre el sitio por HTTPS para usar el GPS del navegador.'));
            return new Promise((resolve, reject) => navigator.geolocation.getCurrentPosition(
                p => resolve({ lat: p.coords.latitude, lng: p.coords.longitude, accuracy: p.coords.accuracy }),
                e => reject(new Error('GPS no disponible. Revisa el permiso y la ubicación del dispositivo. ' + e.message)),
                { enableHighAccuracy: true, maximumAge: 0, timeout: 20000 }));
        }
        function updateLocation() {
            if (!task || !position) { navigate.hidden = true; return; }
            const rad = Math.PI / 180;
            const a = Math.sin((task.latitude - position.lat) * rad / 2) ** 2
                + Math.cos(task.latitude * rad) * Math.cos(position.lat * rad) * Math.sin((task.longitude - position.lng) * rad / 2) ** 2;
            const distance = Math.round(12742000 * Math.asin(Math.sqrt(Math.min(1, a))));
            get('field-location').textContent = 'Destino ' + task.latitude.toFixed(6) + ', ' + task.longitude.toFixed(6)
                + ' · Actual ' + position.lat.toFixed(6) + ', ' + position.lng.toFixed(6)
                + ' · ' + distance + ' m en línea recta · precisión ' + Math.round(position.accuracy) + ' m';
            const params = new URLSearchParams({ api: '1', destination: task.latitude + ',' + task.longitude, travelmode: mode.value, dir_action: 'navigate' });
            navigate.href = 'https://www.google.com/maps/dir/?' + params;
            navigate.hidden = false;
        }
        function select(t) {
            if (saving) return;
            task = t;
            position = null;
            photos = [];
            form.reset();
            showPhotos();
            get('meter-details').hidden = false;
            form.hidden = false;
            get('field-destination').textContent = 'Orden ' + t.sequence + ' · ' + (t.codFijoValor ?? t.id);
            get('field-location').textContent = 'Destino ' + t.latitude.toFixed(6) + ', ' + t.longitude.toFixed(6);
            updateLocation();
            form.scrollIntoView({ block: 'nearest' });
        }
        function stopTracking(message) {
            clearTimeout(trackTimer);
            if (native && tracking && message)
                nativeRequest('stop').catch(error => status(error.message));
            tracking = null;
            if (message) status(message);
        }
        async function startTracking(id) {
            if (!native && tracking === id) return;
            if (!native) { stopTracking(); tracking = id; track(id); return; }
            try {
                await nativeRequest('track/' + id + '#' + encodeURIComponent(token));
                tracking = id;
                status('Recorrido iniciado. Android comparte la ubicación en segundo plano durante la navegación.');
            } catch (error) { status(error.message); }
        }
        async function track(id) {
            if (tracking !== id) return;
            try {
                if (!document.hidden && !saving && !nativePending) {
                    const gps = await locate();
                    if (tracking !== id) return;
                    if (gps.accuracy <= 100) {
                        await request('Posicion/' + id, gps);
                        status('Recorrido activo · ubicación enviada ' + new Date().toLocaleTimeString());
                    } else status('Recorrido activo · esperando mejor precisión del GPS.');
                }
            } catch (error) { if (tracking === id) status(error.message); }
            if (tracking === id) trackTimer = setTimeout(() => track(id), 12000);
        }
        async function load() {
            try {
                const data = await request('Tareas');
                holder.replaceChildren();
                if (!data.length) holder.append(node('p', 'Todavía no tienes asignaciones.'));
                if (tracking && !data.some(a => a.id === tracking && a.tasks.some(t => !t.completedUtc))) stopTracking();
                data.forEach(a => {
                    const group = node('section', '', 'field-assignment');
                    const total = a.tasks.length;
                    const completed = a.tasks.filter(t => t.completedUtc).length;
                    const pending = total - completed;
                    const header = node('div', '', 'field-assignment-header');
                    const title = node('h2', label(a.reconnect) + ' #' + a.id, 'h5 mb-1');
                    const summary = node('p', completed + ' de ' + total + ' tareas registradas · ' + pending + ' pendientes', 'field-assignment-summary');
                    const progress = document.createElement('progress');
                    progress.className = 'field-assignment-progress';
                    progress.max = Math.max(total, 1);
                    progress.value = completed;
                    progress.setAttribute('aria-label', 'Avance de ' + label(a.reconnect) + ' #' + a.id);
                    header.append(title, summary, progress);
                    group.append(header);
                    if (pending) {
                        const actions = node('div', '', 'field-assignment-actions');
                        const start = node('button', 'Iniciar recorrido', 'btn btn-outline-primary btn-sm');
                        const end = node('button', 'Detener recorrido', 'btn btn-outline-secondary btn-sm');
                        start.type = end.type = 'button';
                        start.onclick = () => startTracking(a.id);
                        end.onclick = () => { if (tracking === a.id) stopTracking('Recorrido detenido.'); };
                        actions.append(start, end);
                        group.append(actions);
                    }
                    const list = node('ol', '', 'field-task-list');
                    a.tasks.forEach(t => {
                        const row = node('li', '', 'field-task-card');
                        const details = node('div', '', 'field-task-details');
                        const line = node('div', '', 'field-task-topline');
                        const sequence = node('span', 'Parada ' + t.sequence, 'field-task-sequence');
                        const state = node('span', t.completedUtc ? (t.observed ? 'Observado' : 'Realizado') : 'Pendiente',
                            'field-task-state ' + (t.completedUtc ? (t.observed ? 'is-observed' : 'is-done') : 'is-pending'));
                        line.append(sequence, state);
                        const name = node('strong', t.nombre || 'Conexión de agua', 'field-task-name');
                        const code = node('span', 'Código fijo: ' + (t.codFijoValor ?? t.id), 'field-task-code');
                        details.append(line, name, code);
                        row.append(details);
                        if (!t.completedUtc) {
                            const button = node('button', 'Abrir tarea', 'btn btn-primary field-task-action');
                            button.type = 'button';
                            button.onclick = () => select(t);
                            row.append(button);
                        }
                        list.append(row);
                    });
                    group.append(list);
                    holder.append(group);
                });
            } catch (error) { status(error.message); }
        }
        get('get-gps').onclick = async () => {
            const selected = task;
            try { const gps = await locate(); if (task === selected) { position = gps; updateLocation(); } }
            catch (error) { status(error.message); }
        };
        mode.onchange = updateLocation;
        form.elements.Observed.onchange = () => get('meter-details').hidden = form.elements.Observed.value === 'true';
        form.onsubmit = async e => {
            e.preventDefault();
            if (saving || !task) return;
            const observed = form.elements.Observed.value === 'true';
            const description = observed ? 'registrar la observación' : 'confirmar el trabajo realizado';
            if (!window.confirm('¿Deseas ' + description + ' de la parada ' + task.sequence + '? Se guardará con la ubicación GPS actual.')) return;
            saving = true;
            const selected = task;
            const submit = form.querySelector('button[type="submit"], button:not([type])');
            submit.disabled = true;
            status('Obteniendo ubicación actual…');
            try {
                const data = new FormData(form);
                position = await locate();
                updateLocation();
                data.set('Lat', position.lat);
                data.set('Lng', position.lng);
                data.set('Accuracy', position.accuracy);
                data.delete('Photos');
                photos.forEach(photo => data.append('Photos', photo));
                await request('Resultado/' + selected.id, data);
                status('Resultado guardado.');
                form.hidden = true;
                task = null;
                await load();
            } catch (error) { status(error.message); }
            finally { saving = false; submit.disabled = false; }
        };
        load();
        window.addEventListener('pagehide', () => stopTracking());
    }
    async function initialize() {
        root.setAttribute('aria-busy', 'true');
        status('Comprobando disponibilidad de operaciones…');
        const controls = root.querySelectorAll('input:not([type="hidden"]), select, button, textarea');
        const disabled = [...controls].map(control => control.disabled);
        controls.forEach(control => control.disabled = true);
        try {
            const data = await request('Preparacion');
            if (!data.ready) {
                get('operation-status').classList.add('operation-alert');
                status('El mapa está disponible para consulta. Para habilitar las operaciones, el administrador debe aplicar la extensión SQL de la fase 2.');
                get('operation-worker')?.replaceChildren(new Option('Pendiente de configuración SQL', ''));
                if (root.dataset.operation !== 'field' && window.L) mapView();
                return;
            }
            controls.forEach((control, index) => control.disabled = disabled[index]);
            status('');
            if (root.dataset.operation === 'assign') assignments();
            if (root.dataset.operation === 'monitor') monitor();
            if (root.dataset.operation === 'field') field();
        } catch (error) {
            get('operation-status').classList.add('operation-alert');
            status(error.message);
        } finally { root.removeAttribute('aria-busy'); }
    }
    initialize();
})();
