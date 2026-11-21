// The Update page: check a branch for new commits, start the update, and follow it.
//
// The update ends by restarting this site, so the status requests fail for a while. That is part of
// the update and not an error: the page keeps asking until the site answers again, then shows how the
// update ended.
(() => {
    'use strict';

    const root = document.querySelector('[data-update]');
    if (!root) return;

    const form = document.getElementById('updateForm');
    const branch = document.getElementById('branch');
    const checkButton = document.getElementById('checkButton');
    const updateButton = document.getElementById('updateButton');
    const message = document.getElementById('updateMessage');
    const commits = document.getElementById('commits');
    const deployed = document.getElementById('deployed');
    const stateLine = document.getElementById('updateState');
    const log = document.getElementById('updateLog');

    const pollInterval = 1000;
    const sleep = ms => new Promise(resolve => setTimeout(resolve, ms));
    const pad = value => String(value).padStart(2, '0');

    const stamp = iso => {
        const date = new Date(iso);
        return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())} ${pad(date.getHours())}:${pad(date.getMinutes())}:${pad(date.getSeconds())}`;
    };

    const show = (element, text, color) => {
        element.textContent = text;
        element.style.color = color ?? '';
    };

    const post = async url => {
        const response = await fetch(url, {
            method: 'POST',
            headers: { Accept: 'application/json' },
            body: new FormData(form),
        });
        const body = await response.json().catch(() => ({}));
        if (!response.ok) throw new Error(body.error ?? `The server answered ${response.status}.`);
        return body;
    };

    const describeState = status => {
        const at = status.changedUtc ? ` — ${stamp(status.changedUtc)}` : '';
        switch (status.state) {
            case 'Running': return ['Updating…', '#57a857'];
            case 'Succeeded': return [`Succeeded${at}`, '#57a857'];
            case 'Failed': return [`Failed${at}. The log below says why.`, '#ab0909'];
            case 'Interrupted': return [`Interrupted${at}: nothing is running it any more.`, '#c9b037'];
            default: return ['No update has run yet.', '#8a8a8a'];
        }
    };

    const render = status => {
        const [text, color] = describeState(status);
        show(stateLine, text, color);

        const stickToEnd = log.scrollTop + log.clientHeight >= log.scrollHeight - 20;
        log.textContent = status.log;
        if (stickToEnd) log.scrollTop = log.scrollHeight;

        if (status.deployed) {
            deployed.textContent = '';
            const label = document.createElement('span');
            label.textContent = `${status.deployed.branch ?? 'unknown branch'} at `;
            const hash = document.createElement('span');
            hash.style.fontFamily = 'monospace';
            hash.textContent = status.deployed.commit.slice(0, 8);
            label.append(hash);
            deployed.append(label);
        }

        const running = status.state === 'Running';
        checkButton.disabled = running;
        updateButton.disabled = running;
    };

    const fetchStatus = async () => {
        const response = await fetch(root.dataset.statusUrl, { headers: { Accept: 'application/json' }, cache: 'no-store' });
        if (response.redirected || !response.ok) throw new Error('unavailable');
        return response.json();
    };

    let following = false;

    // Follows a running update to its end, riding out the restart in the middle of it.
    const follow = async () => {
        if (following) return;
        following = true;
        let sawRestart = false;

        try {
            for (;;) {
                try {
                    const status = await fetchStatus();
                    render(status);
                    if (status.state !== 'Running') {
                        if (status.state === 'Succeeded') {
                            show(message, sawRestart ? 'Updated. The site restarted.' : 'Updated.', '#57a857');
                            commits.textContent = '';
                        } else if (status.state === 'Failed' || status.state === 'Interrupted') {
                            show(message, 'The update did not finish.', '#ab0909');
                        }
                        return;
                    }
                } catch {
                    sawRestart = true;
                    show(stateLine, 'Updating… the site is restarting.', '#57a857');
                }

                await sleep(pollInterval);
            }
        } finally {
            following = false;
        }
    };

    const renderCommits = check => {
        commits.textContent = '';

        if (check.isUpToDate) {
            show(message, `Already running the latest ${check.branch} (${check.tip.slice(0, 8)}).`, '#57a857');
            return;
        }

        const heading = check.showsLatestOnly
            ? `The running version is not known here, so these are the latest commits on ${check.branch}:`
            : `${check.commits.length} new ${check.commits.length === 1 ? 'commit' : 'commits'} on ${check.branch}:`;
        show(message, heading);

        const list = document.createElement('div');
        for (const commit of check.commits) {
            const line = document.createElement('div');
            const hash = document.createElement('span');
            hash.style.cssText = 'font-family:monospace;color:#8a8a8a;';
            hash.textContent = `${commit.shortHash} `;
            line.append(hash, `${commit.subject} `);
            const detail = document.createElement('span');
            detail.style.color = '#8a8a8a';
            detail.textContent = `— ${commit.author}, ${stamp(commit.dateUtc)}`;
            line.append(detail);
            list.append(line);
        }
        commits.append(list);
    };

    checkButton.addEventListener('click', async () => {
        checkButton.disabled = true;
        show(message, 'Checking GitHub…');
        commits.textContent = '';

        try {
            renderCommits(await post(root.dataset.checkUrl));
        } catch (error) {
            show(message, error.message, '#ab0909');
        } finally {
            checkButton.disabled = false;
        }
    });

    updateButton.addEventListener('click', async () => {
        if (!confirm(`Update to ${branch.value}? The site restarts and is unavailable for a moment.`)) return;

        updateButton.disabled = true;
        checkButton.disabled = true;
        show(message, '');

        try {
            render(await post(root.dataset.startUrl));
            follow();
        } catch (error) {
            show(message, error.message, '#ab0909');
            updateButton.disabled = false;
            checkButton.disabled = false;
        }
    });

    render({
        state: stateLine.dataset.state,
        changedUtc: stateLine.dataset.changed || null,
        log: log.textContent,
        deployed: null,
    });
    if (stateLine.dataset.state === 'Running') follow();
})();
