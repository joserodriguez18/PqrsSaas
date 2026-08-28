// Configuración global del dashboard de super administrador.
// En docker (nginx) la API se sirve en el mismo origen por /api/v1 (sin CORS).
// En dev con Live Server (puerto 5500) se apunta directo a la API en localhost:5000.
const API_URL = location.port === '5500' || location.port === '5173'
  ? 'http://localhost:5000/api/v1'
  : '/api/v1';

const TOKEN_COOKIE = 'sa_token';
const TOKEN_DAYS = 7;
// `Secure` solo en HTTPS (en http://localhost se omite para que la cookie se guarde).
const COOKIE_SECURE = location.protocol === 'https:';

export { API_URL, TOKEN_COOKIE, TOKEN_DAYS, COOKIE_SECURE };
