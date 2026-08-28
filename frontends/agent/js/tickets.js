import { apiRequest } from './api.js';
import { showLoading, hideLoading, showToast, badge } from './ui.js';

const Swal = window.Swal;
let ticketsCache = [];
let filters = { estado: '', prioridad: '' };

export function getFilters() { return filters; }

export async function render(container) {
    container.innerHTML = `
        <div class="flex flex-col md:flex-row md:items-center justify-between gap-4 mb-6">
            <div>
                <h2 class="text-2xl font-bold text-gray-900">Tickets PQRS</h2>
                <p class="text-sm text-gray-500 mt-1">Peticiones, quejas, reclamos y sugerencias.</p>
            </div>
        </div>
        <div class="bg-white rounded-xl border border-slate-200 shadow-sm p-4 mb-4 flex flex-col md:flex-row gap-3 items-end md:items-center justify-between">
            <div class="grid grid-cols-2 gap-3 w-full md:w-auto">
                <div>
                    <label class="block text-xs font-semibold text-gray-600 mb-1">Estado</label>
                    <select id="filtro-estado" class="w-full px-3 py-2 border border-slate-300 rounded-lg text-sm focus:border-blue-600 outline-none">
                        <option value="">Todos</option>
                        <option value="Pendiente">Pendiente</option>
                        <option value="EnProceso">En proceso</option>
                        <option value="Resuelto">Resuelto</option>
                    </select>
                </div>
                <div>
                    <label class="block text-xs font-semibold text-gray-600 mb-1">Prioridad</label>
                    <select id="filtro-prioridad" class="w-full px-3 py-2 border border-slate-300 rounded-lg text-sm focus:border-blue-600 outline-none">
                        <option value="">Todas</option>
                        <option value="Baja">Baja</option>
                        <option value="Media">Media</option>
                        <option value="Alta">Alta</option>
                    </select>
                </div>
            </div>
            <button id="btn-refrescar" class="inline-flex items-center gap-2 bg-gray-100 text-gray-700 px-3 py-2 rounded-lg text-sm font-semibold hover:bg-gray-200 transition">
                <i class="fas fa-sync-alt text-[14px]"></i> Refrescar
            </button>
        </div>
        <div id="tickets-body"></div>`;

    document.getElementById('filtro-estado').addEventListener('change', (e) => { filters.estado = e.target.value; loadTickets(); });
    document.getElementById('filtro-prioridad').addEventListener('change', (e) => { filters.prioridad = e.target.value; loadTickets(); });
    document.getElementById('btn-refrescar').addEventListener('click', () => loadTickets());

    await loadTickets();
}

export async function loadTickets() {
    const body = document.getElementById('tickets-body');
    showLoading('tickets-body');
    try {
        const params = new URLSearchParams();
        if (filters.estado) params.set('estado', filters.estado);
        if (filters.prioridad) params.set('prioridad', filters.prioridad);
        const qs = params.toString();
        ticketsCache = await apiRequest(`/tickets${qs ? '?' + qs : ''}`);
        hideLoading('tickets-body');
        renderTable(body);
    } catch (err) {
        hideLoading('tickets-body');
        body.innerHTML = `<div class="bg-white rounded-xl border border-slate-200 p-8 text-center text-rose-600">No se pudieron cargar los tickets: ${err.message}</div>`;
    }
}

