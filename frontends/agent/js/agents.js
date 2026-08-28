import { apiRequest } from './api.js';
import { showLoading, hideLoading, showToast, badge } from './ui.js';
import { ROLES } from './config.js';

const Swal = window.Swal;
let agentsCache = [];

export async function render(container) {
    container.innerHTML = `
        <div class="flex flex-col md:flex-row md:items-center justify-between gap-4 mb-6">
            <div>
                <h2 class="text-2xl font-bold text-gray-900">Agentes</h2>
                <p class="text-sm text-gray-500 mt-1">Usuarios con acceso al panel de este tenant.</p>
            </div>
            <button id="btn-invitar" class="inline-flex items-center gap-2 bg-blue-700 text-white px-4 py-2 rounded-lg text-sm font-semibold hover:bg-blue-800 transition shadow-sm">
                <i class="fas fa-user-plus text-[14px]"></i> Invitar agente
            </button>
        </div>
        <div id="agents-body"></div>`;

    document.getElementById('btn-invitar').addEventListener('click', openInviteModal);

    await loadAgents();
}

async function loadAgents() {
    const body = document.getElementById('agents-body');
    showLoading('agents-body');
    try {
        agentsCache = await apiRequest('/agents');
        hideLoading('agents-body');
        renderTable(body);
    } catch (err) {
        hideLoading('agents-body');
        body.innerHTML = `<div class="bg-white rounded-xl border border-slate-200 p-8 text-center text-rose-600">No se pudieron cargar los agentes: ${err.message}</div>`;
    }
}

function renderTable(body) {
    if (agentsCache.length === 0) {
        body.innerHTML = `<div class="bg-white rounded-xl border border-slate-200 p-10 text-center text-gray-400">Aún no hay agentes.</div>`;
        return;
    }
    body.innerHTML = `
        <div class="bg-white rounded-xl border border-slate-200 shadow-sm overflow-hidden">
            <div class="overflow-x-auto">
                <table class="w-full text-left">
                    <thead>
                        <tr class="bg-gray-50 border-b border-slate-200 text-xs uppercase tracking-wider text-gray-500">
                            <th class="px-4 py-3">Email</th>
                            <th class="px-4 py-3">Rol</th>
                            <th class="px-4 py-3">Estado</th>
                            <th class="px-4 py-3 text-right">Acciones</th>
                        </tr>
                    </thead>
                    <tbody class="divide-y divide-slate-100">
                        ${agentsCache.map(a => `
                            <tr class="hover:bg-gray-50 transition">
                                <td class="px-4 py-3 font-medium text-gray-800">${a.email}</td>
                                <td class="px-4 py-3">${a.rol === ROLES.ADMIN ? badge('Administrador', 'blue') : badge('Agente', 'slate')}</td>
                                <td class="px-4 py-3">${a.activo ? badge('Activo', 'green') : badge('Inactivo', 'red')}</td>
                                <td class="px-4 py-3 text-right whitespace-nowrap">
                                    ${a.activo
                                        ? `<button class="text-rose-600 hover:text-rose-800 text-sm" onclick="window.toggleAgent('${a.id}','${a.email}',false)"><i class="fas fa-user-slash"></i> Desactivar</button>`
                                        : `<button class="text-emerald-600 hover:text-emerald-800 text-sm" onclick="window.toggleAgent('${a.id}','${a.email}',true)"><i class="fas fa-user-check"></i> Activar</button>`}
                                </td>
                            </tr>`).join('')}
                    </tbody>
                </table>
            </div>
        </div>`;
}

function openInviteModal() {
    Swal.fire({
        title: 'Invitar agente',
        html: `
            <div class="text-left space-y-3">
                <div><label class="block text-xs font-semibold text-gray-600 mb-1">Email</label>
                    <input id="a-email" type="email" class="w-full px-3 py-2 border border-slate-300 rounded-lg text-sm focus:border-blue-600 outline-none" placeholder="agente@empresa.com"></div>
                <div><label class="block text-xs font-semibold text-gray-600 mb-1">Rol</label>
                    <select id="a-rol" class="w-full px-3 py-2 border border-slate-300 rounded-lg text-sm focus:border-blue-600 outline-none">
                        <option value="${ROLES.AGENTE}">Agente</option>
                        <option value="${ROLES.ADMIN}">Administrador</option>
                    </select></div>
            </div>`,
        showCancelButton: true,
        confirmButtonText: 'Invitar',
        preConfirm: () => {
            const email = document.getElementById('a-email').value.trim();
            const rol = document.getElementById('a-rol').value;
            if (!email) { Swal.showValidationMessage('El email es obligatorio.'); return false; }
            return { email, rol };
        }
    }).then(async (res) => {
        if (!res.isConfirmed) return;
        try {
            await apiRequest('/agents/invite', 'POST', res.value);
            showToast('Agente invitado', 'success');
            await loadAgents();
        } catch (err) {
            showToast(err.message, 'error');
        }
    });
}

export async function deactivateAgent(id, email) {
    await setAgentState(id, email, false);
}

async function setAgentState(id, email, activo) {
    const conf = await Swal.fire({
        title: activo ? 'Activar agente' : 'Desactivar agente',
        text: activo ? `¿Reactivar a ${email}? Volverá a tener acceso al panel.` : `¿Desactivar a ${email}? Perderá el acceso al panel.`,
        icon: 'warning',
        showCancelButton: true,
        confirmButtonText: activo ? 'Sí, activar' : 'Sí, desactivar',
        confirmButtonColor: activo ? '#059669' : '#e11d48'
    });
    if (!conf.isConfirmed) return;
    try {
        await apiRequest(`/agents/${id}/estado`, 'PUT', { activo });
        showToast(activo ? 'Agente activado' : 'Agente desactivado', 'success');
        await loadAgents();
    } catch (err) {
        showToast(err.message, 'error');
    }
}

window.toggleAgent = (id, email, activo) => setAgentState(id, email, activo);
window.delAgent = (id, email) => deactivateAgent(id, email);
