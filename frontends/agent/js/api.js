import { API_URL } from './config.js';
import { getToken, logout } from './auth.js';

export async function apiRequest(endpoint, method = 'GET', body = null) {
    const token = getToken();
    const headers = {};
    if (token) headers['Authorization'] = `Bearer ${token}`;
    if (body !== null) headers['Content-Type'] = 'application/json';

    const res = await fetch(`${API_URL}${endpoint}`, {
        method,
        headers,
        body: body !== null ? JSON.stringify(body) : undefined
    });

    if (res.status === 401) {
        logout();
        window.location.reload();
        throw new Error('Sesión expirada');
    }
    if (res.status === 403) {
        throw new Error('No tienes permisos para realizar esta acción.');
    }
    if (!res.ok) {
        let msg = `Error ${res.status}`;
        try {
            const j = await res.json();
            msg = j.detail || j.title || j.message || msg;
        } catch (e) { /* ignore */ }
        throw new Error(msg);
    }
    if (res.status === 204) return null;
    return res.json();
}

export async function apiUpload(endpoint, formData) {
    const token = getToken();
    const headers = {};
    if (token) headers['Authorization'] = `Bearer ${token}`;
    // No se define Content-Type: fetch lo establece con el boundary para multipart.

    const res = await fetch(`${API_URL}${endpoint}`, {
        method: 'POST',
        headers,
        body: formData
    });

    if (res.status === 401) {
        logout();
        window.location.reload();
        throw new Error('Sesión expirada');
    }
    if (!res.ok) {
        let msg = `Error ${res.status}`;
        try {
            const j = await res.json();
            msg = j.detail || j.title || j.message || msg;
        } catch (e) { /* ignore */ }
        throw new Error(msg);
    }
    return res.json();
}