function renderTable(body) {
    if (ticketsCache.length === 0) {
        body.innerHTML = `<div class="bg-white rounded-xl border border-slate-200 p-10 text-center text-gray-400">No hay tickets ${filters.estado || filters.prioridad ? 'con los filtros aplicados' : 'registrados'}.</div>`;
        return;
    }
    body.innerHTML = `
        <div class="hidden md:block bg-white rounded-xl border border-slate-200 shadow-sm overflow-hidden">
            <div class="overflow-x-auto">
                <table class="w-full text-left">
                    <thead>
                        <tr class="bg-gray-50 border-b border-slate-200 text-xs uppercase tracking-wider text-gray-500">
                            <th class="px-4 py-3">Radicado</th>
                            <th class="px-4 py-3">Cliente</th>
                            <th class="px-4 py-3">Asunto</th>
                            <th class="px-4 py-3">Tipo</th>
                            <th class="px-4 py-3">Prioridad</th>
                            <th class="px-4 py-3">Estado</th>
                            <th class="px-4 py-3 text-right">Acciones</th>
                        </tr>
                    </thead>
                    <tbody class="divide-y divide-slate-100">${ticketsCache.map(desktopRow).join('')}</tbody>
                </table>
            </div>
        </div>
        <div class="md:hidden space-y-4">${ticketsCache.map(mobileCard).join('')}</div>`;
}

function prioridadBadge(p) {
    const map = { Alta: ['Alta', 'red'], Media: ['Media', 'amber'], Baja: ['Baja', 'green'] };
    return badge(...(map[p] || [p || '-', 'slate']));
}
function estadoBadge(e) {
    const map = { Resuelto: ['Resuelto', 'green'], EnProceso: ['En proceso', 'amber'], Pendiente: ['Pendiente', 'slate'] };
    return badge(...(map[e] || [e || '-', 'slate']));
}
function tipoBadge(t) {
    const map = { Peticion: ['Petición', 'blue'], Queja: ['Queja', 'amber'], Reclamo: ['Reclamo', 'red'], Sugerencia: ['Sugerencia', 'green'] };
    return badge(...(map[t] || [t || '-', 'slate']));
}

function desktopRow(t) {
    return `
        <tr class="hover:bg-gray-50 transition">
            <td class="px-4 py-3 font-medium text-blue-700">${t.numeroRadicado}</td>
            <td class="px-4 py-3"><div class="font-medium text-gray-800">${t.clienteNombre}</div><div class="text-xs text-gray-400">${t.clienteCorreo}</div></td>
            <td class="px-4 py-3 text-gray-600 max-w-[200px] truncate">${t.asunto}</td>
            <td class="px-4 py-3">${tipoBadge(t.tipo)}</td>
            <td class="px-4 py-3">${prioridadBadge(t.prioridad)}</td>
            <td class="px-4 py-3">${estadoBadge(t.estado)}</td>
            <td class="px-4 py-3 text-right"><button class="text-blue-600 hover:underline text-sm font-medium" onclick="window.viewTicket('${t.id}')">Ver</button></td>
        </tr>`;
}

function mobileCard(t) {
    return `
        <div class="bg-white rounded-xl border border-slate-200 shadow-sm p-4 space-y-2">
            <div class="flex items-center justify-between">
                <span class="font-medium text-blue-700">${t.numeroRadicado}</span>
                ${prioridadBadge(t.prioridad)}
            </div>
            <div class="font-semibold text-gray-800">${t.clienteNombre}</div>
            <div class="text-sm text-gray-600 line-clamp-2">${t.asunto}</div>
            <div class="flex items-center justify-between pt-1">
                <div class="flex gap-1">${tipoBadge(t.tipo)}${estadoBadge(t.estado)}</div>
                <button class="text-blue-600 hover:underline text-sm font-medium" onclick="window.viewTicket('${t.id}')">Ver</button>
            </div>
        </div>`;
}

