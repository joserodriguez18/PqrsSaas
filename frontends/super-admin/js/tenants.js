import { apiRequest } from './api.js';
import { showLoading, hideLoading, showToast, badge } from './ui.js';

const Swal = window.Swal;
let tenantsCache = [];

export async function render(container) {
    container.innerHTML = `
        <div class="flex flex-col md:flex-row md:items-center justify-between gap-4 mb-6">
            <div>
                <h2 class="text-2xl font-bold text-slate-900">Tenants</h2>
                <p class="text-sm text-slate-500 mt-1">Organizaciones registradas en la plataforma.</p>
            </div>
            <button id="btn-nuevo-tenant" class="inline-flex items-center gap-2 bg-indigo-600 text-white px-4 py-2 rounded-lg text-sm font-semibold hover:bg-indigo-700 transition shadow-sm">
                <span class="material-symbols-outlined text-[18px]">add</span> Nuevo tenant
            </button>
        </div>
        <div id="tenants-body"></div>`;

    document.getElementById('btn-nuevo-tenant').addEventListener('click', openCreateModal);

    await loadTenants(container);
}

async function loadTenants(container) {
    const body = document.getElementById('tenants-body');
    showLoading('tenants-body');
    try {
        tenantsCache = await apiRequest('/tenants');
        hideLoading('tenants-body');
        renderTable(body);
    } catch (err) {
        hideLoading('tenants-body');
        body.innerHTML = `<div class="bg-white rounded-xl border border-slate-200 p-8 text-center text-rose-600">No se pudieron cargar los tenants: ${err.message}</div>`;
    }
}

function renderTable(body) {
    if (tenantsCache.length === 0) {
        body.innerHTML = `<div class="bg-white rounded-xl border border-slate-200 p-10 text-center text-slate-400">Aún no hay tenants registrados.</div>`;
        return;
    }

    // Desktop: tabla. Móvil: tarjetas.
    body.innerHTML = `
        <div class="hidden md:block bg-white rounded-xl border border-slate-200 shadow-sm overflow-hidden">
            <div class="overflow-x-auto">
                <table class="w-full text-left">
                    <thead>
                        <tr class="bg-slate-50 border-b border-slate-200 text-xs uppercase tracking-wider text-slate-500">
                            <th class="px-4 py-3">Tenant</th>
                            <th class="px-4 py-3">Dominios</th>
                            <th class="px-4 py-3">Estado</th>
                            <th class="px-4 py-3">API Key</th>
                            <th class="px-4 py-3 text-right">Acciones</th>
                        </tr>
                    </thead>
                    <tbody class="divide-y divide-slate-100">${tenantsCache.map(desktopRow).join('')}</tbody>
                </table>
            </div>
        </div>
        <div class="md:hidden space-y-4">${tenantsCache.map(mobileCard).join('')}</div>`;
}

function estadoBadge(t) {
    const map = {
        Completado: badge('Completado', 'green'),
        Pendiente: badge('Pendiente', 'amber'),
        Error: badge('Error', 'red')
    };
    return map[t.estadoProvisionamiento] || badge(t.estadoProvisionamiento || '-', 'slate');
}

function activoBadge(t) {
    return t.activo ? badge('Activo', 'green') : badge('Inactivo', 'red');
}

function dominiosList(t) {
    const d = Array.isArray(t.dominios) && t.dominios.length ? t.dominios : [t.dominioPermitido];
    return d.map(x => `<span class="inline-block bg-slate-100 text-slate-600 rounded px-1.5 py-0.5 text-xs mr-1 mb-1">${x}</span>`).join('');
}

function desktopRow(t) {
    return `
        <tr class="hover:bg-slate-50 transition">
            <td class="px-4 py-3">
                <div class="font-medium text-slate-800">${t.nombre}</div>
                <div class="text-xs text-slate-400">${t.slug}</div>
            </td>
            <td class="px-4 py-3 max-w-xs">${dominiosList(t)}</td>
            <td class="px-4 py-3">${estadoBadge(t)} ${activoBadge(t)}</td>
            <td class="px-4 py-3">
                <button class="text-xs text-indigo-600 hover:underline inline-flex items-center gap-1" onclick="navigator.clipboard.writeText('${t.apiKeyWidget}')">copiar</button>
            </td>
            <td class="px-4 py-3 text-right whitespace-nowrap">${actions(t)}</td>
        </tr>`;
}

function mobileCard(t) {
    return `
        <div class="bg-white rounded-xl border border-slate-200 shadow-sm p-4 space-y-2">
            <div class="flex items-center justify-between">
                <div>
                    <div class="font-medium text-slate-800">${t.nombre}</div>
                    <div class="text-xs text-slate-400">${t.slug}</div>
                </div>
                <div class="flex gap-1">${estadoBadge(t)}${activoBadge(t)}</div>
            </div>
            <div class="text-xs text-slate-500">${dominiosList(t)}</div>
            <div class="flex items-center justify-between pt-1">
                <button class="text-xs text-indigo-600 hover:underline" onclick="navigator.clipboard.writeText('${t.apiKeyWidget}')">Copiar API key</button>
                <div class="flex gap-2">${actions(t)}</div>
            </div>
        </div>`;
}

