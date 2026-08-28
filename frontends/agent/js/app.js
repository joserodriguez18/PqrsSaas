import * as Auth from './auth.js';
import { ROLES } from './config.js';
import { showSection, showToast, toggleSidebar, hideSidebar } from './ui.js';
import { apiRequest } from './api.js';
import { render as renderTickets } from './tickets.js';
import { render as renderKb } from './kb-articles.js';
import { render as renderAgents } from './agents.js';

const Swal = window.Swal;
const appContent = () => document.getElementById('app-content');

// Contraseña temporal usada en el login, necesaria para forzar el primer cambio.
let lastPassword = null;

const ROUTES = {
    tickets: { title: 'Tickets PQRS', loader: renderTickets },
    kb: { title: 'Base de conocimiento', loader: renderKb },
    agents: { title: 'Agentes', loader: renderAgents, adminOnly: true }
};

function init() {
    document.getElementById('login-form').addEventListener('submit', onLogin);
    document.getElementById('logout-btn').addEventListener('click', onLogout);
    document.getElementById('logout-btn-mobile').addEventListener('click', onLogout);
    document.getElementById('menu-toggle').addEventListener('click', toggleSidebar);
    document.getElementById('sidebar-close').addEventListener('click', () => hideSidebar());

    document.querySelectorAll('[data-nav]').forEach(link => {
        link.addEventListener('click', (e) => {
            e.preventDefault();
            hideSidebar();
            navigate(link.dataset.nav);
        });
    });
    document.getElementById('nav-config').addEventListener('click', (e) => { e.preventDefault(); hideSidebar(); showConfig(); });

    if (Auth.isAuthenticated()) {
        showApp();
        configureSidebar();
        navigate('tickets');
    } else {
        showLogin();
    }
}

function onLogin(e) {
    e.preventDefault();
    const slug = document.getElementById('slug').value.trim();
    const email = document.getElementById('email').value.trim();
    const password = document.getElementById('password').value;
    const btn = document.getElementById('login-btn');
    btn.disabled = true;
    btn.textContent = 'Ingresando...';

    Auth.login(slug, email, password)
        .then((data) => {
            lastPassword = password;
            showToast('Bienvenido', 'success');
            showApp();
            configureSidebar();
            navigate('tickets');
            if (data.usuario.debeCambiarPassword) {
                forcePasswordChange();
            }
        })
        .catch((err) => showToast(err.message, 'error'))
        .finally(() => { btn.disabled = false; btn.textContent = 'Ingresar'; });
}

function onLogout(e) {
    e.preventDefault();
    Swal.fire({
        title: 'Cerrar sesión',
        text: '¿Seguro que deseas salir?',
        icon: 'question',
        showCancelButton: true,
        confirmButtonText: 'Salir'
    }).then((res) => {
        if (res.isConfirmed) {
            Auth.logout();
            showLogin();
        }
    });
}

function configureSidebar() {
    const role = Auth.getRole();
    const isAdmin = role === ROLES.ADMIN;
    const agentsLink = document.getElementById('nav-agents');
    agentsLink.classList.toggle('hidden', !isAdmin);
    document.getElementById('user-role').textContent = role;
}

function showLogin() {
    showSection('login-view');
    document.getElementById('login-view').classList.remove('hidden');
}
function showApp() {
    showSection('app-view');
}

function navigate(name) {
    const route = ROUTES[name];
    if (!route) return;
    if (route.adminOnly && Auth.getRole() !== ROLES.ADMIN) return;
    document.getElementById('app-title').textContent = route.title;
    appContent().innerHTML = '';
    route.loader(appContent());
    setActiveNav(name);
}

function showConfig() {
    document.getElementById('app-title').textContent = 'Configuración';
    setActiveNav(null);
    appContent().innerHTML = `
        <div class="mb-6">
            <h2 class="text-2xl font-bold text-gray-900">Configuración</h2>
            <p class="text-sm text-gray-500 mt-1">Seguridad de tu cuenta.</p>
        </div>
        <div class="max-w-xl bg-white rounded-xl border border-slate-200 shadow-sm p-6">
            <h3 class="text-sm font-semibold text-gray-800 mb-4">Cambiar contraseña</h3>
            <button id="btn-change-pw" class="bg-blue-700 text-white px-4 py-2 rounded-lg text-sm font-semibold hover:bg-blue-800 transition">Cambiar mi contraseña</button>
        </div>`;
    document.getElementById('btn-change-pw').addEventListener('click', changeOwnPassword);
}

function setActiveNav(name) {
    document.querySelectorAll('[data-nav]').forEach(link => {
        const active = link.dataset.nav === name;
        link.classList.toggle('bg-gray-700', active);
        link.classList.toggle('text-white', active);
        link.classList.toggle('text-gray-300', !active);
    });
}

function forcePasswordChange() {
    changeOwnPassword(true);
}

function changeOwnPassword(force) {
    Swal.fire({
        title: force ? 'Cambia tu contraseña' : 'Cambiar contraseña',
        html: `
            <div class="text-left space-y-3">
                ${force ? '<p class="text-sm text-amber-600"><i class="fas fa-exclamation-triangle"></i> Debes cambiar tu contraseña antes de continuar.</p>' : ''}
                <div><label class="block text-xs font-semibold text-gray-600 mb-1">Contraseña actual</label>
                    <input id="pw-actual" type="password" class="w-full px-3 py-2 border border-slate-300 rounded-lg text-sm focus:border-blue-600 outline-none" value="${force ? (lastPassword || '') : ''}" ${force ? 'readonly' : ''}></div>
                <div><label class="block text-xs font-semibold text-gray-600 mb-1">Nueva contraseña (mín. 6)</label>
                    <input id="pw-nueva" type="password" class="w-full px-3 py-2 border border-slate-300 rounded-lg text-sm focus:border-blue-600 outline-none"></div>
            </div>`,
        showCancelButton: !force,
        confirmButtonText: 'Cambiar',
        allowOutsideClick: !force,
        allowEscapeKey: !force,
        preConfirm: () => {
            const actual = document.getElementById('pw-actual').value;
            const nueva = document.getElementById('pw-nueva').value;
            if (!actual || nueva.length < 6) {
                Swal.showValidationMessage('Completa los campos (nueva contraseña ≥ 6 caracteres).');
                return false;
            }
            return { passwordActual: actual, passwordNueva: nueva };
        }
    }).then(async (res) => {
        if (!res.isConfirmed) return;
        try {
            await apiRequest('/agents/me/password', 'PUT', res.value);
            lastPassword = null;
            showToast(force ? 'Contraseña actualizada. ¡Ya puedes usar el panel!' : 'Contraseña actualizada', 'success');
        } catch (err) {
            showToast(err.message, 'error');
            if (force) forcePasswordChange();
        }
    });
}

document.addEventListener('DOMContentLoaded', init);
