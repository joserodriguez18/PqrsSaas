import { API_URL, TOKEN_COOKIE, ROLE_COOKIE, SLUG_COOKIE, TOKEN_DAYS, COOKIE_SECURE } from './config.js';

export async function login(tenantSlug, email, password) {
    const res = await fetch(`${API_URL}/auth/login`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ tenantSlug, email, password })
    });
    if (!res.ok) {
        let msg = 'Credenciales inválidas.';
        try { const j = await res.json(); msg = j.detail || j.title || msg; } catch (e) { /* ignore */ }
        throw new Error(msg);
    }
    const data = await res.json();
    setToken(data.token);
    // El rol y el slug se toman de la respuesta del login (el claim "role" del JWT
    // usa una URI larga, no la clave "role"). El slug sí viene como claim custom.
    setCookie(ROLE_COOKIE, data.usuario.rol);
    setCookie(SLUG_COOKIE, data.tenant.slug);
    return data;
}

export function setToken(token) {
    setCookie(TOKEN_COOKIE, token);
}
export function getToken() {
    return getCookie(TOKEN_COOKIE);
}
export function getRole() {
    return getCookie(ROLE_COOKIE);
}
export function getTenantSlug() {
    return getCookie(SLUG_COOKIE);
}
export function isAuthenticated() {
    return !!getToken();
}
export function logout() {
    clearCookie(TOKEN_COOKIE);
    clearCookie(ROLE_COOKIE);
    clearCookie(SLUG_COOKIE);
}

function setCookie(name, value) {
    const secure = COOKIE_SECURE ? '; Secure' : '';
    const exp = new Date();
    exp.setDate(exp.getDate() + TOKEN_DAYS);
    document.cookie = `${name}=${encodeURIComponent(value)}; expires=${exp.toUTCString()}; path=/; SameSite=Strict${secure}`;
}
function getCookie(name) {
    const match = document.cookie.split('; ').find(c => c.startsWith(name + '='));
    return match ? decodeURIComponent(match.split('=')[1]) : null;
}
function clearCookie(name) {
    document.cookie = `${name}=; expires=Thu, 01 Jan 1970 00:00:00 GMT; path=/; SameSite=Strict`;
}