function actions(t) {
    const icon = t.activo ? 'block' : 'check_circle';
    const title = t.activo ? 'Desactivar' : 'Activar';
    const color = t.activo ? 'hover:text-rose-600' : 'hover:text-emerald-600';
    return `<button class="p-1.5 rounded text-slate-400 ${color} hover:bg-slate-100" title="${title}"
        onclick="window.tenantsToggle('${t.id}','${!t.activo}')"><span class="material-symbols-outlined text-[20px]">${icon}</span></button>`;
}

window.tenantsToggle = (id, activo) => toggleTenant(id, activo === 'true');

async function toggleTenant(id, activo) {
    const t = tenantsCache.find(x => x.id === id);
    const action = activo ? 'activar' : 'desactivar';
    const conf = await Swal.fire({
        title: activo ? '¿Activar tenant?' : '¿Desactivar tenant?',
        text: `${t?.nombre || ''}. Esta acción ${activo ? 'restablece el acceso' : 'bloquea el acceso al tenant y su widget'}.`,
        icon: 'warning',
        showCancelButton: true,
        confirmButtonColor: activo ? '#059669' : '#e11d48',
        confirmButtonText: activo ? 'Sí, activar' : 'Sí, desactivar'
    });
    if (!conf.isConfirmed) return;

    try {
        await apiRequest(`/tenants/${id}/estado`, 'PUT', { activo });
        showToast(`Tenant ${action}do`, 'success');
        await loadTenants(document.getElementById('tenants-body').parentElement);
    } catch (err) {
        showToast(err.message, 'error');
    }
}

function openCreateModal() {
    Swal.fire({
        title: 'Nuevo tenant',
        html: `
            <div class="text-left space-y-3">
                <div><label class="block text-xs font-semibold text-slate-600 mb-1">Nombre de la empresa</label>
                    <input id="c-nombre" class="w-full px-3 py-2 border border-slate-300 rounded-lg text-sm focus:border-indigo-500 focus:ring-1 focus:ring-indigo-500 outline-none" placeholder="Acme Corp"></div>
                <div><label class="block text-xs font-semibold text-slate-600 mb-1">Email del administrador</label>
                    <input id="c-email" type="email" class="w-full px-3 py-2 border border-slate-300 rounded-lg text-sm focus:border-indigo-500 focus:ring-1 focus:ring-indigo-500 outline-none" placeholder="admin@acme.com"></div>
                <div><label class="block text-xs font-semibold text-slate-600 mb-1">Dominio principal</label>
                    <input id="c-dominio" class="w-full px-3 py-2 border border-slate-300 rounded-lg text-sm focus:border-indigo-500 focus:ring-1 focus:ring-indigo-500 outline-none" placeholder="https://acme.com"></div>
                <div><label class="block text-xs font-semibold text-slate-600 mb-1">Dominios adicionales (uno por línea, opcional)</label>
                    <textarea id="c-dominios" rows="2" class="w-full px-3 py-2 border border-slate-300 rounded-lg text-sm focus:border-indigo-500 focus:ring-1 focus:ring-indigo-500 outline-none" placeholder="https://www.acme.com"></textarea></div>
            </div>`,
        showCancelButton: true,
        confirmButtonText: 'Crear tenant',
        preConfirm: () => {
            const nombre = document.getElementById('c-nombre').value.trim();
            const email = document.getElementById('c-email').value.trim();
            const dominio = document.getElementById('c-dominio').value.trim();
            const extras = document.getElementById('c-dominios').value.split('\n').map(s => s.trim()).filter(Boolean);
            if (!nombre || !email || !dominio) {
                Swal.showValidationMessage('Nombre, email y dominio principal son obligatorios.');
                return false;
            }
            return { nombre, emailAdministrador: email, dominioPermitido: dominio, dominiosPermitidos: extras };
        }
    }).then(async (res) => {
        if (res.isConfirmed) await createTenant(res.value);
    });
}

async function createTenant(data) {
    try {
        const created = await apiRequest('/tenants/registro', 'POST', data);
        showToast('Tenant creado correctamente', 'success');
        await loadTenants(document.getElementById('tenants-body').parentElement);

        const creds = created.credenciales;
        const credsHtml = creds && creds.enviadasPorCorreo
            ? `<div class="bg-emerald-50 border border-emerald-200 rounded-lg p-3">
                  <div class="text-xs font-semibold text-emerald-700 mb-1">Credenciales enviadas por correo</div>
                  <p class="text-sm text-emerald-700">Se enviaron a <strong>${creds.emailAdministrador}</strong>. El administrador debe cambiar su contraseña en el primer ingreso.</p>
              </div>`
            : (creds && creds.password
                ? `<div class="bg-amber-50 border border-amber-200 rounded-lg p-3">
                      <div class="text-xs font-semibold text-amber-700 mb-1">Contraseña del administrador (una sola vez)</div>
                      <code class="text-amber-800 break-all">${creds.password}</code>
                      <p class="text-xs text-amber-600 mt-1">${creds.aviso || 'Guárdala ahora, no se puede recuperar.'}</p>
                  </div>`
                : '');

        Swal.fire({
            title: 'Tenant creado',
            html: `
                <div class="text-left text-sm space-y-3">
                    <div class="bg-slate-50 border border-slate-200 rounded-lg p-3">
                        <div class="text-xs font-semibold text-slate-500 mb-1">API Key del widget</div>
                        <code class="text-indigo-700 break-all">${created.apiKeyWidget}</code>
                    </div>
                    ${credsHtml}
                </div>`,
            icon: 'success',
            confirmButtonText: 'Entendido'
        });
    } catch (err) {
        showToast(err.message, 'error');
    }
}
