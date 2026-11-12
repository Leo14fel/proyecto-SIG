(() => {
    const isLocalhost = ['localhost', '127.0.0.1', '[::1]'].includes(window.location.hostname);
    const isStandalone = window.matchMedia('(display-mode: standalone)').matches || window.navigator.standalone === true;

    if ('serviceWorker' in navigator && (window.isSecureContext || isLocalhost)) {
        window.addEventListener('load', () => {
            navigator.serviceWorker.register('/service-worker.js', { scope: '/' }).catch(error => {
                console.warn('No se pudo registrar el modo sin conexión.', error);
            });
        });
    }

document.addEventListener('DOMContentLoaded', () => {
    const shell = document.body;
    const sidebar = document.getElementById('app-sidebar');
    const sidebarToggle = document.getElementById('sidebar-toggle');
    const sidebarCollapse = document.getElementById('sidebar-collapse');
    const sidebarClose = document.getElementById('sidebar-close');
    const sidebarBackdrop = document.getElementById('sidebar-backdrop');
    const mobile = window.matchMedia('(max-width: 800px)');
    const main = document.querySelector('.app-main');
    const setSidebarOpen = open => {
        const wasOpen = shell.classList.contains('sidebar-open');
        shell.classList.toggle('sidebar-open', open);
        if (sidebarToggle) sidebarToggle.setAttribute('aria-expanded', String(open));
        if (sidebarBackdrop) sidebarBackdrop.hidden = !open;
        if (main) main.inert = open;
        if (open) sidebarClose?.focus();
        else if (wasOpen && mobile.matches) sidebarToggle?.focus();
    };
    const updateCollapse = collapsed => {
        shell.classList.toggle('sidebar-collapsed', collapsed);
        if (!sidebarCollapse) return;
        const label = collapsed ? 'Expandir navegación' : 'Contraer navegación';
        sidebarCollapse.setAttribute('aria-label', label);
        sidebarCollapse.setAttribute('aria-expanded', String(!collapsed));
        sidebarCollapse.title = label;
    };
    if (sidebar) {
        try { updateCollapse(localStorage.getItem('visordatos.sidebar') === 'collapsed'); } catch { updateCollapse(false); }
    }
    sidebarCollapse?.addEventListener('click', () => {
        const collapsed = !shell.classList.contains('sidebar-collapsed');
        updateCollapse(collapsed);
        try { localStorage.setItem('visordatos.sidebar', collapsed ? 'collapsed' : 'expanded'); } catch { /* La navegación sigue operativa. */ }
    });
    sidebarToggle?.addEventListener('click', () => setSidebarOpen(!shell.classList.contains('sidebar-open')));
    sidebarClose?.addEventListener('click', () => setSidebarOpen(false));
    sidebarBackdrop?.addEventListener('click', () => setSidebarOpen(false));
    mobile.addEventListener('change', () => setSidebarOpen(false));
    document.addEventListener('keydown', event => { if (event.key === 'Escape') setSidebarOpen(false); });
    document.querySelectorAll('[data-password-toggle]').forEach(button => {
        button.addEventListener('click', () => {
            const input = document.getElementById(button.dataset.passwordToggle);
            if (!input) return;
            const show = input.type === 'password';
            input.type = show ? 'text' : 'password';
            button.setAttribute('aria-pressed', String(show));
            button.setAttribute('aria-label', show ? 'Ocultar contraseña' : 'Mostrar contraseña');
            const icon = button.querySelector('use');
            if (icon) icon.setAttribute('href', icon.getAttribute('href').split('#')[0] + (show ? '#eye-off' : '#eye'));
        });
    });
    const userFilter = document.getElementById('user-filter');
    if (userFilter) {
        const rows = [...document.querySelectorAll('#user-rows [data-user-search]')];
        const count = document.getElementById('user-filter-count');
        const normalize = value => value.normalize('NFD').replace(/\p{Diacritic}/gu, '').toLocaleLowerCase('es');
        const update = () => {
            const term = normalize(userFilter.value.trim());
            let visible = 0;
            rows.forEach(row => {
                row.hidden = !normalize(row.dataset.userSearch).includes(term);
                if (!row.hidden) visible++;
            });
            count.textContent = visible === 0 ? 'No se encontraron cuentas.' : visible + (visible === 1 ? ' cuenta' : ' cuentas');
        };
        userFilter.addEventListener('input', update);
        update();
    }
    document.querySelectorAll('[data-confirm-action]').forEach(form => form.addEventListener('submit', event => {
        if (!window.confirm(form.dataset.confirmAction)) event.preventDefault();
    }));

        const prompt = document.getElementById('pwa-install');
        const action = document.getElementById('pwa-install-action');
        const dismiss = document.getElementById('pwa-install-dismiss');
        const message = document.getElementById('pwa-install-message');
        if (!prompt || !action || !dismiss || isStandalone || sessionStorage.getItem('pwa-install-dismissed') === 'true') return;

        let deferredPrompt;
        const show = () => { prompt.hidden = false; };
        const hide = () => { prompt.hidden = true; };
        window.addEventListener('beforeinstallprompt', event => {
            event.preventDefault();
            deferredPrompt = event;
            show();
        });
        if (/iphone|ipad|ipod/i.test(navigator.userAgent)) {
            message.textContent = 'En Safari, toque Compartir y luego Agregar a pantalla de inicio.';
            action.hidden = true;
            show();
        }
        action.addEventListener('click', async () => {
            if (!deferredPrompt) return;
            deferredPrompt.prompt();
            await deferredPrompt.userChoice;
            deferredPrompt = undefined;
            hide();
        });
        dismiss.addEventListener('click', () => {
            sessionStorage.setItem('pwa-install-dismissed', 'true');
            hide();
        });
        window.addEventListener('appinstalled', hide);
    });
})();
