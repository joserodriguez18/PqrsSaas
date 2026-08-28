import { HUB_URL } from './config.js';
import { getToken } from './auth.js';
import { showToast } from './ui.js';
import { loadTickets } from './tickets.js';

let connection = null;

function setStatus(state) {
    const el = document.getElementById('rt-status');
    if (!el) return;
    const states = {
        connected: ['Verde', 'bg-emerald-500'],
        connecting: ['Conectando', 'bg-amber-500'],
        disconnected: ['Sin conexión', 'bg-rose-500']
    };
    const [label, color] = states[state] || states.disconnected;
    el.classList.remove('bg-emerald-500', 'bg-amber-500', 'bg-rose-500');
    el.classList.add(color);
    el.title = `Tiempo real: ${label}`;
}

export function initRealtime() {
    if (connection) return;

    // Si el CDN de SignalR no cargó (global indefinido), no rompemos la app:
    // solo lo registramos. La tabla sigue funcionando con el botón "Refrescar".
    if (typeof signalR === 'undefined') {
        console.error('[realtime] El cliente SignalR no cargó (¿CDN bloqueado?).');
        setStatus('disconnected');
        return;
    }

    setStatus('connecting');

    connection = new signalR.HubConnectionBuilder()
        .withUrl(HUB_URL, {
            accessTokenFactory: () => getToken() || ''
        })
        .withAutomaticReconnect()
        .configureLogging(signalR.LogLevel.Information)
        .build();

    connection.on('TicketNuevo', (ticket) => {
        showToast(`Nuevo ticket ${ticket.numeroRadicado}`, 'info');
        loadTickets();
    });

    connection.on('TicketActualizado', (ticket) => {
        showToast(`Ticket ${ticket.numeroRadicado} actualizado`, 'info');
        loadTickets();
    });

    connection.onreconnecting(() => {
        console.warn('[realtime] Reconectando...');
        setStatus('connecting');
    });

    connection.onreconnected(() => {
        console.log('[realtime] Reconectado.');
        setStatus('connected');
        loadTickets();
    });

    connection.onclose(() => {
        console.warn('[realtime] Conexión cerrada.');
        setStatus('disconnected');
    });

    connection.start()
        .then(() => { console.log('[realtime] Conectado.'); setStatus('connected'); })
        .catch((err) => {
            console.error('[realtime] No se pudo conectar:', err);
            setStatus('disconnected');
        });
}

export function stopRealtime() {
    if (connection) {
        connection.stop().catch(() => {});
        connection = null;
    }
    setStatus('disconnected');
}
