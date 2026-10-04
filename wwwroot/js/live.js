// Live updates: on every "changed" message the page re-fetches itself and swaps <main id="live-root">.
// While the user is dragging, typing or has a modal open the refresh waits.
(function () {
    const TT = window.TT;
    const me = +document.body.dataset.user;
    const root = () => document.getElementById('live-root');
    let busy = 0, pending = false, inflight = false, flashId = null;

    // درگ‌اند‌دراپ و کارهای در جریان، به‌روزرسانی را عقب می‌اندازند
    TT.busy = on => {
        busy = Math.max(0, busy + (on ? 1 : -1));
        if (!busy && pending) TT.refresh();
    };

    function blocked() {
        if (busy > 0 || document.querySelector('.modal.show, .dropdown-menu.show, .jdate.open')) return true;
        const a = document.activeElement;
        // تایپ کردن در فیلدها نباید با به‌روزرسانی قطع شود (چک‌باکس‌ها مشکلی ندارند)
        return !!(a && a.matches('textarea, select, input:not([type=checkbox]):not([type=radio])') && root()?.contains(a));
    }

    TT.refresh = async function (opts = {}) {
        if (opts.flash) flashId = opts.flash;
        if (blocked() || inflight) { pending = true; return; }
        inflight = true; pending = false;
        try {
            const res = await fetch(location.href, { headers: { 'X-Live': '1' }, cache: 'no-store' });
            if (!res.ok || res.redirected) {
                // مثلاً تسکی که رویش بودیم حذف شده
                location.href = '/';
                return;
            }
            const doc = new DOMParser().parseFromString(await res.text(), 'text/html');
            const bell = doc.getElementById('live-bell');
            if (bell && document.getElementById('live-bell')) document.getElementById('live-bell').innerHTML = bell.innerHTML;
            const fresh = doc.getElementById('live-root');
            if (fresh && root()) {
                root().innerHTML = fresh.innerHTML;
                TT.runUpdateHooks(root());
                if (flashId) {
                    document.querySelectorAll(`[data-id="${flashId}"]`).forEach(el => {
                        el.classList.remove('flash'); void el.offsetWidth; el.classList.add('flash');
                    });
                    flashId = null;
                }
            }
        } catch { /* شبکه قطع است؛ دفعه‌ی بعد */ }
        finally {
            inflight = false;
            if (pending) setTimeout(() => TT.refresh(), 60);
        }
    };

    // بعد از بسته شدن مودال، اگر تغییری در صف بود اعمال شود
    document.addEventListener('hidden.bs.modal', () => { if (pending) TT.refresh(); });
    document.addEventListener('hidden.bs.dropdown', () => { if (pending) TT.refresh(); });
    document.addEventListener('focusout', () => setTimeout(() => { if (pending) TT.refresh(); }, 50));

    // اتصال SignalR
    const dot = document.getElementById('liveDot');
    function setState(state) {
        if (!dot) return;
        dot.dataset.state = state;
        dot.title = { live: 'به‌روزرسانی زنده فعاله', connecting: 'در حال اتصال…', offline: 'اتصال زنده قطعه؛ هر ۳۰ ثانیه به‌روز می‌شه' }[state];
    }

    if (!window.signalR) return;

    const conn = new signalR.HubConnectionBuilder()
        .withUrl('/hubs/board')
        .withAutomaticReconnect([0, 2000, 5000, 10000, 20000])
        .configureLogging(signalR.LogLevel.None)
        .build();

    conn.on('changed', ch => {
        if (ch.actorId === me) return;            // تغییر خودم قبلاً اعمال شده
        TT.toast(ch.message, 'info');
        if (ch.kind === 'approved' && ch.assigneeId === me) TT.confetti();
        TT.refresh({ flash: ch.taskId });
    });

    conn.on('reload', () => location.reload());

    let poll = null;
    const startPolling = () => { if (!poll) poll = setInterval(() => TT.refresh(), 30000); };
    const stopPolling = () => { clearInterval(poll); poll = null; };

    conn.onreconnecting(() => setState('connecting'));
    conn.onreconnected(() => { setState('live'); stopPolling(); TT.refresh(); });
    conn.onclose(() => { setState('offline'); startPolling(); retry(); });

    async function retry() {
        try {
            await conn.start();
            setState('live');
            stopPolling();
        } catch {
            setState('offline');
            startPolling();
            setTimeout(retry, 15000);
        }
    }
    setState('connecting');
    retry();
})();
