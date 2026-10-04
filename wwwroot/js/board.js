// Board + task actions for both roles. The rules here are only for UX; TaskService enforces them again.
(function () {
    const TT = window.TT;
    const isLeader = document.body.dataset.role === 'leader';
    const $ = id => document.getElementById(id);
    const cardOf = el => el.closest('[data-id]');
    const refresh = flash => (TT.refresh ? TT.refresh({ flash }) : location.reload());
    const modal = id => $(id) ? bootstrap.Modal.getOrCreateInstance($(id)) : null;
    const todayIso = () => { const d = new Date(); d.setMinutes(d.getMinutes() - d.getTimezoneOffset()); return d.toISOString().slice(0, 10); };

    function setDate(input, iso) {
        input.value = iso;
        input.dispatchEvent(new Event('change', { bubbles: true }));
    }

    // مودال‌ها

    function openLog(card) {
        const f = $('logForm');
        if (!f) return;
        f.action = `/Member/LogWork/${card.dataset.id}`;
        f.reset();
        setDate(f.querySelector('[name="NewLog.Date"]'), todayIso());
        f.querySelector('.js-task-title').textContent = card.dataset.title;
        modal('logModal').show();
        setTimeout(() => f.querySelector('.js-hours').focus(), 300);
    }

    function openDone(card) {
        const f = $('doneForm');
        if (!f) return;
        f.action = `/Member/Complete/${card.dataset.id}`;
        f.reset();
        f.querySelector('.js-done-submit').disabled = true;
        f.querySelector('.js-task-title').textContent = card.dataset.title;

        const hours = parseFloat(card.dataset.hours || '0');
        const running = card.classList.contains('running') || !!document.querySelector('.timer-box.on');
        const todayInput = f.querySelector('input[name="Complete.TodayHours"]');
        const info = f.querySelector('.js-hours-info');
        const label = f.querySelector('.js-today-label');
        if (hours > 0 || running) {
            info.innerHTML = `تا الان <b>${TT.fa(hours)} ساعت</b> روی این تسک ثبت کردی.` +
                (running ? ' <span class="text-review">تایمر روشنه و موقع ثبت، زمانش خودکار اضافه می‌شه.</span>' : '');
            todayInput.required = false;
            todayInput.min = 0;
            label.innerHTML = 'ساعت کار امروز <span class="text-muted fw-normal">(اختیاری)</span>';
            todayInput.placeholder = 'اگر امروز ساعتی ثبت نکردی';
        } else {
            info.innerHTML = '<span class="text-danger">هنوز ساعتی ثبت نکردی؛ ساعت کار رو وارد کن.</span>';
            todayInput.required = true;
            todayInput.min = 0.25;
            label.innerHTML = 'چند ساعت روی این کار وقت گذاشتی؟';
            todayInput.placeholder = 'مثلاً 4';
        }
        modal('doneModal').show();
    }

    function openApprove(card) {
        const f = $('approveForm');
        if (!f) return;
        f.action = `/Leader/Approve/${card.dataset.id}`;
        f.reset();
        f.querySelector('.js-task-title').textContent = card.dataset.title;
        const s = f.querySelector('.js-summary');
        s.innerHTML = '';
        const line = document.createElement('div');
        line.innerHTML = `<b></b> در <b>${TT.fa(card.dataset.days || 0)} روز</b> مجموعاً <b>${TT.fa(card.dataset.hours || 0)} ساعت</b> کار کرده.`;
        line.querySelector('b').textContent = card.dataset.owner || 'عضو تیم';
        s.appendChild(line);
        if (card.dataset.note) {
            const q = document.createElement('div');
            q.className = 'quote';
            q.textContent = `«${card.dataset.note}»`;
            s.appendChild(q);
        }
        modal('approveModal').show();
    }

    function openReject(card) {
        const f = $('rejectForm');
        if (!f) return;
        f.action = `/Leader/Reject/${card.dataset.id}`;
        f.reset();
        f.querySelector('.js-task-title').textContent = card.dataset.title;
        modal('rejectModal').show();
        setTimeout(() => f.querySelector('textarea').focus(), 300);
    }

    // کلیک‌ها

    async function quick(url, card, btn) {
        btn && (btn.disabled = true);
        const r = await TT.post(url);
        btn && (btn.disabled = false);
        TT.toast(r.message, r.ok ? 'ok' : 'err');
        if (r.ok) refresh(card?.dataset.id);
    }

    document.addEventListener('click', e => {
        const t = e.target;
        let el;
        if ((el = t.closest('.js-tick'))) openDone(cardOf(el));
        else if ((el = t.closest('.js-log'))) openLog(cardOf(el));
        else if ((el = t.closest('.js-approve'))) openApprove(cardOf(el));
        else if ((el = t.closest('.js-reject'))) openReject(cardOf(el));
        else if ((el = t.closest('.js-timer-start'))) quick(`/Member/TimerStart/${cardOf(el).dataset.id}`, cardOf(el), el);
        else if ((el = t.closest('.js-timer-stop'))) quick(`/Member/TimerStop/${cardOf(el).dataset.id}`, cardOf(el), el);
        else if ((el = t.closest('.quick-hours button[data-h]'))) {
            const input = el.closest('.mb-3, [class*="col-"]').querySelector('.js-hours');
            input.value = el.dataset.h;
            input.focus();
        }
        else if ((el = t.closest('.js-quick-date button'))) {
            const d = new Date(); d.setDate(d.getDate() + (+el.dataset.d));
            d.setMinutes(d.getMinutes() - d.getTimezoneOffset());
            setDate(el.closest('[class*="col-"]').querySelector('input[name="NewLog.Date"]'), d.toISOString().slice(0, 10));
        }
    });

    // تیک تأیید در مودال «انجام شد»
    document.addEventListener('change', async e => {
        const t = e.target;
        if (t.matches('.js-confirm')) {
            t.closest('form').querySelector('.js-done-submit').disabled = !t.checked;
        }
        else if (t.matches('.js-check')) {
            const list = t.closest('.checklist');
            t.closest('li').classList.toggle('done', t.checked);
            const r = await TT.post(`/Member/Check/${list.dataset.id}`, { index: t.dataset.index, done: t.checked });
            if (!r.ok) {
                t.checked = !t.checked;
                t.closest('li').classList.toggle('done', t.checked);
                TT.toast(r.message, 'err');
            } else {
                refresh();
            }
        }
    });

    // فرم‌های مودال (ارسال بدون رفرش)

    document.addEventListener('submit', async e => {
        const f = e.target;
        if (!f.matches('.js-ajax-form')) return;
        e.preventDefault();
        if (!f.checkValidity()) { f.reportValidity(); return; }

        const btn = f.querySelector('button:not([type=button])');
        btn.disabled = true;
        const r = await TT.post(f.action, new FormData(f));
        btn.disabled = f.id === 'doneForm' && !f.querySelector('.js-confirm').checked;

        if (!r.ok) { TT.toast(r.message, 'err'); return; }
        const m = f.closest('.modal');
        if (m) bootstrap.Modal.getInstance(m)?.hide();
        else { f.reset(); document.activeElement?.blur(); }
        if (r.kind !== 'commented') TT.toast(r.message, 'ok');
        if (r.kind === 'approved' || r.kind === 'submitted') TT.confetti();
        refresh(r.taskId);
    });

    // ارسال نظر با Ctrl+Enter و بزرگ شدن خودکار کادر
    document.addEventListener('keydown', e => {
        if (e.key === 'Enter' && (e.ctrlKey || e.metaKey) && e.target.matches('.chat-form textarea')) {
            e.preventDefault();
            e.target.form.requestSubmit();
        }
    });
    document.addEventListener('input', e => {
        if (!e.target.matches('.chat-form textarea')) return;
        e.target.style.height = 'auto';
        e.target.style.height = Math.min(e.target.scrollHeight, 160) + 'px';
    });

    // درگ‌اند‌دراپ

    /**
     * آیا کارت از ستون from به ستون to مجاز است؟
     * خروجی: true یا متن دلیل
     */
    function canMove(from, to, card) {
        if (from === to) return true;
        const logs = +card.dataset.logs;
        const backToStart = to === 'NotStarted' && (logs > 0 || card.classList.contains('running'))
            ? 'ساعت ثبت شده؛ دیگه «شروع نشده» نیست' : true;

        if (isLeader) {
            if (from === 'Review') return to === 'Done' || to === 'InProgress' ? true : 'اول تأیید یا برگشت';
            if (to === 'Review' || to === 'Done') return 'اول باید خود عضو تیک بزنه';
            return backToStart;
        }
        if (from === 'Review' || from === 'Done') return 'منتظر تأیید تیم‌لید';
        return backToStart;
    }

    let sortables = [];
    let hoverChip = null;

    function trackPointer(e) {
        const p = e.touches ? e.touches[0] : e;
        const el = document.elementFromPoint(p.clientX, p.clientY);
        const chip = el && el.closest('.drop-chips a[data-member]');
        if (chip !== hoverChip) {
            hoverChip?.classList.remove('drop-hover');
            hoverChip = chip;
            hoverChip?.classList.add('drop-hover');
        }
    }

    async function reassign(card, chip) {
        if (chip.dataset.member === card.dataset.assignee) return;
        const st = card.dataset.status;
        if (st !== 'NotStarted' && st !== 'InProgress') return TT.toast('تسکی که تیک خورده رو نمی‌شه به نفر دیگه سپرد.', 'err');
        if (+card.dataset.logs > 0) return TT.toast('روی این تسک ساعت ثبت شده؛ دیگه نمی‌شه به نفر دیگه سپرد.', 'err');
        const r = await TT.post(`/Leader/Reassign/${card.dataset.id}`, { memberId: chip.dataset.member });
        TT.toast(r.message, r.ok ? 'ok' : 'err');
        if (r.ok) refresh(card.dataset.id);
    }

    function initBoard() {
        sortables.forEach(s => s.destroy());
        sortables = [];
        const board = document.querySelector('.board');
        if (!board || !window.Sortable) return;
        const cols = [...board.querySelectorAll('.col')];
        const fa = TT.fa;
        const updateCounts = () => cols.forEach(c => c.querySelector('.count').textContent = fa(c.querySelectorAll('.kcard').length));

        cols.forEach(col => {
            const status = col.dataset.status;
            sortables.push(Sortable.create(col.querySelector('.col-body'), {
                group: {
                    name: 'board',
                    pull: true,
                    put: (to, from, dragEl) => canMove(from.el.closest('.col').dataset.status, status, dragEl) === true
                },
                draggable: '.kcard.can-drag',
                filter: '.tick, .btn',
                preventOnFilter: false,
                sort: false,
                forceFallback: true,      // درگ یکسان در همه‌ی مرورگرها و موبایل
                fallbackTolerance: 4,     // کلیک ساده روی عنوان همچنان صفحه‌ی تسک را باز می‌کند
                animation: 180,
                delay: 140,
                delayOnTouchOnly: true,
                ghostClass: 'k-ghost',
                chosenClass: 'k-chosen',
                dragClass: 'k-drag',

                onStart: evt => {
                    TT.busy?.(true);
                    document.body.classList.add('is-dragging');
                    const from = status;
                    cols.forEach(c => {
                        const ok = canMove(from, c.dataset.status, evt.item);
                        c.classList.toggle('drop-ok', ok === true && c !== col);
                        c.classList.toggle('drop-no', ok !== true);
                        c.dataset.why = ok === true ? '' : ok;
                    });
                    if (isLeader) {
                        addEventListener('pointermove', trackPointer);
                        addEventListener('touchmove', trackPointer, { passive: true });
                    }
                },

                onEnd: async evt => {
                    cols.forEach(c => { c.classList.remove('drop-ok', 'drop-no'); c.dataset.why = ''; });
                    document.body.classList.remove('is-dragging');
                    removeEventListener('pointermove', trackPointer);
                    removeEventListener('touchmove', trackPointer);
                    const chip = hoverChip;
                    hoverChip?.classList.remove('drop-hover');
                    hoverChip = null;
                    if (isLeader && chip && evt.from === evt.to) await reassign(evt.item, chip);
                    TT.busy?.(false);
                },

                onAdd: async evt => {
                    const card = evt.item;
                    const from = evt.from.closest('.col').dataset.status;
                    const revert = () => {
                        evt.from.insertBefore(card, evt.from.children[evt.oldIndex] || null);
                        updateCounts();
                    };
                    updateCounts();

                    // کارهایی که تأیید می‌خواهند: کارت فعلاً برمی‌گردد و مودال باز می‌شود
                    if (!isLeader && (status === 'Review' || status === 'Done')) { revert(); return openDone(card); }
                    if (isLeader && from === 'Review' && status === 'Done') { revert(); return openApprove(card); }
                    if (isLeader && from === 'Review' && status === 'InProgress') { revert(); return openReject(card); }

                    const url = isLeader ? `/Leader/Move/${card.dataset.id}` : `/Member/Move/${card.dataset.id}`;
                    TT.busy?.(true);
                    const r = await TT.post(url, { status });
                    if (r.ok) {
                        card.dataset.status = status;
                        TT.toast(r.message, 'ok');
                        refresh(card.dataset.id);
                    } else {
                        revert();
                        TT.toast(r.message || 'جابه‌جایی انجام نشد', 'err');
                    }
                    TT.busy?.(false);
                }
            }));
        });
    }

    TT.onUpdate(initBoard);
})();
