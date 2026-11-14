(() => {
    const form = document.getElementById('companyPolicyForm');
    const root = document.getElementById('policyBuilder');
    if (!form || !root) return;

    const $ = (id) => document.getElementById(id);
    const bodyInput = $('policyBody');
    const sectionsHost = $('policySections');
    const legacy = window.companyPolicyEditorLegacy === true;
    let legacyMode = legacy;
    let doc = JSON.parse(JSON.stringify(window.companyPolicyEditorInitial || { version: 1, sections: [] }));
    let assets = [];
    const removedFlowchartImages = new WeakMap();
    const token = form.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
    const labels = { text: 'Text', bullets: 'Bullet list', steps: 'Numbered steps', process: 'Process / approval stages', cards: 'Role cards', flowchart: 'Flowchart' };

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
        if (text === '↑' || text === '↓') {
            node.title = text === '↑' ? 'Move up' : 'Move down';
            node.setAttribute('aria-label', node.title);
        }
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
    const createSectionId = () => {
        if (window.crypto?.randomUUID) return 'section-' + window.crypto.randomUUID().replaceAll('-', '').slice(0, 16);
        return 'section-' + Date.now().toString(36) + Math.random().toString(36).slice(2, 10);
    };
    const newSection = () => ({ id: createSectionId(), title: '', intro: '', blocks: [] });
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
        select.addEventListener('change', () => {
            removedFlowchartImages.delete(block);
            block.imageUrl = select.value;
            changed(); render();
        });
        label.appendChild(select);
        card.appendChild(label);
        const imageActions = make('div', 'policy-icon-actions');
        const removedImageUrl = removedFlowchartImages.get(block);
        if (removedImageUrl) {
            imageActions.appendChild(button('Undo remove', 'admin-button admin-button--ghost', () => {
                block.imageUrl = removedImageUrl;
                removedFlowchartImages.delete(block);
                changed(); render();
            }));
        }
        imageActions.appendChild(button('Remove image from this flowchart', 'policy-remove-button', () => {
            if (!block.imageUrl) return;
            removedFlowchartImages.set(block, block.imageUrl);
            block.imageUrl = '';
            changed(); render();
        }, !block.imageUrl));
        card.appendChild(imageActions);

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
                const responseText = await response.text();
                let result;
                try { result = JSON.parse(responseText); }
                catch {
                    throw new Error(`Image upload failed (HTTP ${response.status}). The server returned an HTML error page; check the portal server log.`);
                }
                if (!response.ok || !result.success) throw new Error(result.message || 'The diagram upload failed.');
                assets = [{ name: result.name, url: result.url }, ...assets.filter(x => x.url !== result.url)];
                removedFlowchartImages.delete(block);
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
        card.appendChild(field('Image alt text (optional)', block.altText, value => { block.altText = value; }, false, 'Describe the diagram for employees using a screen reader.'));
        card.appendChild(field('Source note (optional)', block.sourceNote, value => { block.sourceNote = value; }, true, 'For example, source document and page.'));
        card.appendChild(field('Readable paths (one path per line)', (block.paths || []).join('\n'), value => { block.paths = newlineValues(value); }, true, 'Describe each branch, role lane, and decision path.'));
        if (block.imageUrl) {
            const image = make('img', 'policy-image-preview'); image.src = block.imageUrl; image.alt = block.altText || 'Selected diagram preview';
            card.appendChild(image);
        }
    }

    // Profile Account editor: structured fields, compatible with existing policy readers.
    function readProfileAccount(section) {
        if (section.id !== 'accounts' || section.blocks?.length !== 3 ||
            !section.blocks.every(block => block.type === 'text') ||
            section.blocks[1].title !== 'Active Domain Account Process') return null;
        const parseItems = (text, expression) => Array.from(text.matchAll(expression), match => match[2]);
        const requirements = [
            ...parseItems(section.blocks[0].text, /\*\*(\d+)\.\*\* ([\s\S]*?)(?=\n\n\*\*\d+\.\*\* |$)/g),
            ...parseItems(section.blocks[2].text, /\*\*(\d+)\.\*\* ([\s\S]*?)(?=\n\n\*\*\d+\.\*\* |$)/g)
        ];
        const entries = Array.from(section.blocks[1].text.matchAll(/\*\*([a-z]+)\.\*\* ([\s\S]*?)(?=\n\n\*\*[a-z]+\.\*\* |$)/gi));
        if (requirements.length < 2 || entries.length < 1) return null;
        return {
            requirements,
            entries: entries.map(match => {
                const split = match[2].indexOf('\n\n- ');
                return { text: split < 0 ? match[2] : match[2].slice(0, split),
                    points: split < 0 ? [] : match[2].slice(split + 2).split('\n').map(line => line.replace(/^- /, '')) };
            })
        };
    }

    function writeProfileAccount(section, profile) {
        section.blocks[0].text = '**1.** ' + profile.requirements[0];
        section.blocks[1].text = profile.entries.map((entry, index) =>
            '**' + String.fromCharCode(97 + index) + '.** ' + entry.text +
            (entry.points.length ? '\n\n' + entry.points.map(point => '- ' + point).join('\n') : '')).join('\n\n');
        section.blocks[2].text = profile.requirements.slice(1).map((value, index) => '**' + (index + 2) + '.** ' + value).join('\n\n');
    }

    function renderProfileAccountEditor(section, profile) {
        const editor = make('div', 'policy-block-card');
        editor.dataset.editorFormat = 'profile-account';
        appendText(editor, 'strong', 'Profile Account', 'policy-builder-kicker');

        const update = () => { writeProfileAccount(section, profile); render(); changed(); };
        const renderItemActions = (list, index, removeLabel, minimum, onChange) => {
            const actions = make('div', 'policy-icon-actions');
            actions.appendChild(button('↑', '', () => {
                if (index === 0) return;
                [list[index - 1], list[index]] = [list[index], list[index - 1]];
                onChange();
            }, index === 0));
            actions.appendChild(button('↓', '', () => {
                if (index >= list.length - 1) return;
                [list[index], list[index + 1]] = [list[index + 1], list[index]];
                onChange();
            }, index === list.length - 1));
            actions.appendChild(button(removeLabel, 'policy-remove-button', () => {
                if (list.length <= minimum) return;
                list.splice(index, 1);
                onChange();
            }, list.length <= minimum));
            return actions;
        };

        const requirements = make('div', 'policy-nested-card');
        const requirementsHead = make('div', 'policy-stage-head');
        appendText(requirementsHead, 'h3', 'Requirements');
        requirements.appendChild(requirementsHead);
        profile.requirements.forEach((value, index) => {
            const row = make('div', 'policy-nested-card');
            const head = make('div', 'policy-stage-head');
            head.appendChild(make('strong', '', 'Requirement ' + (index + 1)));
            head.appendChild(renderItemActions(profile.requirements, index, 'Remove requirement', 2, update));
            row.appendChild(head);
            row.appendChild(field('Requirement text', value, next => {
                profile.requirements[index] = next; writeProfileAccount(section, profile);
            }, true));
            requirements.appendChild(row);
        });
        requirements.appendChild(button('+ Add requirement', 'admin-button admin-button--ghost', () => {
            profile.requirements.push(''); update();
        }));
        editor.appendChild(requirements);

        const process = make('div', 'policy-nested-card');
        const processHead = make('div', 'policy-stage-head');
        appendText(processHead, 'h3', 'Active Domain Account Process');
        process.appendChild(processHead);
        profile.entries.forEach((entry, index) => {
            const label = String.fromCharCode(97 + index);
            const group = make('div', 'policy-nested-card');
            const entryHead = make('div', 'policy-stage-head');
            entryHead.appendChild(make('strong', '', 'Process ' + label + '.'));
            entryHead.appendChild(renderItemActions(profile.entries, index, 'Remove process', 1, update));
            group.appendChild(entryHead);
            group.appendChild(field('Process ' + label + '.', entry.text, value => {
                entry.text = value; writeProfileAccount(section, profile);
            }, true));
            entry.points.forEach((point, pointIndex) => {
                const bullet = make('div', 'policy-nested-card');
                const bulletHead = make('div', 'policy-stage-head');
                bulletHead.appendChild(make('strong', '', 'Bullet ' + (pointIndex + 1)));
                bulletHead.appendChild(renderItemActions(entry.points, pointIndex, 'Remove bullet', 0, update));
                bullet.appendChild(bulletHead);
                bullet.appendChild(field('Bullet text', point, value => {
                    entry.points[pointIndex] = value; writeProfileAccount(section, profile);
                }, true));
                group.appendChild(bullet);
            });
            group.appendChild(button('+ Add bullet', 'admin-button admin-button--ghost', () => {
                entry.points.push(''); update();
            }));
            process.appendChild(group);
        });
        process.appendChild(button('+ Add process', 'admin-button admin-button--ghost', () => {
            if (profile.entries.length >= 26) return;
            profile.entries.push({ text: '', points: [] }); update();
        }, profile.entries.length >= 26));
        editor.appendChild(process);
        return editor;
    }

    // End Profile Account editor.

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
            title.appendChild(titleInput);
            const actions = make('div', 'policy-icon-actions');
            actions.appendChild(button('↑', '', () => move(doc.sections, index, -1), index === 0));
            actions.appendChild(button('↓', '', () => move(doc.sections, index, 1), index === doc.sections.length - 1));
            actions.appendChild(button('Remove', 'policy-remove-button', () => { doc.sections.splice(index, 1); render(); changed(); }));
            head.appendChild(actions); card.appendChild(head); card.appendChild(title);
            card.appendChild(field('Section introduction (optional)', section.intro, value => { section.intro = value; }, true));
            const profile = readProfileAccount(section);
            if (profile) {
                card.appendChild(renderProfileAccountEditor(section, profile));
            } else {
            const blockList = make('div', 'policy-block-list');
            section.blocks.forEach((block, blockIndex) => blockList.appendChild(renderBlockEditor(block, blockIndex, section.blocks)));
            card.appendChild(blockList);
            const controls = make('div', 'policy-block-controls');
            const kind = document.createElement('select');
            const kindLabel = make('label', 'policy-field', 'Content block type');
            Object.entries(labels).forEach(([value, labelText]) => kind.appendChild(new Option(labelText, value)));
            const addBlock = button('+ Add content block', 'admin-button admin-button--ghost', () => { section.blocks.push(newBlock(kind.value)); render(); changed(); });
            kindLabel.appendChild(kind); controls.appendChild(kindLabel); controls.appendChild(addBlock); card.appendChild(controls);
            }
            sectionsHost.appendChild(card);
        });
        renderPreview(); syncBody();
    }

    function appendText(parent, tag, text, className) {
        const node = make(tag, className, text || '');
        parent.appendChild(node);
        return node;
    }
    let previewTimer;
    let previewVersion = 0;
    let previewRequest;
    function renderPreview() {
        const version = ++previewVersion;
        clearTimeout(previewTimer);
        previewRequest?.abort();
        $('policyPreviewStatus').textContent = 'Updating preview…';
        previewTimer = setTimeout(async () => {
            syncBody();
            const validation = legacyMode ? '' : validateDocument();
            if (validation) {
                $('policyPreviewStatus').textContent = 'Showing last valid preview. ' + validation;
                return;
            }
            previewRequest = new AbortController();
            try {
                const response = await fetch(root.dataset.previewUrl, {
                    method: 'POST', body: new FormData(form), signal: previewRequest.signal,
                    headers: { 'X-Requested-With': 'XMLHttpRequest' }
                });
                if (version !== previewVersion) return;
                if (!response.ok) {
                    const error = response.status === 400 ? await response.json() : null;
                    throw new Error(error?.message || 'Preview is unavailable. Check your connection and sign-in.');
                }
                if (!response.headers.get('content-type')?.includes('text/html') || response.redirected)
                    throw new Error('Sign in again to update the preview.');
                const html = await response.text();
                if (version !== previewVersion) return;
                const host = $('policyPreview');
                host.querySelector('[data-policy-reader]')?.policyReaderCleanup?.();
                host.innerHTML = html;
                window.CompanyPolicyReader?.init(host);
                $('policyPreviewStatus').textContent = 'Preview up to date';
            } catch (error) {
                if (version === previewVersion && error.name !== 'AbortError')
                    $('policyPreviewStatus').textContent = 'Showing last valid preview. ' + error.message;
            }
        }, 350);
    }
    function validateDocument() {
        if (bodyInput.value.includes('[Confirm title:')) return 'Replace each approver title placeholder with the official job title before saving.';
        if (!$('Title').value.trim()) return 'Enter a policy title.';
        if (!$('Category').value.trim()) return 'Enter a policy topic.';
        if (!doc.sections.length) return 'Add at least one section.';
        for (const section of doc.sections) {
            if (!section.title?.trim()) return 'Give every section a title.';
            if (!section.blocks?.length) return 'Add at least one content block to “' + section.title + '”.';
            const profile = readProfileAccount(section);
            if (profile && (profile.requirements.some(value => !value.trim()) || profile.entries.some(entry =>
                !entry.text.trim() || entry.points.some(point => !point.trim())))) return 'Complete every Profile Account requirement and process field.';
            for (const block of section.blocks) {
                if (block.type === 'text' && !block.text?.trim()) return 'Fill in the text block in “' + section.title + '”.';
                if (['bullets', 'steps'].includes(block.type) && !(block.items || []).some(x => x.trim())) return 'Add at least one list item in “' + section.title + '”.';
                if (block.type === 'process' && (!(block.stages || []).length || block.stages.some(stage => !stage.owner?.trim() || !stage.title?.trim() || !(stage.points || []).some(x => x.trim())))) return 'Complete each process stage in “' + section.title + '”.';
                if (block.type === 'cards' && (!(block.cards || []).length || block.cards.some(entry => !entry.label?.trim() || !entry.title?.trim() || !entry.text?.trim()))) return 'Complete each role card in “' + section.title + '”.';
                if (block.type === 'flowchart' && block.imageUrl && !block.imageUrl.startsWith('/media/company-policies/')) return 'Choose an uploaded diagram in “' + section.title + '”.';
            }
        }
        return '';
    }

    $('addPolicySection').addEventListener('click', () => {
        doc.sections.push(newSection());
        render();
        sectionsHost.lastElementChild?.scrollIntoView({ behavior: 'smooth', block: 'nearest' });
        sectionsHost.lastElementChild?.querySelector('input')?.focus({ preventScroll: true });
        window.yakultToast?.('Section added. Add a title and content block.', 'success');
    });
    if (legacy) {
        $('legacyPolicyEditor').hidden = false;
        $('structuredPolicyEditor').hidden = false;
        root.classList.add('is-legacy');
        sectionsHost.hidden = true;
        $('structuredPolicyEditor').querySelector('.policy-builder-heading').hidden = true;
        $('legacyPolicyBody').addEventListener('input', renderPreview);
        $('convertPolicy').addEventListener('click', () => {
            const markdown = $('legacyPolicyBody').value;
            doc = { version: 1, sections: [{ id: createSectionId(), title: 'Policy details', intro: '', blocks: [{ type: 'text', title: '', text: markdown, items: [], stages: [], cards: [], imageUrl: '', altText: '', paths: [], sourceNote: '' }] }] };
            legacyMode = false; $('legacyPolicyEditor').hidden = true; $('structuredPolicyEditor').hidden = false;
            root.classList.remove('is-legacy');
            sectionsHost.hidden = false;
            $('structuredPolicyEditor').querySelector('.policy-builder-heading').hidden = false;
            render(); changed();
            window.yakultToast?.('Policy converted to structured sections.', 'success');
        });
    } else {
        $('structuredPolicyEditor').hidden = false;
    }
    for (const id of ['Title', 'Summary', 'Category', 'EffectiveDate']) $(id).addEventListener('input', renderPreview);
    form.addEventListener('submit', event => {
        syncBody();
        if (legacyMode) return;
        const error = validateDocument();
        if (error) { event.preventDefault(); window.yakultToast?.(error, 'error'); return; }
        window.yakultToast?.('Saving policy…', 'info');
    });
    async function loadAssets() {
        try { const response = await fetch(root.dataset.assetsUrl, { credentials: 'same-origin' }); if (response.ok) assets = await response.json(); }
        catch { assets = []; }
    }
    loadAssets().then(() => { render(); });
    render();
})();
