import { apiRequest } from './api.js';
import { showLoading, hideLoading, showToast, badge } from './ui.js';

const Swal = window.Swal;
let adminsCache = [];

export async function render(container) {
    container.innerHTML = `
        <div class="flex flex-col md:flex-row md:items-center justify-between gap-4 mb-6">
            <div>
                <h2 class="text-2xl font-bold text-slate-900">Super administradores</h2>
                <p class="text-sm text-slate-500 mt-1">Cuentas con acceso total a la plataforma.</p>
            </div>
            <button id="btn-nuevo-sa" class="inline-flex items-center gap-2 bg-indigo-600 text-white px-4 py-2 rounded-lg text-sm font-semibold hover:bg-indigo-700 transition shadow-sm">
                <span class="material-symbols-outlined text-[18px]">add</span> Nuevo superadmin
            </button>
        </div>
        <div id="sa-body"></div>`;

    document.getElementById('btn-nuevo-sa').addEventListener('click', openCreateModal);

    await loadAdmins();
}

async function loadAdmins() {
    const body = document.getElementById('sa-body');
    showLoading('sa-body');
    try {
        adminsCache = await apiRequest('/superadmins');
        hideLoading('sa-body');
        renderTable(body);
    } catch (err) {
        hideLoading('sa-body');
        body.innerHTML = `<div class="bg-white rounded-xl border border-slate-200 p-8 text-center text-rose-600">No se pudieron cargar los superadmins: ${err.message}</div>`;
    }
}

function renderTable(body) {
    if (adminsCache.length === 0) {
        body.innerHTML = `<div class="bg-white rounded-xl border border-slate-200 p-10 text-center text-slate-400">Aún no hay super administradores.</div>`;
        return;
    }
    body.innerHTML = `
        <div class="bg-white rounded-xl border border-slate-200 shadow-sm overflow-hidden">
            <div class="overflow-x-auto">
                <table class="w-full text-left">
                    <thead>
                        <tr class="bg-slate-50 border-b border-slate-200 text-xs uppercase tracking-wider text-slate-500">
                            <th class="px-4 py-3">Email</th>
                            <th class="px-4 py-3">Estado</th>
                            <th class="px-4 py-3">Creado</th>
                        </tr>
                    </thead>
                    <tbody class="divide-y divide-slate-100">
                        ${adminsCache.map(a => `
                            <tr class="hover:bg-slate-50 transition">
                                <td class="px-4 py-3 font-medium text-slate-800">${a.email}</td>
                                <td class="px-4 py-3">${a.activo ? badge('Activo', 'green') : badge('Inactivo', 'red')}</td>
                                <td class="px-4 py-3 text-slate-500">${formatDate(a.fechaCreacion)}</td>
                            </tr>`).join('')}
                    </tbody>
                </table>
            </div>
        </div>`;
}

function formatDate(iso) {
    if (!iso) return '-';
    return new Date(iso).toLocaleDateString('es-CO', { year: 'numeric', month: 'short', day: 'numeric' });
}

function openCreateModal() {
    Swal.fire({
        title: 'Nuevo superadmin',
        html: `
            <div class="text-left space-y-3">
                <div><label class="block text-xs font-semibold text-slate-600 mb-1">Email</label>
                    <input id="sa-email" type="email" class="w-full px-3 py-2 border border-slate-300 rounded-lg text-sm focus:border-indigo-500 outline-none" placeholder="admin@empresa.com"></div>
                <div><label class="block text-xs font-semibold text-slate-600 mb-1">Contraseña (mín. 6 caracteres)</label>
                    <input id="sa-pass" type="password" class="w-full px-3 py-2 border border-slate-300 rounded-lg text-sm focus:border-indigo-500 outline-none" placeholder="••••••••"></div>
            </div>`,
        showCancelButton: true,
        confirmButtonText: 'Crear',
        preConfirm: () => {
            const email = document.getElementById('sa-email').value.trim();
            const password = document.getElementById('sa-pass').value;
            if (!email || password.length < 6) {
                Swal.showValidationMessage('Email válido y contraseña de al menos 6 caracteres.');
                return false;
            }
            return { email, password };
        }
    }).then(async (res) => {
        if (!res.isConfirmed) return;
        try {
            await apiRequest('/superadmins', 'POST', res.value);
            showToast('Superadmin creado', 'success');
            await loadAdmins();
        } catch (err) {
            showToast(err.message, 'error');
        }
    });
}

export async function changeOwnPassword() {
    Swal.fire({
        title: 'Cambiar mi contraseña',
        html: `
            <div class="text-left space-y-3">
                <div><label class="block text-xs font-semibold text-slate-600 mb-1">Contraseña actual</label>
                    <input id="pw-actual" type="password" class="w-full px-3 py-2 border border-slate-300 rounded-lg text-sm focus:border-indigo-500 outline-none"></div>
                <div><label class="block text-xs font-semibold text-slate-600 mb-1">Nueva contraseña (mín. 6)</label>
                    <input id="pw-nueva" type="password" class="w-full px-3 py-2 border border-slate-300 rounded-lg text-sm focus:border-indigo-500 outline-none"></div>
            </div>`,
        showCancelButton: true,
        confirmButtonText: 'Cambiar',
        preConfirm: () => {
            const actual = document.getElementById('pw-actual').value;
            const nueva = document.getElementById('pw-nueva').value;
            if (!actual || nueva.length < 6) {
                Swal.showValidationMessage('Completa ambos campos (nueva ≥ 6 caracteres).');
                return false;
            }
            return { passwordActual: actual, passwordNueva: nueva };
        }
    }).then(async (res) => {
        if (!res.isConfirmed) return;
        try {
            await apiRequest('/superadmins/me/password', 'PUT', res.value);
            showToast('Contraseña actualizada', 'success');
        } catch (err) {
            showToast(err.message, 'error');
        }
    });
}
