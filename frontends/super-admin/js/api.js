import { API_URL } from './config.js';
import { getToken, clearToken } from './auth.js';

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
        clearToken();
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
    if (res.status === 204) return null;
    return res.json();
}