export async function viewTicket(id) {
    try {
        const t = await apiRequest(`/tickets/${id}`);
        const estadoOpts = ['Pendiente', 'EnProceso', 'Resuelto'].map(e =>
            `<option value="${e}" ${e === t.estado ? 'selected' : ''}>${e === 'EnProceso' ? 'En proceso' : e}</option>`).join('');
        const priorOpts = ['Baja', 'Media', 'Alta'].map(p =>
            `<option value="${p}" ${p === t.prioridad ? 'selected' : ''}>${p}</option>`).join('');

        await Swal.fire({
            title: t.numeroRadicado,
            width: 640,
            html: `
                <div class="text-left text-sm space-y-3">
                    <div class="grid grid-cols-2 gap-3">
                        <div><div class="text-xs font-semibold text-gray-500">Cliente</div><div class="font-medium text-gray-800">${t.clienteNombre}</div></div>
                        <div><div class="text-xs font-semibold text-gray-500">Correo</div><div class="text-gray-700 break-all">${t.clienteCorreo}</div></div>
                    </div>
                    <div><div class="text-xs font-semibold text-gray-500">Asunto</div><div class="font-medium text-gray-800">${t.asunto}</div></div>
                    <div><div class="text-xs font-semibold text-gray-500">Descripción</div><div class="text-gray-700 whitespace-pre-wrap">${t.descripcion}</div></div>
                    <div class="bg-blue-50 border border-blue-200 rounded-lg p-3">
                        <div class="text-xs font-semibold text-blue-700 mb-1">Resumen (IA)</div>
                        <p class="text-gray-700">${t.resumen || 'Sin resumen'}</p>
                    </div>
                    <div class="grid grid-cols-2 gap-3">
                        <div><div class="text-xs font-semibold text-gray-500 mb-1">Tipo</div>${tipoBadge(t.tipo)}</div>
                        <div><div class="text-xs font-semibold text-gray-500 mb-1">Sentimiento</div>${sentimientoBadge(t.sentimiento)}</div>
                    </div>
                    <div class="grid grid-cols-2 gap-3">
                        <div><label class="block text-xs font-semibold text-gray-500 mb-1">Estado</label>
                            <select id="tkt-estado" class="w-full px-2 py-1.5 border border-slate-300 rounded-lg text-sm">${estadoOpts}</select></div>
                        <div><label class="block text-xs font-semibold text-gray-500 mb-1">Prioridad</label>
                            <select id="tkt-prioridad" class="w-full px-2 py-1.5 border border-slate-300 rounded-lg text-sm">${priorOpts}</select></div>
                    </div>
                    <div class="text-xs text-gray-400">Creado: ${new Date(t.fechaCreacion).toLocaleString('es-CO')}</div>
                </div>`,
            showCancelButton: true,
            showConfirmButton: false,
            cancelButtonText: 'Cerrar',
            showDenyButton: true,
            denyButtonText: 'Marcar como resuelto',
            denyButtonColor: '#059669',
            didOpen: () => {
                document.getElementById('tkt-estado').addEventListener('change', (e) => updateTicketStatus(id, e.target.value));
                document.getElementById('tkt-prioridad').addEventListener('change', (e) => updateTicketPriority(id, e.target.value));
            }
        }).then((res) => {
            if (res.isDenied) closeTicket(id);
        });
    } catch (err) {
        showToast(err.message, 'error');
    }
}

function sentimientoBadge(s) {
    const map = { Positivo: ['Positivo', 'green'], Neutro: ['Neutro', 'slate'], Negativo: ['Negativo', 'red'] };
    return badge(...(map[s] || [s || '-', 'slate']));
}

export async function updateTicketStatus(id, estado) {
    try {
        await apiRequest(`/tickets/${id}/estado`, 'PUT', { estado });
        showToast('Estado actualizado', 'success');
        await loadTickets();
    } catch (err) {
        showToast(err.message, 'error');
    }
}

export async function updateTicketPriority(id, prioridad) {
    try {
        await apiRequest(`/tickets/${id}/prioridad`, 'PUT', { prioridad });
        showToast('Prioridad actualizada', 'success');
        await loadTickets();
    } catch (err) {
        showToast(err.message, 'error');
    }
}

export async function closeTicket(id) {
    const conf = await Swal.fire({
        title: 'Cerrar ticket',
        text: '¿Marcar este ticket como Resuelto?',
        icon: 'question',
        showCancelButton: true,
        confirmButtonText: 'Sí, resolver',
        confirmButtonColor: '#059669'
    });
    if (!conf.isConfirmed) return;
    await updateTicketStatus(id, 'Resuelto');
}

window.viewTicket = viewTicket;
