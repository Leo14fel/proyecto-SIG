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
    const sidebarBackdrop = document.getElementById('sidebar-backdrop');
    const setSidebarOpen = open => {
        shell.classList.toggle('sidebar-open', open);
        if (sidebarToggle) sidebarToggle.setAttribute('aria-expanded', String(open));
        if (sidebarBackdrop) sidebarBackdrop.hidden = !open;
    };
    if (sidebar && localStorage.getItem('visordatos.sidebar') === 'collapsed') shell.classList.add('sidebar-collapsed');
    sidebarCollapse?.addEventListener('click', () => {
        const collapsed = shell.classList.toggle('sidebar-collapsed');
        localStorage.setItem('visordatos.sidebar', collapsed ? 'collapsed' : 'expanded');
    });
    sidebarToggle?.addEventListener('click', () => setSidebarOpen(!shell.classList.contains('sidebar-open')));
    sidebarBackdrop?.addEventListener('click', () => setSidebarOpen(false));
    document.addEventListener('keydown', event => { if (event.key === 'Escape') setSidebarOpen(false); });
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
