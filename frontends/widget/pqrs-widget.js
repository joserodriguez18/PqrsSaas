/*!
 * PQRS Widget - Asistente conversacional embebible
 * Vanilla JS + Shadow DOM (estilos aislados, sin dependencias).
 * Uso:
 *   <script src="https://TU-SERVIDOR/widget/pqrs-widget.js"
 *     data-tenant="<ApiKeyWidget>"
 *     data-api-url="https://TU-API/api/v1"
 *     data-color="#3525cd" data-title="Asistente Virtual"></script>
 */
(function () {
    'use strict';

    var script = document.currentScript;
    var apiKey = (script && script.getAttribute('data-tenant')) || '';
    var apiUrl = ((script && script.getAttribute('data-api-url')) || '').replace(/\/+$/, '') || location.origin + '/api/v1';
    var primary = (script && script.getAttribute('data-color')) || '#3525cd';
    var title = (script && script.getAttribute('data-title')) || 'Asistente Virtual';
    var greeting = (script && script.getAttribute('data-greeting')) || '¡Hola! Soy tu asistente virtual. ¿En qué te puedo ayudar hoy con tu Petición, Queja, Reclamo o Sugerencia?';

    /* ---------- Iconos SVG (autocontenidos, sin fuentes externas) ---------- */
    var ICON_CHAT = '<svg viewBox="0 0 24 24" width="26" height="26" fill="currentColor"><path d="M20 2H4c-1.1 0-2 .9-2 2v18l4-4h14c1.1 0 2-.9 2-2V4c0-1.1-.9-2-2-2z"/></svg>';
    var ICON_CLOSE = '<svg viewBox="0 0 24 24" width="22" height="22" fill="currentColor"><path d="M19 6.41 17.59 5 12 10.59 6.41 5 5 6.41 10.59 12 5 17.59 6.41 19 12 13.41 17.59 19 19 17.59 13.41 12z"/></svg>';
    var ICON_SEND = '<svg viewBox="0 0 24 24" width="20" height="20" fill="currentColor"><path d="M2.01 21 23 12 2.01 3 2 10l15 2-15 2z"/></svg>';
    var ICON_USER = '<svg viewBox="0 0 24 24" width="20" height="20" fill="currentColor"><path d="M12 12c2.21 0 4-1.79 4-4s-1.79-4-4-4-4 1.79-4 4 1.79 4 4 4zm0 2c-2.67 0-8 1.34-8 4v2h16v-2c0-2.66-5.33-4-8-4z"/></svg>';
    var ICON_CHECK = '<svg viewBox="0 0 24 24" width="18" height="18" fill="currentColor"><path d="M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm-2 15-5-5 1.41-1.41L10 14.17l7.59-7.59L19 8l-9 9z"/></svg>';
    var ICON_DOC = '<svg viewBox="0 0 24 24" width="18" height="18" fill="currentColor"><path d="M14 2H6c-1.1 0-2 .9-2 2v16c0 1.1.9 2 2 2h12c1.1 0 2-.9 2-2V8l-6-6zm2 16H8v-2h8v2zm0-4H8v-2h8v2zm-3-5V3.5L18.5 9H13z"/></svg>';
    var ICON_SUCCESS = '<svg viewBox="0 0 24 24" width="52" height="52" fill="currentColor"><path d="M12 2C6.48 2 2 6.48 2 12s4.48 10 10 10 10-4.48 10-10S17.52 2 12 2zm-2 15-5-5 1.41-1.41L10 14.17l7.59-7.59L19 8l-9 9z"/></svg>';

    /* ---------- HTML del Shadow DOM ---------- */
    var html =
        '<button class="fab" type="button" aria-label="Abrir asistente">' + ICON_CHAT + '</button>' +
        '<div class="panel hidden" role="dialog" aria-label="Asistente virtual">' +
            '<div class="header">' +
                '<div class="header-left">' +
                    '<div class="avatar">' + ICON_USER + '</div>' +
                    '<span class="title">' + title + '</span>' +
                '</div>' +
                '<button class="icon-btn close-btn" type="button" aria-label="Cerrar">' + ICON_CLOSE + '</button>' +
            '</div>' +
            '<div class="body">' +
                '<!-- VISTA CHAT -->' +
                '<div class="view view-chat">' +
                    '<div class="messages"></div>' +
                    '<div class="inputbar">' +
                        '<input class="input" type="text" placeholder="Escribe un mensaje..." autocomplete="off">' +
                        '<button class="send-btn" type="button" aria-label="Enviar">' + ICON_SEND + '</button>' +
                    '</div>' +
                '</div>' +
                '<!-- VISTA FORMULARIO -->' +
                '<div class="view view-form hidden">' +
                    '<div class="form-head">Radicar una solicitud</div>' +
                    '<form id="pqrs-form" class="form">' +
                        '<label>Nombre<sup>*</sup></label>' +
                        '<input name="clienteNombre" type="text" required placeholder="Tu nombre">' +
                        '<label>Correo<sup>*</sup></label>' +
                        '<input name="clienteCorreo" type="email" required placeholder="tu@correo.com">' +
                        '<label>Asunto<sup>*</sup></label>' +
                        '<input name="asunto" type="text" required placeholder="Asunto de la solicitud">' +
                        '<label>Descripción<sup>*</sup></label>' +
                        '<textarea name="descripcion" rows="4" required placeholder="Cuéntanos el detalle de tu Petición, Queja, Reclamo o Sugerencia"></textarea>' +
                        '<div class="form-actions">' +
                            '<button type="button" class="btn back-btn">Volver</button>' +
                            '<button type="submit" class="btn submit-btn">' + ICON_SEND + ' Enviar</button>' +
                        '</div>' +
                    '</form>' +
                '</div>' +
                '<!-- VISTA ÉXITO -->' +
                '<div class="view view-success hidden">' +
                    '<div class="success-icon">' + ICON_SUCCESS + '</div>' +
                    '<h3 class="success-title">¡Gracias por tu solicitud!</h3>' +
                    '<p class="success-text">Tu solicitud fue radicada correctamente.</p>' +
                    '<div class="radicado">Nº <strong id="radicado"></strong></div>' +
                    '<p class="success-note">Un agente la revisará y te responderemos en los plazos establecidos.</p>' +
                    '<button type="button" class="btn done-btn">Hacer otra consulta</button>' +
                '</div>' +
            '</div>' +
        '</div>';

    /* ---------- CSS scoped (portado del diseño Stitch, sin Tailwind) ---------- */
    var css = [
        ':host { all: initial; --primary: ' + primary + '; display: block; position: fixed; bottom: 24px; right: 24px; z-index: 2147483000; }',
        '* { box-sizing: border-box; font-family: Inter, system-ui, -apple-system, Arial, sans-serif; }',
        '@media (max-width: 480px) { :host { right: 12px; bottom: 12px; left: 12px; } }',
        /* FAB */
        '.fab { width: 56px; height: 56px; border-radius: 50%; border: none; cursor: pointer;',
        '  background: var(--primary); color: #fff; display: flex; align-items: center; justify-content: center;',
        '  box-shadow: 0 10px 25px -5px rgba(0,0,0,0.3); transition: transform .2s, opacity .2s; margin-left:auto; }',
        '.fab:hover { transform: scale(1.05); } .fab.hidden, .panel.hidden { display: none !important; }',
        /* Panel */
        '.panel { width: 360px; height: 520px; max-width: calc(100vw - 24px); max-height: calc(100vh - 24px);',
        '  background: #ffffff; border-radius: 12px; box-shadow: 0 10px 25px -5px rgba(0,0,0,0.18);',
        '  display: flex; flex-direction: column; overflow: hidden; border: 1px solid #c7c4d8; }',
        '.header { background: var(--primary); color: #fff; padding: 14px 20px; display: flex; align-items: center; justify-content: space-between; flex: 0 0 auto; }',
        '.header-left { display: flex; align-items: center; gap: 10px; }',
        '.avatar { width: 32px; height: 32px; border-radius: 50%; background: #dce9ff; color: var(--primary); display: flex; align-items: center; justify-content: center; }',
        '.title { font-size: 16px; font-weight: 600; }',
        '.icon-btn { background: transparent; border: none; color: #fff; cursor: pointer; padding: 6px; border-radius: 50%; display: flex; align-items: center; justify-content: center; }',
        '.icon-btn:hover { background: rgba(255,255,255,0.15); }',
        '.body { flex: 1; display: flex; flex-direction: column; min-height: 0; }',
        '.view { display: flex; flex-direction: column; flex: 1; min-height: 0; }',
        '.view.hidden { display: none; }',
        /* Chat */
        '.messages { flex: 1; overflow-y: auto; padding: 16px 20px; display: flex; flex-direction: column; gap: 12px; background: #f8f9ff; }',
        '.msg { max-width: 85%; padding: 10px 14px; border-radius: 16px; font-size: 13px; line-height: 20px; box-shadow: 0 1px 2px rgba(0,0,0,0.06); }',
        '.msg.user { align-self: flex-end; background: var(--primary); color: #fff; border-bottom-right-radius: 4px; }',
        '.msg.bot { align-self: flex-start; background: #e5eeff; color: #0b1c30; border-bottom-left-radius: 4px; }',
        '.typing { display: flex; gap: 4px; padding: 4px 2px; }',
        '.typing span { width: 6px; height: 6px; border-radius: 50%; background: #777587; animation: pqrsblink 1.4s infinite both; }',
        '.typing span:nth-child(2) { animation-delay: .2s; } .typing span:nth-child(3) { animation-delay: .4s; }',
        '@keyframes pqrsblink { 0%,100% { opacity:.2; transform:scale(.8);} 20% {opacity:1; transform:scale(1.2);} }',
        /* Acciones Si/No */
        '.actions { display: flex; flex-direction: column; gap: 8px; margin-top: 4px; padding-left: 2px; }',
        '.btn-action { display: flex; align-items: center; gap: 8px; width: 100%; text-align: left;',
        '  padding: 10px 16px; border-radius: 999px; font-size: 14px; font-weight: 600; cursor: pointer; transition: background .15s; }',
        '.btn-action.yes { background: #ffffff; color: var(--primary); border: 1px solid var(--primary); }',
        '.btn-action.yes:hover { background: #eff4ff; }',
        '.btn-action.no { background: var(--primary); color: #fff; border: 1px solid var(--primary); }',
        '.btn-action.no:hover { background: color-mix(in srgb, var(--primary) 85%, black); }',
        /* Input */
        '.inputbar { display: flex; align-items: center; gap: 8px; padding: 12px; background: #fff; border-top: 1px solid #e2e8f0; }',
        '.input { flex: 1; border: 1px solid #c7c4d8; border-radius: 999px; padding: 9px 16px; font-size: 13px; outline: none; }',
        '.input:focus { border-color: var(--primary); }',
        '.input:disabled { background: #f1f5f9; opacity: .6; }',
        '.send-btn { width: 38px; height: 38px; border-radius: 50%; border: none; background: var(--primary); color: #fff; cursor: pointer; display: flex; align-items: center; justify-content: center; flex: 0 0 auto; }',
        '.send-btn:hover { background: color-mix(in srgb, var(--primary) 85%, black); }',
        '.send-btn:disabled { opacity: .5; cursor: default; }',
        /* Formulario */
        '.form-head { padding: 14px 20px; font-size: 16px; font-weight: 600; color: #0b1c30; background: #fff; border-bottom: 1px solid #e2e8f0; }',
        '.form { padding: 16px 20px; overflow-y: auto; display: flex; flex-direction: column; gap: 4px; }',
        '.form label { font-size: 12px; font-weight: 500; color: #464555; margin-top: 8px; }',
        '.form input, .form textarea { width: 100%; padding: 9px 12px; border: 1px solid #c7c4d8; border-radius: 8px; font-size: 14px; font-family: inherit; outline: none; }',
        '.form input:focus, .form textarea:focus { border-color: var(--primary); }',
        '.form sup { color: #ba1a1a; }',
        '.form-actions { display: flex; justify-content: flex-end; gap: 10px; margin-top: 16px; }',
        '.btn { border: none; cursor: pointer; font-size: 14px; font-weight: 600; padding: 10px 18px; border-radius: 999px; display: inline-flex; align-items: center; gap: 8px; }',
        '.submit-btn { background: var(--primary); color: #fff; } .submit-btn:hover { background: color-mix(in srgb, var(--primary) 85%, black); }',
        '.back-btn { background: #fff; color: #464555; border: 1px solid #c7c4d8; }',
        /* Éxito */
        '.view-success { align-items: center; justify-content: center; text-align: center; padding: 24px; gap: 8px; }',
        '.success-icon { color: #059669; }',
        '.success-title { font-size: 18px; font-weight: 700; color: #0b1c30; margin: 8px 0 4px; }',
        '.success-text { color: #464555; font-size: 14px; }',
        '.radicado { background: #eff4ff; color: var(--primary); border: 1px solid var(--primary); padding: 12px 20px; border-radius: 12px; font-size: 16px; margin: 12px 0; }',
        '.success-note { color: #64748b; font-size: 12px; max-width: 260px; }',
        '.done-btn { background: var(--primary); color: #fff; margin-top: 12px; }',
        '::-webkit-scrollbar { width: 8px; } ::-webkit-scrollbar-thumb { background: #cbd5e1; border-radius: 8px; }'
    ].join('\n');

    /* ---------- Montar Shadow DOM ---------- */
    var host = document.createElement('div');
    host.className = 'pqrs';
    document.body.appendChild(host);
    var shadow = host.attachShadow({ mode: 'open' });
    var style = document.createElement('style');
    style.textContent = css;
    shadow.appendChild(style);
    var root = document.createElement('div');
    root.innerHTML = html;
    shadow.appendChild(root);

    var fab = shadow.querySelector('.fab');
    var panel = shadow.querySelector('.panel');
    var messages = shadow.querySelector('.messages');
    var input = shadow.querySelector('.input');
    var sendBtn = shadow.querySelector('.send-btn');
    var form = shadow.querySelector('#pqrs-form');
    var viewChat = shadow.querySelector('.view-chat');
    var viewForm = shadow.querySelector('.view-form');
    var viewSuccess = shadow.querySelector('.view-success');

    var waiting = false;
    var actionsShown = false;

    function showView(view) {
        viewChat.classList.toggle('hidden', view !== 'chat');
        viewForm.classList.toggle('hidden', view !== 'form');
        viewSuccess.classList.toggle('hidden', view !== 'success');
    }

    function setChatEnabled(enabled) {
        input.disabled = !enabled;
        sendBtn.disabled = !enabled;
    }

    function scroll() { messages.scrollTop = messages.scrollHeight; }

    function addMsg(role, text) {
        var d = document.createElement('div');
        d.className = 'msg ' + role;
        d.textContent = text;
        messages.appendChild(d);
        scroll();
    }

    function addTyping() {
        var d = document.createElement('div');
        d.className = 'msg bot';
        d.dataset.typing = '1';
        d.innerHTML = '<div class="typing"><span></span><span></span><span></span></div>';
        messages.appendChild(d);
        scroll();
    }
    function removeTyping() {
        var t = messages.querySelector('[data-typing]');
        if (t) t.remove();
    }
    function removeActions() {
        var a = messages.querySelector('.actions');
        if (a) a.remove();
    }

    async function api(path, body) {
        var res = await fetch(apiUrl + path, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json', 'X-Tenant-Api-Key': apiKey },
            body: JSON.stringify(body)
        });
        if (!res.ok) {
            var msg = 'Error ' + res.status;
            try { var j = await res.json(); msg = j.detail || j.title || msg; } catch (e) { /* ignore */ }
            var err = new Error(msg);
            err.status = res.status;
            throw err;
        }
        return res.json();
    }

    // Reintenta la búsqueda RAG ante errores de red o 503 (Gemini saturado).
    async function ragSearch(text) {
        var lastErr;
        for (var attempt = 0; attempt < 4; attempt++) {
            try {
                return await api('/widget/rag-search', { consulta: text });
            } catch (e) {
                lastErr = e;
                var retryable = e.status === 503 || !e.status; // 503 o error de red
                if (retryable && attempt < 3) {
                    await new Promise(function (r) { setTimeout(r, 1500 * (attempt + 1)); });
                    continue;
                }
                throw e;
            }
        }
        throw lastErr;
    }

    async function send(text) {
        if (waiting) return;
        waiting = true;
        removeActions();
        setChatEnabled(false);
        addMsg('user', text);
        addTyping();
        try {
            var r = await ragSearch(text);
            removeTyping();
            if (r.saludo) {
                addMsg('bot', r.respuesta);
                setChatEnabled(true); // saludo: seguir conversando, sin botones Sí/No
            } else if (r.encontrado && r.respuesta) {
                addMsg('bot', r.respuesta);
                showActions();
            } else {
                addMsg('bot', 'Lo siento, no tengo una respuesta automática para eso. Puedes probar con otra pregunta o radicar una solicitud para que un agente la atienda.');
                showSingleAction('Radicar una solicitud', ICON_DOC, goForm);
                setChatEnabled(true); // seguir preguntando
            }
        } catch (e) {
            removeTyping();
            addMsg('bot', 'Estoy teniendo problemas para conectarme. Puedes intentarlo de nuevo o radicar tu solicitud.');
            showSingleAction('Radicar una solicitud', ICON_DOC, goForm);
            setChatEnabled(true);
        } finally {
            waiting = false;
        }
    }

    function showSingleAction(label, icon, handler) {
        actionsShown = true;
        var d = document.createElement('div');
        d.className = 'actions';
        d.innerHTML = '<button type="button" class="btn-action no">' + icon + ' ' + label + '</button>';
        messages.appendChild(d);
        d.querySelector('button').addEventListener('click', function () {
            removeActions();
            actionsShown = false;
            handler();
        });
        scroll();
    }

    function showActions() {
        actionsShown = true;
        var d = document.createElement('div');
        d.className = 'actions';
        d.innerHTML =
            '<button type="button" class="btn-action yes">' + ICON_CHECK + ' Sí, resolvió mi duda</button>' +
            '<button type="button" class="btn-action no">' + ICON_DOC + ' No, quiero radicar una solicitud</button>';
        messages.appendChild(d);
        d.querySelector('.yes').addEventListener('click', onYes);
        d.querySelector('.no').addEventListener('click', onNo);
        scroll();
    }

    function onYes() {
        removeActions();
        actionsShown = false;
        addMsg('bot', '¡Genial! Me alegra haberte ayudado. Si necesitas algo más, aquí estoy.');
        setChatEnabled(true);
    }

    function onNo() {
        removeActions();
        actionsShown = false;
        goForm();
    }

    function goForm() {
        showView('form');
        form.clienteNombre.focus();
    }

    function openPanel() {
        fab.classList.add('hidden');
        panel.classList.remove('hidden');
        showView('chat');
        messages.innerHTML = '';
        setChatEnabled(true);
        addMsg('bot', greeting);
        input.focus();
    }

    function closePanel() {
        panel.classList.add('hidden');
        fab.classList.remove('hidden');
    }

    form.addEventListener('submit', async function (e) {
        e.preventDefault();
        var data = {
            clienteNombre: form.clienteNombre.value.trim(),
            clienteCorreo: form.clienteCorreo.value.trim(),
            asunto: form.asunto.value.trim(),
            descripcion: form.descripcion.value.trim()
        };
        var submitBtn = form.querySelector('.submit-btn');
        submitBtn.disabled = true;
        submitBtn.textContent = 'Enviando...';
        try {
            var r = await api('/widget/tickets', data);
            showView('success');
            shadow.querySelector('#radicado').textContent = r.numeroRadicado;
        } catch (err) {
            alert('No se pudo radicar tu solicitud: ' + err.message);
        } finally {
            submitBtn.disabled = false;
            submitBtn.innerHTML = ICON_SEND + ' Enviar';
        }
    });

    sendBtn.addEventListener('click', function () {
        var t = input.value.trim();
        if (!t) return;
        input.value = '';
        send(t);
    });
    input.addEventListener('keydown', function (e) {
        if (e.key === 'Enter' && !e.shiftKey) {
            e.preventDefault();
            var t = input.value.trim();
            if (!t) return;
            input.value = '';
            send(t);
        }
    });
    fab.addEventListener('click', openPanel);
    shadow.querySelector('.close-btn').addEventListener('click', closePanel);
    form.querySelector('.back-btn').addEventListener('click', function () {
        showView('chat');
    });
    shadow.querySelector('.done-btn').addEventListener('click', openPanel);
})();
