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
            <i class="fas fa-spinner fa-spin text-3xl"></i>
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
        slate: 'bg-gray-100 text-gray-600 border-gray-200',
        green: 'bg-emerald-50 text-emerald-700 border-emerald-200',
        red: 'bg-rose-50 text-rose-700 border-rose-200',
        amber: 'bg-amber-50 text-amber-700 border-amber-200',
        indigo: 'bg-blue-50 text-blue-700 border-blue-200'
    };
    return `<span class="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-xs font-semibold border ${colors[kind] || colors.slate}">
        <span class="w-1.5 h-1.5 rounded-full bg-current"></span>${text}</span>`;
}

export function toggleSidebar() {
    const sb = document.getElementById('sidebar');
    sb.classList.toggle('-translate-x-full');
}
export function hideSidebar() {
    document.getElementById('sidebar').classList.add('-translate-x-full');
}
