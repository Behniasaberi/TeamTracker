// Shared helpers (window.TT): toasts, POST with antiforgery token, theme, confetti, tooltips, live clocks
(function () {
    const TT = window.TT = window.TT || {};
    const updateHooks = [];

    // ارقام لاتین → فارسی
    TT.fa = n => String(n).replace(/\d/g, d => '۰۱۲۳۴۵۶۷۸۹'[d]).replace(/\./g, '٫');

    // تابعی که بعد از هر به‌روزرسانی زنده‌ی صفحه دوباره اجرا شود
    TT.onUpdate = fn => { updateHooks.push(fn); fn(document); };
    TT.runUpdateHooks = root => updateHooks.forEach(fn => fn(root || document));

    // toast
    const icons = {
        ok: '<path d="M20 6 9 17l-5-5"/>',
        err: '<path d="M18 6 6 18M6 6l12 12"/>',
        info: '<circle cx="12" cy="12" r="9"/><path d="M12 8h.01M12 11v5"/>'
    };
    TT.toast = function (msg, type = 'ok') {
        if (!msg) return;
        const stack = document.getElementById('toasts');
        const el = document.createElement('div');
        el.className = `toast-x ${type}`;
        el.setAttribute('role', type === 'err' ? 'alert' : 'status');
        el.innerHTML = `<span class="ti"><svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="#fff" stroke-width="2.5" stroke-linecap="round" stroke-linejoin="round">${icons[type] || icons.ok}</svg></span><span></span>`;
        el.lastChild.textContent = msg;
        stack.appendChild(el);
        while (stack.children.length > 4) stack.firstChild.remove();
        setTimeout(() => { el.classList.add('hide'); setTimeout(() => el.remove(), 300); }, type === 'err' ? 6000 : 4000);
    };
    window.toast = TT.toast;

    // POST با توکن ضد جعل
    TT.post = async function (url, data) {
        const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value;
        const body = data instanceof FormData ? new URLSearchParams(data) : new URLSearchParams(data || {});
        let res;
        try {
            res = await fetch(url, {
                method: 'POST',
                headers: { 'RequestVerificationToken': token, 'X-Requested-With': 'fetch' },
                body
            });
        } catch {
            return { ok: false, message: 'ارتباط با سرور برقرار نشد.' };
        }
        let json = {};
        try { json = await res.json(); } catch { /* پاسخ غیر JSON */ }
        return { ...json, ok: res.ok && json.ok !== false, message: json.message || (res.ok ? '' : 'خطایی رخ داد.') };
    };
    window.postForm = TT.post;

    // تم روشن / تاریک
    document.addEventListener('click', e => {
        if (!e.target.closest('.js-theme')) return;
        const next = document.documentElement.dataset.theme === 'dark' ? 'light' : 'dark';
        document.documentElement.dataset.theme = next;
        try { localStorage.setItem('tt-theme', next); } catch { /* حالت خصوصی مرورگر */ }
    });

    // کنفتی
    TT.confetti = function () {
        const cv = document.getElementById('confetti');
        if (!cv || matchMedia('(prefers-reduced-motion: reduce)').matches) return;
        const ctx = cv.getContext('2d');
        const W = cv.width = innerWidth, H = cv.height = innerHeight;
        const colors = ['#5b4cf0', '#8b5cf6', '#10b981', '#f59e0b', '#f43f5e', '#0ea5e9'];
        const parts = Array.from({ length: 160 }, () => ({
            x: W / 2 + (Math.random() - .5) * 120, y: H * .35,
            vx: (Math.random() - .5) * 16, vy: -Math.random() * 14 - 4,
            s: Math.random() * 7 + 4, r: Math.random() * Math.PI, vr: (Math.random() - .5) * .3,
            c: colors[(Math.random() * colors.length) | 0]
        }));
        cv.classList.add('on');
        let frame = 0;
        (function draw() {
            ctx.clearRect(0, 0, W, H);
            parts.forEach(p => {
                p.vy += .38; p.vx *= .99; p.x += p.vx; p.y += p.vy; p.r += p.vr;
                ctx.save(); ctx.translate(p.x, p.y); ctx.rotate(p.r);
                ctx.fillStyle = p.c; ctx.fillRect(-p.s / 2, -p.s / 4, p.s, p.s / 2);
                ctx.restore();
            });
            if (++frame < 150) requestAnimationFrame(draw);
            else { ctx.clearRect(0, 0, W, H); cv.classList.remove('on'); }
        })();
    };

    // تولتیپ ساده برای [data-tip] (ویجت‌ها)
    const tip = document.createElement('div');
    tip.className = 'tt-tip';
    tip.setAttribute('role', 'tooltip');
    document.addEventListener('DOMContentLoaded', () => document.body.appendChild(tip));
    function showTip(el) {
        tip.textContent = el.dataset.tip;
        tip.classList.add('on');
        const r = el.getBoundingClientRect(), t = tip.getBoundingClientRect();
        let left = r.left + r.width / 2 - t.width / 2;
        left = Math.max(8, Math.min(left, innerWidth - t.width - 8));
        const top = r.top - t.height - 8 < 8 ? r.bottom + 8 : r.top - t.height - 8;
        tip.style.transform = `translate(${left}px, ${top}px)`;
    }
    const hideTip = () => tip.classList.remove('on');
    document.addEventListener('pointerover', e => { const el = e.target.closest('[data-tip]'); el ? showTip(el) : hideTip(); });
    document.addEventListener('focusin', e => { const el = e.target.closest('[data-tip]'); if (el) showTip(el); });
    document.addEventListener('focusout', hideTip);
    addEventListener('scroll', hideTip, { passive: true });

    // ساعت‌های زنده (تایمرها)
    let clocks = [];
    const pad = n => String(n).padStart(2, '0');
    const fmt = s => TT.fa(`${pad(Math.floor(s / 3600))}:${pad(Math.floor(s / 60) % 60)}:${pad(s % 60)}`);
    TT.onUpdate(root => {
        clocks = [...document.querySelectorAll('[data-elapsed]')].map(el => ({
            el: el.querySelector('.js-clock'),
            start: Date.now() - (+el.dataset.elapsed || 0) * 1000
        })).filter(c => c.el);
    });
    setInterval(() => clocks.forEach(c => c.el.textContent = fmt(Math.floor((Date.now() - c.start) / 1000))), 1000);

    // تأیید حذف
    document.addEventListener('submit', e => {
        if (e.target.matches('.js-confirm-delete') && !window.confirm(e.target.dataset.confirm || 'این تسک حذف شود؟')) e.preventDefault();
    });

    // اعلان‌ها
    document.addEventListener('click', async e => {
        if (!e.target.closest('.js-read-all')) return;
        await TT.post('/Notifications/ReadAll');
        document.querySelectorAll('.bell-item.unread').forEach(el => el.classList.remove('unread'));
        document.querySelector('.bell-badge')?.remove();
        e.target.closest('.js-read-all').remove();
    });

    // بازه‌ی سریع گزارش (این ماه / ۳۰ روز / این هفته)
    document.addEventListener('click', e => {
        const b = e.target.closest('.js-quick-range button');
        if (!b || !window.Jalali) return;
        const iso = d => { const x = new Date(d); x.setMinutes(x.getMinutes() - x.getTimezoneOffset()); return x.toISOString().slice(0, 10); };
        const today = new Date();
        let from = new Date();
        if (b.dataset.range === 'month') {
            const j = Jalali.isoToJ(iso(today));
            from = new Date(Jalali.jToIso(j.jy, j.jm, 1) + 'T12:00:00');
        } else if (b.dataset.range === 'week') {
            from.setDate(today.getDate() - ((today.getDay() + 1) % 7));
        } else {
            from.setDate(today.getDate() - (+b.dataset.range - 1));
        }
        const f = b.closest('form');
        const set = (name, v) => { const el = f.querySelector(`[name="${name}"]`); el.value = v; el.dispatchEvent(new Event('change', { bubbles: true })); };
        set('from', iso(from));
        set('to', iso(today));
    });
    document.addEventListener('submit', e => {
        if (e.target.id !== 'exportForm') return;
        setTimeout(() => bootstrap.Modal.getInstance(e.target.closest('.modal'))?.hide(), 400);
        TT.toast('فایل اکسل در حال دانلوده…', 'info');
    });

    // پیام‌های سرور (TempData)
    document.addEventListener('DOMContentLoaded', () => {
        const s = document.getElementById('toasts');
        if (s) { TT.toast(s.dataset.ok, 'ok'); TT.toast(s.dataset.err, 'err'); }
    });
})();
