const Swal = window.Swal;

export function showSection(id) {
    document.querySelectorAll('[data-section]').forEach(el => {
        el.classList.toggle('hidden', el.id !== id);
    });
}

export function showLoading(targetId, message = 'Cargando...') {
    const target = document.getElementById(targetId);
    if (!target) return;
    target.innerHTML = `
        <div class="flex flex-col items-center justify-center py-16 text-slate-400">
            <span class="material-symbols-outlined animate-spin text-4xl">progress_activity</span>
            <p class="mt-3 text-sm font-medium">${message}</p>
        </div>`;
}

export function hideLoading(targetId) {
    const target = document.getElementById(targetId);
    if (target) target.innerHTML = '';
}

export function showToast(title, icon = 'success') {
    Swal.mixin({
        toast: true,
        position: 'top-end',
        showConfirmButton: false,
        timer: 2600,
        timerProgressBar: true
    }).fire({ title, icon });
}

export function badge(text, kind = 'slate') {
    const colors = {
        slate: 'bg-slate-100 text-slate-600 border-slate-200',
        green: 'bg-emerald-50 text-emerald-700 border-emerald-200',
        red: 'bg-rose-50 text-rose-700 border-rose-200',
        amber: 'bg-amber-50 text-amber-700 border-amber-200',
        indigo: 'bg-indigo-50 text-indigo-700 border-indigo-200'
    };
    return `<span class="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-xs font-semibold border ${colors[kind] || colors.slate}">
        <span class="w-1.5 h-1.5 rounded-full ${colors[kind] ? 'bg-current' : ''}"></span>${text}</span>`;
}
