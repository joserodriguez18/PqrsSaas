import { apiRequest } from './api.js';
import { showLoading, hideLoading, showToast } from './ui.js';

export async function render(container) {
    container.innerHTML = `
        <div class="mb-6">
            <h2 class="text-2xl font-bold text-slate-900">Panel de control</h2>
            <p class="text-sm text-slate-500 mt-1">Resumen general de la plataforma.</p>
        </div>
        <div id="metrics" class="grid grid-cols-1 sm:grid-cols-3 gap-4"></div>`;

    const metrics = document.getElementById('metrics');
    showLoading('metrics');

    try {
        const [tenants, superadmins] = await Promise.all([
            apiRequest('/tenants'),
            apiRequest('/superadmins')
        ]);
        hideLoading('metrics');

        const activos = tenants.filter(t => t.activo).length;
        const provisioningError = tenants.filter(t => t.estadoProvisionamiento === 'Error').length;

        metrics.innerHTML = [
            card('Tenants totales', tenants.length, 'domain', activos > 0 ? 'text-slate-500' : 'text-slate-400'),
            card('Tenants activos', activos, 'check_circle', 'text-slate-500'),
            card('Super administradores', superadmins.length, 'admin_panel_settings', 'text-slate-500')
        ].join('');

        if (provisioningError > 0) {
            showToast(`${provisioningError} tenant(s) con error de aprovisionamiento`, 'warning');
        }
    } catch (err) {
        hideLoading('metrics');
        metrics.innerHTML = `<div class="col-span-full text-center text-rose-600 py-10">No se pudieron cargar las métricas: ${err.message}</div>`;
    }
}

function card(label, value, icon, textClass) {
    return `
        <div class="bg-white rounded-xl border border-slate-200 shadow-sm p-5">
            <div class="flex items-start justify-between mb-2">
                <span class="text-xs font-semibold uppercase tracking-wider text-slate-500">${label}</span>
                <span class="material-symbols-outlined text-slate-300">${icon}</span>
            </div>
            <div class="text-3xl font-bold text-slate-900">${value}</div>
        </div>`;
}
