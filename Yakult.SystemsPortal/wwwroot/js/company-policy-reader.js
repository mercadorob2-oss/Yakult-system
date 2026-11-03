(() => {
    function init(host = document) {
        host.querySelectorAll('[data-policy-reader]').forEach(reader => {
            if (reader.policyReaderCleanup) return;
            const events = new AbortController();
            const on = (node, type, action) => node?.addEventListener(type, action, { signal: events.signal });
            const body = reader.querySelector('[data-policy-document]');
            const sections = [...body.querySelectorAll(':scope > section')];
            const links = [...reader.querySelectorAll('.it-info-outline details nav a')];
            const jump = reader.querySelector('[data-policy-jump]');
            const input = reader.querySelector('[data-policy-search]');
            const status = reader.querySelector('[data-policy-status]');
            const results = reader.querySelector('[data-policy-results]');
            const scrollRoot = reader.closest('.policy-preview-scroll');
            // Emphasize existing approval names using text nodes only.
            const names = document.createTreeWalker(body, NodeFilter.SHOW_TEXT);
            const textNodes = [];
            while (names.nextNode()) textNodes.push(names.currentNode);
            textNodes.forEach(node => {
                if (node.parentElement.closest('strong')) return;
                const parts = node.data.split(/(Sir Rod|Sir Kawasaki)/g);
                if (parts.length === 1) return;
                const fragment = document.createDocumentFragment();
                parts.forEach((part, i) => {
                    if (i % 2) { const strong = document.createElement('strong'); strong.textContent = part; fragment.append(strong); }
                    else fragment.append(document.createTextNode(part));
                });
                node.replaceWith(fragment);
            });
            const setActive = section => {
                links.forEach(link => {
                    if (link.hash === '#' + section.id) link.setAttribute('aria-current', 'location');
                    else link.removeAttribute('aria-current');
                });
                jump.value = section.id;
            };
            const updateActive = () => {
                const top = scrollRoot ? scrollRoot.getBoundingClientRect().top + 40 : 150;
                const current = sections.filter(section => section.getBoundingClientRect().top <= top).at(-1) || sections[0];
                if (current) setActive(current);
            };
            let frame;
            on(scrollRoot || window, 'scroll', () => { cancelAnimationFrame(frame); frame = requestAnimationFrame(updateActive); });
            on(window, 'resize', updateActive);
            const navigate = id => {
                const section = sections.find(section => section.id === id);
                if (!section) return;
                section.scrollIntoView({ block: 'start' });
                const heading = section.querySelector('h2');
                heading.setAttribute('tabindex', '-1');
                heading.focus({ preventScroll: true });
                setActive(section);
                if (!scrollRoot) history.replaceState(null, '', '#' + id);
            };
            on(reader, 'click', event => {
                const link = event.target.closest('.it-info-outline a[href^="#"]');
                if (!link || event.ctrlKey || event.metaKey || event.shiftKey || event.altKey) return;
                event.preventDefault(); navigate(link.hash.slice(1));
            });
            on(jump, 'change', () => navigate(jump.value));
            const search = () => {
                body.querySelectorAll('mark[data-policy-match]').forEach(mark => mark.replaceWith(document.createTextNode(mark.textContent)));
                body.normalize();
                results.replaceChildren();
                const term = input.value.trim();
                let matches = 0;
                let matchingSections = 0;
                if (term) sections.forEach(section => {
                    const walker = document.createTreeWalker(section, NodeFilter.SHOW_TEXT);
                    const nodes = [];
                    while (walker.nextNode()) nodes.push(walker.currentNode);
                    let count = 0;
                    const pattern = new RegExp(term.replace(/[.*+?^${}()|[\]\\]/g, '\\$&'), 'giu');
                    nodes.forEach(node => {
                        const hits = [...node.data.matchAll(pattern)];
                        if (!hits.length) return;
                        const fragment = document.createDocumentFragment();
                        let offset = 0;
                        hits.forEach(hit => {
                            fragment.append(document.createTextNode(node.data.slice(offset, hit.index)));
                            const mark = document.createElement('mark'); mark.dataset.policyMatch = '';
                            mark.textContent = hit[0]; fragment.append(mark);
                            offset = hit.index + hit[0].length; count++;
                        });
                        fragment.append(document.createTextNode(node.data.slice(offset)));
                        node.replaceWith(fragment);
                    });
                    if (count) {
                        matchingSections++; matches += count;
                        const link = document.createElement('a'); link.href = '#' + section.id;
                        link.textContent = section.querySelector('h2').textContent + ` (${count})`;
                        results.append(link);
                    }
                });
                status.textContent = term ? `${matches} matches in ${matchingSections} of ${sections.length} sections. All sections remain available.` : '';
            };
            on(input, 'input', search);
            on(reader.querySelector('[data-policy-clear]'), 'click', () => { input.value = ''; search(); input.focus(); });
            const dialog = reader.querySelector('dialog');
            const images = [...body.querySelectorAll('[data-policy-image]')];
            const image = dialog.querySelector('img');
            const caption = dialog.querySelector('figcaption');
            const canvas = dialog.querySelector('.policy-viewer-canvas');
            const zoom = dialog.querySelector('[data-image-zoom]');
            let index = 0;
            let opener;
            const resetZoom = () => { canvas.classList.remove('is-zoomed'); zoom.setAttribute('aria-pressed', 'false'); zoom.textContent = 'Zoom in'; };
            const showImage = next => {
                index = (next + images.length) % images.length;
                image.src = images[index].href;
                image.alt = images[index].querySelector('img').alt;
                caption.textContent = `${index + 1} of ${images.length}: ${images[index].dataset.caption || image.alt}`;
                resetZoom();
            };
            images.forEach((link, i) => on(link, 'click', event => {
                if (event.ctrlKey || event.metaKey || event.shiftKey || event.altKey || !dialog.showModal) return;
                event.preventDefault(); opener = link; showImage(i); dialog.showModal();
            }));
            on(dialog.querySelector('[data-image-close]'), 'click', () => dialog.close());
            on(dialog.querySelector('[data-image-prev]'), 'click', () => showImage(index - 1));
            on(dialog.querySelector('[data-image-next]'), 'click', () => showImage(index + 1));
            on(zoom, 'click', () => {
                const enlarged = canvas.classList.toggle('is-zoomed');
                zoom.setAttribute('aria-pressed', String(enlarged)); zoom.textContent = enlarged ? 'Fit image' : 'Zoom in';
            });
            on(dialog, 'keydown', event => {
                if (event.key === 'ArrowLeft' || event.key === 'ArrowRight') {
                    event.preventDefault(); showImage(index + (event.key === 'ArrowLeft' ? -1 : 1));
                }
            });
            on(dialog, 'close', () => { resetZoom(); opener?.focus({ preventScroll: true }); });
            reader.policyReaderCleanup = () => { dialog.close(); events.abort(); cancelAnimationFrame(frame); };
            updateActive();
        });
    }
    window.CompanyPolicyReader = { init };
    init();
})();
