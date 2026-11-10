(() => {
    const button = document.querySelector('[data-copy-reference]');
    const reference = document.getElementById('support-reference');
    const status = document.querySelector('.portal-error__copy-status');
    if (!button || !reference || !status) return;

    button.hidden = false;
    button.addEventListener('click', async () => {
        status.textContent = '';
        try {
            await navigator.clipboard.writeText(reference.textContent.trim());
            status.textContent = 'Support reference copied.';
        } catch {
            // Intranet HTTP pages may not have Clipboard API access.
            const selection = window.getSelection();
            const range = document.createRange();
            range.selectNodeContents(reference);
            selection.removeAllRanges();
            selection.addRange(range);
            status.textContent = 'Select and copy the highlighted reference, or include a screenshot when contacting IT.';
        }
    });
})();
