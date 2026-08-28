import { apiRequest, apiUpload } from './api.js';
import { showLoading, hideLoading, showToast } from './ui.js';

const Swal = window.Swal;
let articlesCache = [];

export async function render(container) {
    container.innerHTML = `
        <div class="flex flex-col md:flex-row md:items-center justify-between gap-4 mb-6">
            <div>
                <h2 class="text-2xl font-bold text-gray-900">Base de conocimiento</h2>
                <p class="text-sm text-gray-500 mt-1">Preguntas frecuentes usadas por el widget (RAG).</p>
            </div>
            <div class="flex gap-2">
                <button id="btn-importar" class="inline-flex items-center gap-2 bg-white border border-slate-300 text-gray-700 px-4 py-2 rounded-lg text-sm font-semibold hover:bg-gray-50 transition">
                    <i class="fas fa-file-upload text-[14px]"></i> Importar documento
                </button>
                <button id="btn-nuevo-articulo" class="inline-flex items-center gap-2 bg-blue-700 text-white px-4 py-2 rounded-lg text-sm font-semibold hover:bg-blue-800 transition shadow-sm">
                    <i class="fas fa-plus text-[14px]"></i> Nuevo artículo
                </button>
            </div>
        </div>
        <div id="kb-body"></div>`;

    document.getElementById('btn-nuevo-articulo').addEventListener('click', () => openArticleModal());
    document.getElementById('btn-importar').addEventListener('click', openImportModal);

    await loadArticles();
}

async function loadArticles() {
    const body = document.getElementById('kb-body');
    showLoading('kb-body');
    try {
        articlesCache = await apiRequest('/kb-articles');
        hideLoading('kb-body');
        renderTable(body);
    } catch (err) {
        hideLoading('kb-body');
        body.innerHTML = `<div class="bg-white rounded-xl border border-slate-200 p-8 text-center text-rose-600">No se pudieron cargar los artículos: ${err.message}</div>`;
    }
}

function renderTable(body) {
    if (articlesCache.length === 0) {
        body.innerHTML = `<div class="bg-white rounded-xl border border-slate-200 p-10 text-center text-gray-400">Aún no hay artículos en la base de conocimiento.</div>`;
        return;
    }
    body.innerHTML = `
        <div class="hidden md:block bg-white rounded-xl border border-slate-200 shadow-sm overflow-hidden">
            <div class="overflow-x-auto">
                <table class="w-full text-left">
                    <thead>
                        <tr class="bg-gray-50 border-b border-slate-200 text-xs uppercase tracking-wider text-gray-500">
                            <th class="px-4 py-3">Pregunta</th>
                            <th class="px-4 py-3">Respuesta</th>
                            <th class="px-4 py-3 text-right">Acciones</th>
                        </tr>
                    </thead>
                    <tbody class="divide-y divide-slate-100">
                        ${articlesCache.map(a => `
                            <tr class="hover:bg-gray-50 transition">
                                <td class="px-4 py-3 max-w-[240px]"><div class="font-medium text-gray-800">${a.pregunta || a.titulo || '—'}</div></td>
                                <td class="px-4 py-3 text-gray-600 max-w-[360px]"><div class="truncate">${a.respuesta}</div></td>
                                <td class="px-4 py-3 text-right whitespace-nowrap">
                                    <button class="text-blue-600 hover:text-blue-800 text-sm mr-3" onclick="window.editArticle('${a.id}')"><i class="fas fa-edit"></i></button>
                                    <button class="text-rose-600 hover:text-rose-800 text-sm" onclick="window.delArticle('${a.id}')"><i class="fas fa-trash"></i></button>
                                </td>
                            </tr>`).join('')}
                    </tbody>
                </table>
            </div>
        </div>
        <div class="md:hidden space-y-4">
            ${articlesCache.map(a => `
                <div class="bg-white rounded-xl border border-slate-200 shadow-sm p-4 space-y-2">
                    <div class="font-semibold text-gray-800">${a.pregunta || a.titulo || '—'}</div>
                    <div class="text-sm text-gray-600 line-clamp-2">${a.respuesta}</div>
                    <div class="flex gap-4 pt-1">
                        <button class="text-blue-600 text-sm" onclick="window.editArticle('${a.id}')"><i class="fas fa-edit"></i> Editar</button>
                        <button class="text-rose-600 text-sm" onclick="window.delArticle('${a.id}')"><i class="fas fa-trash"></i> Eliminar</button>
                    </div>
                </div>`).join('')}
        </div>`;
}

