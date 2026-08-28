// Configuración global del dashboard de agentes.
const API_URL = location.port === '5500' || location.port === '5173'
  ? 'http://localhost:5000/api/v1'
  : '/api/v1';

const TOKEN_COOKIE = 'ag_token';
const ROLE_COOKIE = 'ag_rol';
const SLUG_COOKIE = 'ag_slug';
const TOKEN_DAYS = 7;
const COOKIE_SECURE = location.protocol === 'https:';

const ROLES = {
    ADMIN: 'Administrador',
    AGENTE: 'Agente'
};

export { API_URL, TOKEN_COOKIE, ROLE_COOKIE, SLUG_COOKIE, TOKEN_DAYS, COOKIE_SECURE, ROLES };
