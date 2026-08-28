import * as Auth from './auth.js';
import { showSection, showToast } from './ui.js';
import { render as renderPanel } from './panel.js';
import { render as renderTenants } from './tenants.js';
import { render as renderSuperadmins, changeOwnPassword } from './superadmins.js';

const Swal = window.Swal;

const appContent = () => document.getElementById('app-content');
const ROUTES = {
    panel: { title: 'Panel de control', loader: renderPanel },
    tenants: { title: 'Tenants', loader: renderTenants },
    superadmins: { title: 'Super administradores', loader: renderSuperadmins }
};

function init() {
    document.getElementById('login-form').addEventListener('submit', onLogin);
    document.getElementById('logout-btn').addEventListener('click', onLogout);
    document.getElementById('logout-btn-mobile').addEventListener('click', onLogout);
    document.getElementById('menu-toggle').addEventListener('click', toggleSidebar);
    document.getElementById('sidebar-close').addEventListener('click', () => hideSidebar());

    // Navegación del sidebar
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
        navigate('panel');
    } else {
        showLogin();
    }
}

function onLogin(e) {
    e.preventDefault();
    const email = document.getElementById('email').value.trim();
    const password = document.getElementById('password').value;
    const btn = document.getElementById('login-btn');
    btn.disabled = true;
    btn.textContent = 'Ingresando...';

    Auth.login(email, password)
        .then(() => {
            showToast('Bienvenido', 'success');
            showApp();
            navigate('panel');
        })
        .catch((err) => {
            showToast(err.message, 'error');
        })
        .finally(() => {
            btn.disabled = false;
            btn.textContent = 'Ingresar';
        });
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
            <h2 class="text-2xl font-bold text-slate-900">Configuración</h2>
            <p class="text-sm text-slate-500 mt-1">Seguridad de tu cuenta de super administrador.</p>
        </div>
        <div class="max-w-xl bg-white rounded-xl border border-slate-200 shadow-sm p-6">
            <h3 class="text-sm font-semibold text-slate-800 mb-4">Cambiar contraseña</h3>
            <button id="btn-change-pw" class="bg-indigo-600 text-white px-4 py-2 rounded-lg text-sm font-semibold hover:bg-indigo-700 transition">Cambiar mi contraseña</button>
        </div>`;
    document.getElementById('btn-change-pw').addEventListener('click', changeOwnPassword);
}

function setActiveNav(name) {
    document.querySelectorAll('[data-nav]').forEach(link => {
        const active = link.dataset.nav === name;
        link.classList.toggle('bg-slate-100', active);
        link.classList.toggle('text-indigo-700', active);
        link.classList.toggle('font-semibold', active);
        link.classList.toggle('text-slate-600', !active);
    });
}

function toggleSidebar() {
    const sb = document.getElementById('sidebar');
    sb.classList.toggle('-translate-x-full');
}
function hideSidebar() {
    document.getElementById('sidebar').classList.add('-translate-x-full');
}

document.addEventListener('DOMContentLoaded', init);
