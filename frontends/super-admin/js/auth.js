import { API_URL, TOKEN_COOKIE, TOKEN_DAYS, COOKIE_SECURE } from './config.js';

export async function login(email, password) {
    const res = await fetch(`${API_URL}/auth/login-superadmin`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify({ email, password })
    });
    if (!res.ok) {
        let msg = 'Credenciales inválidas.';
        try { const j = await res.json(); msg = j.detail || j.title || msg; } catch (e) { /* ignore */ }
        throw new Error(msg);
    }
    const data = await res.json();
    setToken(data.token);
    return data;
}

export function setToken(token) {
    const secure = COOKIE_SECURE ? '; Secure' : '';
    const exp = new Date();
    exp.setDate(exp.getDate() + TOKEN_DAYS);
    document.cookie = `${TOKEN_COOKIE}=${encodeURIComponent(token)}; expires=${exp.toUTCString()}; path=/; SameSite=Strict${secure}`;
}

export function getToken() {
    const match = document.cookie.split('; ').find(c => c.startsWith(TOKEN_COOKIE + '='));
    return match ? decodeURIComponent(match.split('=')[1]) : null;
}

export function clearToken() {
    document.cookie = `${TOKEN_COOKIE}=; expires=Thu, 01 Jan 1970 00:00:00 GMT; path=/; SameSite=Strict`;
}

export function isAuthenticated() {
    return !!getToken();
}

export function logout() {
    clearToken();
}