function openArticleModal(id) {
    const existing = id ? articlesCache.find(a => a.id === id) : null;
    Swal.fire({
        title: existing ? 'Editar artículo' : 'Nuevo artículo',
        html: `
            <div class="text-left space-y-3">
                <div><label class="block text-xs font-semibold text-gray-600 mb-1">Pregunta</label>
                    <textarea id="a-pregunta" rows="2" class="w-full px-3 py-2 border border-slate-300 rounded-lg text-sm focus:border-blue-600 outline-none">${existing?.pregunta || ''}</textarea></div>
                <div><label class="block text-xs font-semibold text-gray-600 mb-1">Respuesta</label>
                    <textarea id="a-respuesta" rows="4" class="w-full px-3 py-2 border border-slate-300 rounded-lg text-sm focus:border-blue-600 outline-none">${existing?.respuesta || ''}</textarea></div>
                <p class="text-xs text-gray-400"><i class="fas fa-info-circle"></i> Se generará un embedding para la búsqueda RAG del widget.</p>
            </div>`,
        showCancelButton: true,
        confirmButtonText: existing ? 'Guardar' : 'Crear',
        didOpen: () => {
            const btn = Swal.getConfirmButton();
            btn.disabled = true;
            btn.textContent = 'Generando embedding...';
            // Rehabilitar tras un pequeño delay (el embedding se genera en el backend).
            setTimeout(() => { btn.disabled = false; btn.textContent = existing ? 'Guardar' : 'Crear'; }, 100);
        },
        preConfirm: () => {
            const pregunta = document.getElementById('a-pregunta').value.trim();
            const respuesta = document.getElementById('a-respuesta').value.trim();
            if (!pregunta || !respuesta) {
                Swal.showValidationMessage('Pregunta y respuesta son obligatorias.');
                return false;
            }
            return { pregunta, respuesta };
        }
    }).then(async (res) => {
        if (!res.isConfirmed) return;
        try {
            if (existing) {
                await apiRequest(`/kb-articles/${existing.id}`, 'PUT', res.value);
                showToast('Artículo actualizado', 'success');
            } else {
                await apiRequest('/kb-articles', 'POST', res.value);
                showToast('Artículo creado', 'success');
            }
            await loadArticles();
        } catch (err) {
            showToast(err.message, 'error');
        }
    });
}

function openImportModal() {
    Swal.fire({
        title: 'Importar documento',
        html: `
            <div class="text-left space-y-3">
                <p class="text-sm text-gray-600">Sube un archivo con la documentación de tu empresa. Se troceará automáticamente y cada fragmento se indexará para el widget (RAG).</p>
                <div><label class="block text-xs font-semibold text-gray-600 mb-1">Archivo (.txt, .md, .pdf, .docx)</label>
                    <input id="imp-archivo" type="file" accept=".txt,.md,.pdf,.docx" class="w-full text-sm border border-slate-300 rounded-lg p-2">
                </div>
                <p class="text-xs text-gray-400"><i class="fas fa-info-circle"></i> Máx. 5 MB · hasta 150 fragmentos. El archivo no se guarda, solo se procesa.</p>
            </div>`,
        showCancelButton: true,
        confirmButtonText: 'Importar',
        preConfirm: () => {
            const input = document.getElementById('imp-archivo');
            if (!input.files || input.files.length === 0) {
                Swal.showValidationMessage('Selecciona un archivo.');
                return false;
            }
            const file = input.files[0];
            if (file.size > 5 * 1024 * 1024) {
                Swal.showValidationMessage('El archivo supera el límite de 5 MB.');
                return false;
            }
            return file;
        }
    }).then(async (res) => {
        if (!res.isConfirmed) return;
        const file = res.value;
        const formData = new FormData();
        formData.append('archivo', file);

        Swal.fire({
            title: 'Importando...',
            html: '<p class="text-sm text-gray-600">Troceando y generando embeddings. Esto puede tardar unos segundos.</p>',
            allowOutsideClick: false,
            didOpen: () => Swal.showLoading()
        });

        try {
            const data = await apiUpload('/kb-articles/import', formData);
            Swal.close();
            showToast(data.mensaje || 'Documento importado', 'success');
            await loadArticles();
        } catch (err) {
            Swal.close();
            showToast(err.message, 'error');
        }
    });
}

export async function deleteArticle(id) {
    const conf = await Swal.fire({
        title: 'Eliminar artículo',
        text: '¿Seguro que deseas eliminar este artículo de la base de conocimiento?',
        icon: 'warning',
        showCancelButton: true,
        confirmButtonText: 'Sí, eliminar',
        confirmButtonColor: '#e11d48'
    });
    if (!conf.isConfirmed) return;
    try {
        await apiRequest(`/kb-articles/${id}`, 'DELETE');
        showToast('Artículo eliminado', 'success');
        await loadArticles();
    } catch (err) {
        showToast(err.message, 'error');
    }
}

window.editArticle = (id) => openArticleModal(id);
window.delArticle = (id) => deleteArticle(id);
