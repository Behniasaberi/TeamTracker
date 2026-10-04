// Jalali date picker for <input type="date" data-jalali>.
// The input keeps its Gregorian yyyy-MM-dd value so model binding stays the same.
// Conversion functions are ported from jalaali-js (https://github.com/jalaali/jalaali-js, MIT).
(function () {
    // تبدیل تاریخ
    const div = (a, b) => ~~(a / b);
    const mod = (a, b) => a - ~~(a / b) * b;
    const BREAKS = [-61, 9, 38, 199, 426, 686, 756, 818, 1111, 1181, 1210, 1635, 2060, 2097, 2192, 2262, 2324, 2394, 2456, 3178];

    function jalCal(jy) {
        const gy = jy + 621;
        let leapJ = -14, jp = BREAKS[0], jm, jump = 0, n, i;
        for (i = 1; i < BREAKS.length; i++) {
            jm = BREAKS[i];
            jump = jm - jp;
            if (jy < jm) break;
            leapJ += div(jump, 33) * 8 + div(mod(jump, 33), 4);
            jp = jm;
        }
        n = jy - jp;
        leapJ += div(n, 33) * 8 + div(mod(n, 33) + 3, 4);
        if (mod(jump, 33) === 4 && jump - n === 4) leapJ += 1;
        const leapG = div(gy, 4) - div((div(gy, 100) + 1) * 3, 4) - 150;
        const march = 20 + leapJ - leapG;
        if (jump - n < 6) n = n - jump + div(jump + 4, 33) * 33;
        let leap = mod(mod(n + 1, 33) - 1, 4);
        if (leap === -1) leap = 4;
        return { leap, gy, march };
    }
    function g2d(gy, gm, gd) {
        let d = div((gy + div(gm - 8, 6) + 100100) * 1461, 4) + div(153 * mod(gm + 9, 12) + 2, 5) + gd - 34840408;
        return d - div(div(gy + 100100 + div(gm - 8, 6), 100) * 3, 4) + 752;
    }
    function d2g(jdn) {
        let j = 4 * jdn + 139361631;
        j = j + div(div(4 * jdn + 183187720, 146097) * 3, 4) * 4 - 3908;
        const i = div(mod(j, 1461), 4) * 5 + 308;
        const gd = div(mod(i, 153), 5) + 1;
        const gm = mod(div(i, 153), 12) + 1;
        const gy = div(j, 1461) - 100100 + div(8 - gm, 6);
        return { gy, gm, gd };
    }
    function j2d(jy, jm, jd) {
        const r = jalCal(jy);
        return g2d(r.gy, 3, r.march) + (jm - 1) * 31 - div(jm, 7) * (jm - 7) + jd - 1;
    }
    function d2j(jdn) {
        const gy = d2g(jdn).gy;
        let jy = gy - 621;
        const r = jalCal(jy);
        let k = jdn - g2d(gy, 3, r.march);
        if (k >= 0) {
            if (k <= 185) return { jy, jm: 1 + div(k, 31), jd: mod(k, 31) + 1 };
            k -= 186;
        } else {
            jy -= 1;
            k += 179;
            if (r.leap === 1) k += 1;
        }
        return { jy, jm: 7 + div(k, 30), jd: mod(k, 30) + 1 };
    }
    const monthLen = (jy, jm) => jm <= 6 ? 31 : jm <= 11 ? 30 : (jalCal(jy).leap === 0 ? 30 : 29);

    const pad = n => String(n).padStart(2, '0');
    const isoToJ = iso => { const [y, m, d] = iso.split('-').map(Number); return d2j(g2d(y, m, d)); };
    const jToIso = (jy, jm, jd) => { const g = d2g(j2d(jy, jm, jd)); return `${g.gy}-${pad(g.gm)}-${pad(g.gd)}`; };
    const todayIso = () => { const d = new Date(); return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}`; };
    // روز هفته با شروع از شنبه (۰ = شنبه)
    const satIndex = iso => (new Date(iso + 'T12:00:00').getDay() + 1) % 7;

    const MONTHS = ['فروردین', 'اردیبهشت', 'خرداد', 'تیر', 'مرداد', 'شهریور', 'مهر', 'آبان', 'آذر', 'دی', 'بهمن', 'اسفند'];
    const DAYS = ['شنبه', 'یکشنبه', 'دوشنبه', 'سه‌شنبه', 'چهارشنبه', 'پنجشنبه', 'جمعه'];
    const fa = n => String(n).replace(/\d/g, d => '۰۱۲۳۴۵۶۷۸۹'[d]);

    window.Jalali = { isoToJ, jToIso, monthLen, format: iso => { const j = isoToJ(iso); return `${fa(j.jd)} ${MONTHS[j.jm - 1]} ${fa(j.jy)}`; } };

    // انتخابگر
    function enhance(input) {
        if (input.dataset.jdateReady) return;
        input.dataset.jdateReady = '1';
        const optional = input.hasAttribute('data-optional');
        const min = input.getAttribute('min'), max = input.getAttribute('max');

        const wrap = document.createElement('div');
        wrap.className = 'jdate';
        input.parentNode.insertBefore(wrap, input);
        wrap.appendChild(input);
        input.type = 'hidden';

        const display = document.createElement('button');
        display.type = 'button';
        display.className = 'form-control jdate-display';
        display.setAttribute('aria-haspopup', 'dialog');
        wrap.appendChild(display);

        const clear = document.createElement('button');
        clear.type = 'button';
        clear.className = 'jdate-clear';
        clear.setAttribute('aria-label', 'پاک کردن تاریخ');
        clear.textContent = '×';
        if (optional) wrap.appendChild(clear);

        const pop = document.createElement('div');
        pop.className = 'jdate-pop';
        pop.setAttribute('role', 'dialog');
        pop.setAttribute('aria-label', 'انتخاب تاریخ');
        wrap.appendChild(pop);

        let view; // { jy, jm } ماه در حال نمایش

        function sync() {
            const v = input.value;
            display.innerHTML = v
                ? `<span class="jd-day">${DAYS[satIndex(v)]}</span> ${window.Jalali.format(v)}`
                : '<span class="text-muted">انتخاب تاریخ</span>';
            clear.hidden = !v;
        }

        function render() {
            const { jy, jm } = view;
            const firstIso = jToIso(jy, jm, 1);
            const lead = satIndex(firstIso);
            const len = monthLen(jy, jm);
            const today = todayIso();
            let html = `<div class="jd-head">
                <button type="button" class="jd-nav" data-step="-1" aria-label="ماه قبل">‹</button>
                <b>${MONTHS[jm - 1]} ${fa(jy)}</b>
                <button type="button" class="jd-nav" data-step="1" aria-label="ماه بعد">›</button>
            </div><div class="jd-grid">`;
            html += ['ش', 'ی', 'د', 'س', 'چ', 'پ', 'ج'].map(d => `<span class="jd-wd">${d}</span>`).join('');
            for (let i = 0; i < lead; i++) html += '<span></span>';
            for (let d = 1; d <= len; d++) {
                const iso = jToIso(jy, jm, d);
                const off = (min && iso < min) || (max && iso > max);
                const cls = ['jd-day-btn', iso === input.value ? 'sel' : '', iso === today ? 'today' : '', (lead + d) % 7 === 0 ? 'fri' : ''].join(' ');
                html += `<button type="button" class="${cls}" data-iso="${iso}" ${off ? 'disabled' : ''}>${fa(d)}</button>`;
            }
            html += `</div><div class="jd-foot">
                <button type="button" class="jd-today" ${(min && today < min) || (max && today > max) ? 'disabled' : ''}>امروز</button>
                ${optional ? '<button type="button" class="jd-none">بدون تاریخ</button>' : ''}
            </div>`;
            pop.innerHTML = html;
        }

        function open() {
            const j = isoToJ(input.value || todayIso());
            view = { jy: j.jy, jm: j.jm };
            render();
            wrap.classList.add('open');
            // اگر پایین جا نیست، بالای فیلد باز شود
            const r = wrap.getBoundingClientRect();
            wrap.classList.toggle('up', innerHeight - r.bottom < 340 && r.top > 340);
            (pop.querySelector('.sel') || pop.querySelector('.today') || pop.querySelector('.jd-day-btn:not([disabled])'))?.focus();
        }
        const close = () => wrap.classList.remove('open');
        function pick(iso) {
            input.value = iso;
            input.dispatchEvent(new Event('change', { bubbles: true }));
            close();
            display.focus();
        }

        display.addEventListener('click', () => wrap.classList.contains('open') ? close() : open());
        clear.addEventListener('click', () => pick(''));
        pop.addEventListener('click', e => {
            const b = e.target.closest('button');
            if (!b) return;
            if (b.dataset.step) {
                let { jy, jm } = view;
                jm += +b.dataset.step;
                if (jm < 1) { jm = 12; jy--; } else if (jm > 12) { jm = 1; jy++; }
                view = { jy, jm };
                render();
            }
            else if (b.dataset.iso) pick(b.dataset.iso);
            else if (b.classList.contains('jd-today')) pick(todayIso());
            else if (b.classList.contains('jd-none')) pick('');
        });
        pop.addEventListener('keydown', e => { if (e.key === 'Escape') { e.stopPropagation(); close(); display.focus(); } });
        document.addEventListener('click', e => { if (!wrap.contains(e.target)) close(); });
        input.addEventListener('change', sync);
        sync();
    }

    const init = root => (root || document).querySelectorAll('input[type=date][data-jalali]').forEach(enhance);
    if (window.TT?.onUpdate) TT.onUpdate(init); else init();
})();
