document.addEventListener('DOMContentLoaded', function () {
    'use strict';
    const root = document.getElementById('overviewPage');
    const button = document.getElementById('dashboardHealthBtn');
    const status = document.getElementById('dashboardHealthStatus');
    const runDetails = document.getElementById('overviewRunDetails');
    const runResults = document.getElementById('overviewRunResults');
    if (!root || !button || !status || !runDetails || !runResults) return;

    function showStatus(message, tone) {
        status.textContent = message;
        status.className = 'overview-run-status' + (tone ? ' is-' + tone : '');
        status.hidden = false;
    }

    function setBusy(busy) {
        root.dataset.checking = String(busy);
        button.disabled = busy || button.dataset.configuredCount === '0';
        button.classList.toggle('is-loading', busy);
        button.querySelector('span').textContent = busy ? 'Checking services…' : 'Run health checks';
        root.querySelectorAll('[data-overview-fragment="metrics"], [data-overview-fragment="priorities"]').forEach(element => element.setAttribute('aria-busy', String(busy)));
    }

    function formatPht(date) {
        return new Intl.DateTimeFormat('en-GB', {
            timeZone: 'Asia/Manila', day: '2-digit', month: 'short', year: 'numeric',
            hour: '2-digit', minute: '2-digit', hourCycle: 'h23'
        }).format(date) + ' PHT';
    }

    function showUnsavedResults(items) {
        runResults.replaceChildren();
        items.slice().sort((a, b) => Number(a.status === 'Healthy') - Number(b.status === 'Healthy')).forEach(item => {
            const row = document.createElement('li');
            const name = document.createElement('strong');
            name.textContent = String(item.cardName || item.cardKey || 'Service');
            const checkedAt = new Date(item.checkedAtUtc);
            const timestamp = Number.isNaN(checkedAt.getTime()) ? '' : ' · ' + formatPht(checkedAt);
            row.append(name, document.createTextNode(' · ' + String(item.status || 'Unknown') + timestamp));
            if (item.detail) row.append(document.createTextNode(' · ' + String(item.detail)));
            runResults.appendChild(row);
        });
        runDetails.hidden = !items.length;
    }

    async function refreshOverview() {
        try {
            const response = await fetch(window.location.href, { credentials: 'same-origin', cache: 'no-store' });
            if (!response.ok) return false;
            const page = new DOMParser().parseFromString(await response.text(), 'text/html');
            const incoming = page.getElementById('overviewPage');
            if (!incoming) return false;
            const names = ['freshness', 'metrics', 'priorities', 'monitoring'];
            const replacements = names.map(name => ({
                current: root.querySelector(`[data-overview-fragment="${name}"]`),
                next: incoming.querySelector(`[data-overview-fragment="${name}"]`)
            }));
            if (replacements.some(item => !item.current || !item.next)) return false;
            const openDetails = Array.from(root.querySelectorAll('details[id][open]')).map(item => item.id);
            const active = document.activeElement;
            const replacedFocus = replacements.some(item => item.current.contains(active));
            const focusedDetails = active?.closest('details[id]')?.id;
            const focusedHref = active?.closest('a[href]')?.getAttribute('href');
            replacements.forEach(item => item.current.replaceWith(item.next));
            openDetails.forEach(id => {
                const details = root.querySelector('details[id="' + id + '"]');
                if (details) details.open = true;
            });
            const incomingButton = incoming.querySelector('#dashboardHealthBtn');
            if (incomingButton) button.dataset.configuredCount = incomingButton.dataset.configuredCount;
            if (replacedFocus) {
                const details = focusedDetails ? document.getElementById(focusedDetails) : null;
                const link = focusedHref ? Array.from(root.querySelectorAll('a[href]')).find(item => item.getAttribute('href') === focusedHref) : null;
                (details?.querySelector('summary') || link || button).focus();
            }
            return true;
        } catch {
            return false;
        }
    }

    button.addEventListener('click', async function () {
        if (button.disabled) return;
        runDetails.hidden = true;
        runDetails.open = false;
        runResults.replaceChildren();
        if (button.dataset.preview === 'true') {
            showStatus('Configure a connection and monitoring URLs before running checks.', 'warning');
            return;
        }
        setBusy(true);
        showStatus('Running health checks. Please wait for all services to respond.');
        try {
            const token = root.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
            const response = await fetch(button.dataset.checkUrl, {
                method: 'POST', credentials: 'same-origin', headers: { 'RequestVerificationToken': token }
            });
            if (!response.ok) throw new Error('Unable to complete health checks. Please try again.');
            const result = await response.json();
            if (!result.success || !Array.isArray(result.results)) throw new Error('Unable to read health results. Please try again.');
            const items = result.results;
            if (!items.length) {
                await refreshOverview();
                showStatus('No monitoring URLs are configured. Open Service Health to configure a service.', 'warning');
                return;
            }
            const healthy = items.filter(item => item.status === 'Healthy').length;
            const failed = items.length - healthy;
            const summary = `Checks completed ${formatPht(new Date())}: ${healthy} healthy, ${failed} failed.`;
            if (!result.resultsSaved) {
                showUnsavedResults(items);
                showStatus(summary + ' Some results could not be saved. The summary still shows previously recorded values; expand the results below to review this check.', 'warning');
            } else if (await refreshOverview()) {
                showStatus(summary + ' Results saved and overview updated.', failed ? 'warning' : 'success');
            } else {
                showUnsavedResults(items);
                showStatus(summary + ' Results saved. Reload this page to update the overview, or expand the results below.', 'warning');
            }
            window.yakultToast?.(result.resultsSaved ? 'Health checks completed.' : 'Some check results could not be saved.', result.resultsSaved && !failed ? 'success' : 'warning');
        } catch (error) {
            showStatus(error.message || 'Unable to run health checks. Please try again.', 'error');
            window.yakultToast?.(status.textContent, 'error');
        } finally {
            setBusy(false);
        }
    });
});
