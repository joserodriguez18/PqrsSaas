// Configuración global del dashboard de agentes.
const API_URL = location.port === '5500' || location.port === '5173'
  ? 'http://localhost:5000/api/v1'
  : '/api/v1';

// Hub de SignalR (tiempo real): mismo origen que la API, pero ruta /hubs/tickets.
// La URL base debe ser http(s):// (no ws://): SignalR la usa también para el
// request HTTP de negociación y hace el upgrade a WebSocket por su cuenta.
const HUB_URL = (location.port === '5500' || location.port === '5173'
    ? 'http://localhost:5000'
    : `${location.protocol === 'https:' ? 'https' : 'http'}://${location.host}`) + '/hubs/tickets';

const TOKEN_COOKIE = 'ag_token';
const ROLE_COOKIE = 'ag_rol';
const SLUG_COOKIE = 'ag_slug';
const TOKEN_DAYS = 7;
const COOKIE_SECURE = location.protocol === 'https:';

const ROLES = {
    ADMIN: 'Administrador',
    AGENTE: 'Agente'
};

export { API_URL, HUB_URL, TOKEN_COOKIE, ROLE_COOKIE, SLUG_COOKIE, TOKEN_DAYS, COOKIE_SECURE, ROLES };
