// Loads a run's full log the first time its block is opened.
document.addEventListener('toggle', async event => {
    const details = event.target;
    if (!(details instanceof HTMLDetailsElement) || !details.classList.contains('runLog') || !details.open || details.dataset.loaded)
        return;

    details.dataset.loaded = '1';
    const body = details.querySelector('.logBody');
    try {
        const response = await fetch(details.dataset.logUrl);
        if (!response.ok) throw new Error(response.status);
        body.textContent = await response.text();
    } catch {
        body.textContent = 'The log could not be loaded.';
        delete details.dataset.loaded;
    }
}, true);
