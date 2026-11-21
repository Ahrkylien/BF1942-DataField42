// The admin pages' job cards: start and stop a job, and watch it run, without leaving the page.
//
// The forms work on their own as ordinary posts. This takes them over: it sends them with fetch,
// draws the card from the state the server answers with, and polls while anything runs. When a job
// ends, the parts of the page that show its result are fetched again and swapped in, so nothing
// reloads and nothing the person was doing is lost.
(() => {
    'use strict';

    const pollInterval = 1000;
    const retryInterval = 5000;

    let polling = false;
    let running = new Set();

    const cardOf = id => document.querySelector(`[data-job="${CSS.escape(id)}"]`);
    const sleep = ms => new Promise(resolve => setTimeout(resolve, ms));
    const pad = value => String(value).padStart(2, '0');

    const stamp = iso => {
        const date = new Date(iso);
        return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())} ${pad(date.getHours())}:${pad(date.getMinutes())}`;
    };

    // What a job's state reads as: a coloured word, then the rest of the sentence.
    const describe = job => {
        if (job.isRunning) {
            const parts = [];
            if (job.step) parts.push(job.step);
            if (job.totalCount > 0)
                parts.push(`${job.completedCount}/${job.totalCount} (${Math.round(job.percentage || 0)}%)`);
            return ['Running', '#57a857', parts.length ? ` — ${parts.join(' — ')}` : ''];
        }

        switch (job.state) {
            case 'Succeeded': return ['Finished', '#57a857', ` at ${stamp(job.finishedUtc)}`];
            case 'Cancelled': return ['Stopped', '#c9b037', ` at ${stamp(job.finishedUtc)}`];
            case 'Failed': return ['Failed', '#ab0909', `: ${job.error ?? ''}`];
            case 'Interrupted': return ['Interrupted', '#c9b037', `: ${job.error ?? ''}`];
            default: return ['Never run', '#8a8a8a', ''];
        }
    };

    // The bar eases forward as a job progresses, but going back -- a finished job starting again --
    // is a reset and not progress, so it jumps. The transition is switched off for that one change
    // and put back, with a reflow in between so the browser applies the jump without animating it.
    const barTransition = 'width .4s ease';

    const setBar = (bar, percentage) => {
        const current = parseFloat(bar.style.width) || 0;

        if (percentage < current) {
            bar.style.transition = 'none';
            bar.style.width = `${percentage}%`;
            void bar.offsetWidth;
            bar.style.transition = barTransition;
        } else {
            bar.style.width = `${percentage}%`;
        }
    };

    const render = job => {
        const card = cardOf(job.jobId);
        if (!card) return;

        const [label, color, rest] = describe(job);
        const word = document.createElement('span');
        word.style.color = color;
        word.textContent = label;
        card.querySelector('.jobState').replaceChildren(word, rest);

        setBar(card.querySelector('.jobBarFill'), job.percentage ?? 0);

        const log = card.querySelector('.jobLog');
        if (job.log?.length) {
            // Follow the end of the log only while the reader is already there.
            const atEnd = log.hidden || log.scrollTop + log.clientHeight >= log.scrollHeight - 4;
            log.hidden = false;
            log.textContent = job.log.join('\n');
            if (atEnd) log.scrollTop = log.scrollHeight;
        } else {
            log.hidden = true;
        }

        const form = card.querySelector('[data-job-form]');
        form.action = `/Admin/Jobs/${job.jobId}/${job.isRunning ? 'Cancel' : 'Start'}`;
        form.querySelector('button').textContent = job.isRunning ? 'Stop' : 'Run';
        form.querySelector('select')?.toggleAttribute('hidden', job.isRunning);
    };

    const showMessage = (card, text) => {
        const message = card.querySelector('.jobMessage');
        message.textContent = text;
        message.hidden = !text;
    };

    // The parts of the page that show what a job produced. They are fetched from the server again
    // and swapped in; everything around them is left exactly as it is.
    const refreshRegions = async () => {
        const regions = document.querySelectorAll('[data-region]');
        if (!regions.length) return;

        const response = await fetch(location.href, { headers: { Accept: 'text/html' } });
        if (!response.ok) return;

        const fresh = new DOMParser().parseFromString(await response.text(), 'text/html');
        for (const region of regions) {
            const replacement = fresh.querySelector(`[data-region="${CSS.escape(region.dataset.region)}"]`);
            if (replacement) region.replaceWith(replacement);
        }
    };

    // Polls for as long as something runs, then stops: an idle page makes no requests.
    const poll = async () => {
        if (polling) return;
        polling = true;

        try {
            for (;;) {
                let jobs;
                try {
                    const response = await fetch('/Admin/Jobs/Status', { headers: { Accept: 'application/json' } });
                    if (!response.ok) break;
                    jobs = await response.json();
                } catch {
                    // The server may be restarting; keep trying while a job was running.
                    if (running.size === 0) break;
                    await sleep(retryInterval);
                    continue;
                }

                const nowRunning = new Set();
                for (const job of jobs) {
                    render(job);
                    if (job.isRunning) nowRunning.add(job.jobId);
                }

                const finished = [...running].some(id => !nowRunning.has(id));
                running = nowRunning;

                if (finished) await refreshRegions();
                if (running.size === 0) break;

                await sleep(pollInterval);
            }
        } finally {
            polling = false;
        }
    };

    document.addEventListener('submit', async event => {
        const form = event.target;
        if (!form.matches?.('[data-job-form]')) return;

        event.preventDefault();

        const card = form.closest('[data-job]');
        const button = form.querySelector('button');
        button.disabled = true;

        try {
            const response = await fetch(form.action, {
                method: 'POST',
                body: new FormData(form),
                headers: { Accept: 'application/json' },
            });
            const body = await response.json().catch(() => ({}));

            if (!response.ok) {
                showMessage(card, body.error ?? `The request failed (${response.status}).`);
                return;
            }

            showMessage(card, '');
            render(body);

            // Counted as running even if it has already finished, so that the poll that follows
            // sees it end and refreshes what it produced.
            running.add(body.jobId);
            poll();
        } catch {
            showMessage(card, 'Could not reach the server.');
        } finally {
            button.disabled = false;
        }
    });

    // Catches up with a job that is already running, started from another tab or before this page
    // was opened.
    poll();
})();
