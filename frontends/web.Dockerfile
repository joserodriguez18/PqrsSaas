FROM nginx:alpine

# Rutas del reverse proxy
COPY nginx.conf /etc/nginx/conf.d/default.conf

# Frontends estáticos (vanilla, sin build)
COPY super-admin/ /usr/share/nginx/html/superadmin/
COPY agent/ /usr/share/nginx/html/agent/
COPY widget/ /usr/share/nginx/html/widget/

# Landing
COPY index.html /usr/share/nginx/html/index.html
