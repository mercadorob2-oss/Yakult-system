(() => {
    const form = document.getElementById('companyPolicyForm');
    const root = document.getElementById('policyBuilder');
    if (!form || !root) return;

    const $ = (id) => document.getElementById(id);
    const bodyInput = $('policyBody');
    const sectionsHost = $('policySections');
    const previewSections = $('previewSections');
    const previewContents = $('previewContents');
    const legacy = window.companyPolicyEditorLegacy === true;
    let legacyMode = legacy;
    let doc = JSON.parse(JSON.stringify(window.companyPolicyEditorInitial || { version: 1, sections: [] }));
    let assets = [];
    const token = form.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
    const labels = { text: 'Text', bullets: 'Bullet list', steps: 'Numbered steps', process: 'Account process', cards: 'Role cards', flowchart: 'Flowchart' };

    const ensureArrays = () => {
        if (!Array.isArray(doc.sections)) doc.sections = [];
        doc.sections.forEach(section => {
            if (!Array.isArray(section.blocks)) section.blocks = [];
            section.blocks.forEach(block => {
                if (!Array.isArray(block.items)) block.items = [];
                if (!Array.isArray(block.stages)) block.stages = [];
                if (!Array.isArray(block.cards)) block.cards = [];
                if (!Array.isArray(block.paths)) block.paths = [];
            });
        });
    };
    const make = (tag, className, text) => {
        const node = document.createElement(tag);
        if (className) node.className = className;
        if (text !== undefined) node.textContent = text;
        return node;
    };
    const button = (text, className, action, disabled = false) => {
        const node = make('button', className, text);
        node.type = 'button';
        node.disabled = disabled;
        node.addEventListener('click', action);
        return node;
    };
    const field = (labelText, value, onInput, multiline = false, placeholder = '') => {
        const label = make('label', 'policy-field', labelText);
        const control = document.createElement(multiline ? 'textarea' : 'input');
        if (!multiline) control.type = 'text';
        if (multiline) control.rows = 3;
        control.value = value || '';
        control.placeholder = placeholder;
        control.addEventListener('input', () => { onInput(control.value); changed(); });
        label.appendChild(control);
        return label;
    };
    const newlineValues = value => String(value || '').split(/\r?\n/).map(x => x.trim()).filter(Boolean);
    const move = (list, index, delta) => {
        const next = index + delta;
        if (next < 0 || next >= list.length) return;
        [list[index], list[next]] = [list[next], list[index]];
        render();
    };
    const newSection = () => ({ id: 'section-' + crypto.randomUUID().replaceAll('-', '').slice(0, 16), title: '', intro: '', blocks: [] });
    const newBlock = type => ({ type, title: '', text: '', items: [], stages: [], cards: [], imageUrl: '', altText: '', paths: [], sourceNote: '' });
    const changed = () => { syncBody(); renderPreview(); };
    const syncBody = () => {
        if (legacyMode) {
            bodyInput.value = $('legacyPolicyBody')?.value || '';
            return;
        }
        bodyInput.value = 'yakult-policy:v1:' + JSON.stringify(doc);
    };

    function listEditor(block, key, labelText, itemLabel) {
        const wrapper = make('div', 'policy-nested-list');
        block[key].forEach((value, index) => {
            const row = make('div', 'policy-nested-card');
            row.appendChild(field(itemLabel + ' ' + (index + 1), value, next => { block[key][index] = next; }, true));
            row.appendChild(button('Remove', 'policy-remove-button', () => { block[key].splice(index, 1); render(); }));
            wrapper.appendChild(row);
        });
        wrapper.appendChild(button('+ Add ' + itemLabel.toLowerCase(), 'admin-button admin-button--ghost', () => { block[key].push(''); render(); }));
        return wrapper;
    }

    function renderBlockEditor(block, blockIndex, blocks) {
        const card = make('div', 'policy-block-card');
        const head = make('div', 'policy-block-head');
        head.appendChild(make('strong', '', labels[block.type] || 'Content block'));
        const actions = make('div', 'policy-icon-actions');
        actions.appendChild(button('↑', '', () => move(blocks, blockIndex, -1), blockIndex === 0));
        actions.appendChild(button('↓', '', () => move(blocks, blockIndex, 1), blockIndex === blocks.length - 1));
        actions.appendChild(button('Remove block', 'policy-remove-button', () => { blocks.splice(blockIndex, 1); render(); }));
        head.appendChild(actions);
        card.appendChild(head);
        card.appendChild(field('Block heading (optional)', block.title, value => { block.title = value; }, false, 'For example, What to do'));

        if (block.type === 'text') card.appendChild(field('Text (Markdown supported)', block.text, value => { block.text = value; }, true, 'Write the policy guidance.'));
        if (block.type === 'bullets') card.appendChild(listEditor(block, 'items', 'Bullet points', 'Point'));
        if (block.type === 'steps') card.appendChild(listEditor(block, 'items', 'Numbered instructions', 'Step'));
        if (block.type === 'process') {
            block.stages.forEach((stage, index) => {
                const row = make('div', 'policy-nested-card');
                const stageHead = make('div', 'policy-stage-head');
                stageHead.appendChild(make('strong', '', 'Stage ' + (index + 1)));
                stageHead.appendChild(button('Remove stage', 'policy-remove-button', () => { block.stages.splice(index, 1); render(); }));
                row.appendChild(stageHead);
                row.appendChild(field('Owner or role', stage.owner, value => { stage.owner = value; }));
                row.appendChild(field('Stage title', stage.title, value => { stage.title = value; }));
                row.appendChild(field('Details (one per line)', (stage.points || []).join('\n'), value => { stage.points = newlineValues(value); }, true, 'Describe what this role does.'));
                card.appendChild(row);
            });
            card.appendChild(button('+ Add process stage', 'admin-button admin-button--ghost', () => { block.stages.push({ owner: '', title: '', points: [''] }); render(); }));
        }
        if (block.type === 'cards') {
            block.cards.forEach((entry, index) => {
                const row = make('div', 'policy-nested-card');
                const cardHead = make('div', 'policy-card-head');
                cardHead.appendChild(make('strong', '', 'Role card ' + (index + 1)));
                cardHead.appendChild(button('Remove card', 'policy-remove-button', () => { block.cards.splice(index, 1); render(); }));
                row.appendChild(cardHead);
                row.appendChild(field('Label', entry.label, value => { entry.label = value; }));
                row.appendChild(field('Role or team', entry.title, value => { entry.title = value; }));
                row.appendChild(field('Responsibilities', entry.text, value => { entry.text = value; }, true));
                card.appendChild(row);
            });
            card.appendChild(button('+ Add role card', 'admin-button admin-button--ghost', () => { block.cards.push({ label: '', title: '', text: '' }); render(); }));
        }
        if (block.type === 'flowchart') renderFlowchartEditor(card, block);
        return card;
    }

    function renderFlowchartEditor(card, block) {
        const label = make('label', 'policy-field', 'Select an uploaded diagram');
        const select = document.createElement('select');
        select.appendChild(new Option('Choose a diagram…', ''));
        assets.forEach(asset => select.appendChild(new Option(asset.name, asset.url)));
        if (block.imageUrl && !assets.some(asset => asset.url === block.imageUrl)) select.appendChild(new Option(block.imageUrl.split('/').pop(), block.imageUrl));
        select.value = block.imageUrl || '';
        select.addEventListener('change', () => { block.imageUrl = select.value; changed(); render(); });
        label.appendChild(select);
        card.appendChild(label);

        const uploadLabel = make('label', 'policy-field', 'Upload PNG, JPG, or WebP (10 MB max)');
        const upload = document.createElement('input');
        upload.type = 'file'; upload.accept = '.png,.jpg,.jpeg,.webp,image/png,image/jpeg,image/webp';
        upload.addEventListener('change', async () => {
            if (!upload.files?.length) return;
            const status = uploadLabel.querySelector('.policy-upload-status');
            if (status) status.textContent = 'Uploading…';
            const payload = new FormData(); payload.append('file', upload.files[0]);
            try {
                const response = await fetch(root.dataset.uploadUrl, { method: 'POST', headers: { RequestVerificationToken: token }, body: payload, credentials: 'same-origin' });
                const result = await response.json();
                if (!response.ok || !result.success) throw new Error(result.message || 'The diagram upload failed.');
                assets = [{ name: result.name, url: result.url }, ...assets.filter(x => x.url !== result.url)];
                block.imageUrl = result.url;
                render(); changed();
            } catch (error) {
                if (status) status.textContent = error.message;
                else window.alert(error.message);
            }
        });
        uploadLabel.appendChild(upload);
        const status = make('span', 'policy-upload-status', 'Files stay in the portal media folder.');
        uploadLabel.appendChild(status);
        card.appendChild(uploadLabel);
        card.appendChild(field('Image alt text (required)', block.altText, value => { block.altText = value; }, false, 'Describe the diagram for employees using a screen reader.'));
        card.appendChild(field('Source note (optional)', block.sourceNote, value => { block.sourceNote = value; }, true, 'For example, source document and page.'));
        card.appendChild(field('Readable paths (one path per line)', (block.paths || []).join('\n'), value => { block.paths = newlineValues(value); }, true, 'Describe each branch, role lane, and decision path.'));
        if (block.imageUrl) {
            const image = make('img', 'policy-image-preview'); image.src = block.imageUrl; image.alt = block.altText || 'Selected diagram preview';
            card.appendChild(image);
        }
    }

    function render() {
        ensureArrays();
        sectionsHost.replaceChildren();
        if (!doc.sections.length) sectionsHost.appendChild(make('p', 'policy-empty-note', 'No sections yet. Add a section to begin building the policy.'));
        doc.sections.forEach((section, index) => {
            const card = make('article', 'policy-section-card');
            const head = make('div', 'policy-section-card__head');
            head.appendChild(make('span', 'policy-section-card__number', 'SECTION ' + String(index + 1).padStart(2, '0')));
            const title = make('label', 'policy-field policy-section-card__title', 'Section title');
            const titleInput = document.createElement('input'); titleInput.value = section.title || ''; titleInput.placeholder = 'For example, Workplace Guidelines';
            titleInput.addEventListener('input', () => { section.title = titleInput.value; changed(); });
            title.appendChild(titleInput); head.appendChild(title);
            const actions = make('div', 'policy-icon-actions');
            actions.appendChild(button('↑', '', () => move(doc.sections, index, -1), index === 0));
            actions.appendChild(button('↓', '', () => move(doc.sections, index, 1), index === doc.sections.length - 1));
            actions.appendChild(button('Remove', 'policy-remove-button', () => { doc.sections.splice(index, 1); render(); changed(); }));
            head.appendChild(actions); card.appendChild(head);
            card.appendChild(field('Section introduction (optional)', section.intro, value => { section.intro = value; }, true));
            const blockList = make('div', 'policy-block-list');
            section.blocks.forEach((block, blockIndex) => blockList.appendChild(renderBlockEditor(block, blockIndex, section.blocks)));
            card.appendChild(blockList);
            const controls = make('div', 'policy-block-controls');
            const kind = document.createElement('select');
            Object.entries(labels).forEach(([value, labelText]) => kind.appendChild(new Option(labelText, value)));
            const addBlock = button('+ Add content block', 'admin-button admin-button--ghost', () => { section.blocks.push(newBlock(kind.value)); render(); changed(); });
            controls.appendChild(kind); controls.appendChild(addBlock); card.appendChild(controls);
            sectionsHost.appendChild(card);
        });
        renderPreview(); syncBody();
    }

    function appendText(parent, tag, text, className) {
        const node = make(tag, className, text || '');
        parent.appendChild(node);
        return node;
    }
    function renderPreview() {
        $('previewTitle').textContent = $('Title').value.trim() || 'Untitled policy';
        $('previewSummary').textContent = $('Summary').value.trim();
        $('previewSummary').hidden = !$('Summary').value.trim();
        $('previewCategory').textContent = $('Category').value.trim() || 'Policy';
        $('previewSectionCount').textContent = doc.sections.length + (doc.sections.length === 1 ? ' section' : ' sections');
        previewSections.replaceChildren(); previewContents.replaceChildren();
        doc.sections.forEach((section, index) => {
            const link = make('a'); link.href = '#' + section.id;
            appendText(link, 'span', String(index + 1).padStart(2, '0'));
            link.appendChild(document.createTextNode(section.title || 'Untitled section'));
            previewContents.appendChild(link);
            const wrapper = make('section', 'it-info-section'); wrapper.id = section.id || 'preview-section-' + index;
            appendText(wrapper, 'span', 'SECTION ' + String(index + 1).padStart(2, '0'), 'it-info-eyebrow');
            appendText(wrapper, 'h2', section.title || 'Untitled section');
            if (section.intro) appendText(wrapper, 'p', section.intro, 'it-info-section__intro policy-preview-text');
            (section.blocks || []).forEach(block => renderPreviewBlock(wrapper, block));
            previewSections.appendChild(wrapper);
        });
        if (legacyMode) {
            previewSections.replaceChildren();
            const section = make('section', 'it-info-section');
            appendText(section, 'span', 'EXISTING POLICY', 'it-info-eyebrow');
            appendText(section, 'h2', 'Markdown content');
            appendText(section, 'p', $('legacyPolicyBody')?.value || '', 'policy-preview-text');
            previewSections.appendChild(section);
            $('previewSectionCount').textContent = 'Markdown policy';
        }
    }
    function renderPreviewBlock(parent, block) {
        if (block.title) appendText(parent, 'h3', block.title);
        if (block.type === 'text') appendText(parent, 'p', block.text, 'policy-preview-text');
        if (block.type === 'bullets' || block.type === 'steps') {
            const list = make(block.type === 'steps' ? 'ol' : 'ul', block.type === 'steps' ? '' : 'it-info-points');
            (block.items || []).forEach(item => appendText(list, 'li', item, 'policy-preview-text'));
            parent.appendChild(list);
        }
        if (block.type === 'process') {
            const process = make('div', 'it-info-account-process'); const list = make('ol');
            (block.stages || []).forEach(stage => {
                const row = make('li'); appendText(row, 'span', stage.owner); appendText(row, 'h4', stage.title);
                const details = make('ul'); (stage.points || []).forEach(point => appendText(details, 'li', point, 'policy-preview-text'));
                row.appendChild(details); list.appendChild(row);
            }); process.appendChild(list); parent.appendChild(process);
        }
        if (block.type === 'cards') {
            const grid = make('div', 'it-info-detail-grid');
            (block.cards || []).forEach(entry => { const card = make('article', 'it-info-detail-card'); appendText(card, 'span', entry.label); appendText(card, 'h3', entry.title); appendText(card, 'p', entry.text, 'policy-preview-text'); grid.appendChild(card); });
            parent.appendChild(grid);
        }
        if (block.type === 'flowchart') {
            const details = make('details', 'company-policy-flowchart'); details.open = true;
            appendText(details, 'summary', block.title || 'Process flowchart');
            if (block.imageUrl) { const figure = make('figure'); const imageLink = make('a'); imageLink.href = block.imageUrl; const image = make('img'); image.src = block.imageUrl; image.alt = block.altText || ''; image.loading = 'lazy'; imageLink.appendChild(image); figure.appendChild(imageLink); details.appendChild(figure); }
            if (block.sourceNote) appendText(details, 'p', block.sourceNote, 'company-policy-source-note');
            if ((block.paths || []).length) { const paths = make('div', 'company-policy-flowchart__paths'); appendText(paths, 'h3', 'Process paths'); const list = make('ul'); block.paths.forEach(path => appendText(list, 'li', path, 'policy-preview-text')); paths.appendChild(list); details.appendChild(paths); }
            parent.appendChild(details);
        }
    }
    function validateDocument() {
        if (!$('Title').value.trim()) return 'Enter a policy title.';
        if (!$('Category').value.trim()) return 'Enter a policy topic.';
        if (!doc.sections.length) return 'Add at least one section.';
        for (const section of doc.sections) {
            if (!section.title?.trim()) return 'Give every section a title.';
            if (!section.blocks?.length) return 'Add at least one content block to “' + section.title + '”.';
            for (const block of section.blocks) {
                if (block.type === 'text' && !block.text?.trim()) return 'Fill in the text block in “' + section.title + '”.';
                if (['bullets', 'steps'].includes(block.type) && !(block.items || []).some(x => x.trim())) return 'Add at least one list item in “' + section.title + '”.';
                if (block.type === 'process' && (!(block.stages || []).length || block.stages.some(stage => !stage.owner?.trim() || !stage.title?.trim() || !(stage.points || []).some(x => x.trim())))) return 'Complete each process stage in “' + section.title + '”.';
                if (block.type === 'cards' && (!(block.cards || []).length || block.cards.some(entry => !entry.label?.trim() || !entry.title?.trim() || !entry.text?.trim()))) return 'Complete each role card in “' + section.title + '”.';
                if (block.type === 'flowchart' && (!block.imageUrl?.startsWith('/media/company-policies/') || !block.altText?.trim() || !(block.paths || []).some(x => x.trim()))) return 'Choose a diagram and add alt text and readable path descriptions in “' + section.title + '”.';
            }
        }
        return '';
    }

    $('addPolicySection').addEventListener('click', () => { doc.sections.push(newSection()); render(); changed(); sectionsHost.lastElementChild?.scrollIntoView({ behavior: 'smooth', block: 'nearest' }); });
    if (legacy) {
        $('legacyPolicyEditor').hidden = false;
        $('structuredPolicyEditor').hidden = true;
        $('legacyPolicyBody').addEventListener('input', renderPreview);
        $('convertPolicy').addEventListener('click', () => {
            const markdown = $('legacyPolicyBody').value;
            doc = { version: 1, sections: [{ id: 'section-' + crypto.randomUUID().replaceAll('-', '').slice(0, 16), title: 'Policy details', intro: '', blocks: [{ type: 'text', title: '', text: markdown, items: [], stages: [], cards: [], imageUrl: '', altText: '', paths: [], sourceNote: '' }] }] };
            legacyMode = false; $('legacyPolicyEditor').hidden = true; $('structuredPolicyEditor').hidden = false;
            render(); changed();
        });
    } else {
        $('structuredPolicyEditor').hidden = false;
    }
    for (const id of ['Title', 'Summary', 'Category']) $(id).addEventListener('input', renderPreview);
    form.addEventListener('submit', event => {
        syncBody();
        if (legacyMode) return;
        const error = validateDocument();
        if (error) { event.preventDefault(); window.alert(error); }
    });
    async function loadAssets() {
        try { const response = await fetch(root.dataset.assetsUrl, { credentials: 'same-origin' }); if (response.ok) assets = await response.json(); }
        catch { assets = []; }
    }
    loadAssets().then(() => { render(); });
    render();
})();
