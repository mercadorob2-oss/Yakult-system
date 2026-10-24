(() => {
    'use strict';
    const prefix = 'yakult-course:v1\n';
    const labels = { text: 'Text & heading', image: 'Image & caption', video: 'Video & transcript', callout: 'Highlight / warning', steps: 'Step-by-step', accordion: 'Expandable details', comparison: 'Comparison', quiz: 'Knowledge check', download: 'Download / reference' };
    const uid = () => 'lesson-' + (globalThis.crypto?.randomUUID?.() || Date.now().toString(36) + Math.random().toString(36).slice(2));
    const newBlock = type => ({ type, title: '', text: '', extra: '', url: '', options: type === 'quiz' ? ['Yes', 'No'] : [], answer: 0 });
    const newLesson = () => ({ id: uid(), title: 'New lesson', minutes: 5, blocks: [newBlock('text')] });
    function parse(value) {
        try {
            const doc = value.startsWith(prefix) ? JSON.parse(value.slice(prefix.length)) : null;
            if (doc?.version !== 1 || !Array.isArray(doc.objectives) || !Array.isArray(doc.modules) || !doc.modules.length) return null;
            if (!doc.modules.every(module => module && Array.isArray(module.lessons) && module.lessons.length && module.lessons.every(lesson => lesson && Array.isArray(lesson.blocks) && lesson.blocks.length && lesson.blocks.every(block => block && labels[block.type] && Array.isArray(block.options))))) return null;
            return doc;
        } catch (_) { return null; }
    }
    function safeUrl(value) {
        if (!value || /[\\\u0000-\u0020]/.test(value)) return false;
        if (value.startsWith('/media/')) { try { return !decodeURIComponent(value).includes('..'); } catch (_) { return false; } }
        try { const url = new URL(value); return url.protocol === 'https:' && !url.username && !url.password; } catch (_) { return false; }
    }
    function element(tag, text, className) { const el = document.createElement(tag); if (text !== undefined) el.textContent = text; if (className) el.className = className; return el; }
    function markdown(container, value) {
        function inline(parent, text) {
            const pattern = /\*\*([^*]+)\*\*|\[([^\]]+)\]\(([^)\s]+)\)/g;
            let position = 0, match;
            while ((match = pattern.exec(text))) {
                parent.appendChild(document.createTextNode(text.slice(position, match.index)));
                if (match[1]) parent.appendChild(element('strong', match[1]));
                else if (safeUrl(match[3])) { const link = element('a', match[2]); link.href = match[3]; link.target = '_blank'; link.rel = 'noopener noreferrer'; parent.appendChild(link); }
                else parent.appendChild(document.createTextNode(match[2]));
                position = pattern.lastIndex;
            }
            parent.appendChild(document.createTextNode(text.slice(position)));
        }
        let paragraph = null, list = null;
        String(value || '').split(/\r?\n/).forEach(line => {
            const trimmed = line.trim(), heading = /^(#{1,3})\s+(.+)$/.exec(trimmed), item = /^(?:- |\d+\. )(.+)$/.exec(trimmed);
            if (!trimmed) { paragraph = list = null; return; }
            if (heading) { const title = element('h4'); inline(title, heading[2]); container.appendChild(title); paragraph = list = null; return; }
            if (item) {
                const tag = trimmed.startsWith('- ') ? 'UL' : 'OL';
                if (!list || list.tagName !== tag) { list = element(tag.toLowerCase()); container.appendChild(list); }
                const li = element('li'); inline(li, item[1]); list.appendChild(li); paragraph = null; return;
            }
            list = null;
            if (!paragraph) { paragraph = element('p'); container.appendChild(paragraph); } else paragraph.appendChild(document.createTextNode(' '));
            inline(paragraph, trimmed);
        });
    }
    function previewBlocks(container, blocks) {
        container.replaceChildren();
        blocks.forEach(block => {
            const section = element('section', undefined, 'builder-preview-block');
            section.dataset.blockType = block.type;
            section.appendChild(element('small', labels[block.type]));
            if (block.title) section.appendChild(element('h4', block.title));
            if (block.type === 'image' && safeUrl(block.url)) { const img = element('img'); img.src = block.url; img.alt = block.title || ''; section.appendChild(img); }
            if (block.type === 'comparison') { const columns = element('div', undefined, 'builder-preview-columns'); const left = element('div'), right = element('div'); markdown(left, block.text); markdown(right, block.extra); columns.append(left, right); section.appendChild(columns); }
            else if (block.type === 'steps') { const list = element('ol'); block.text.split('\n').filter(x => x.trim()).forEach(line => list.appendChild(element('li', line))); section.appendChild(list); }
            else if (block.text) markdown(section, block.text);
            if (block.type === 'quiz') (block.options || []).forEach(option => section.appendChild(element('p', '○ ' + option)));
            if (['video', 'download'].includes(block.type)) section.appendChild(element('p', safeUrl(block.url) ? '↗ ' + block.url : 'Add an HTTPS URL or /media/ path.'));
            container.appendChild(section);
        });
    }
    window.YakultCourseBuilder = {
        create({ body, onChange, previewOnly = false }) {
            const $ = id => document.getElementById(id);
            const root = $('courseBuilder');
            let doc = null, moduleIndex = 0, lessonIndex = 0;
            const activeUploads = new Set();
            const lesson = () => doc.modules[moduleIndex].lessons[lessonIndex];
            const announce = message => { $('builderStatus').textContent = message; };
            function sync() {
                body.value = prefix + JSON.stringify(doc);
                $('contentType').value = 'Course';
                $('builderLessonCount').textContent = `${doc.modules.length} modules · ${doc.modules.reduce((n, m) => n + m.lessons.length, 0)} lessons`;
                onChange();
            }
            function control(label, action, disabled = false) {
                const button = element('button', label, 'studio-btn'); button.type = 'button'; button.disabled = disabled; button.addEventListener('click', action); return button;
            }
            function move(array, index, delta) { const target = index + delta; if (target < 0 || target >= array.length) return; [array[index], array[target]] = [array[target], array[index]]; }
            function renderOutline() {
                const outline = $('builderOutline'); outline.replaceChildren();
                doc.modules.forEach((module, m) => {
                    const section = element('section');
                    const field = element('label', `Module ${m + 1}`);
                    const title = element('input', undefined, 'studio-input'); title.value = module.title; title.maxLength = 180;
                    title.addEventListener('input', () => { module.title = title.value; sync(); });
                    field.appendChild(title); section.appendChild(field);
                    const actions = element('div', undefined, 'builder-row builder-small-actions');
                    actions.append(control('↑', () => { move(doc.modules, m, -1); moduleIndex = m - 1; lessonIndex = 0; render(); sync(); }, m === 0), control('↓', () => { move(doc.modules, m, 1); moduleIndex = m + 1; lessonIndex = 0; render(); sync(); }, m === doc.modules.length - 1));
                    actions.children[0].setAttribute('aria-label', `Move module ${m + 1} up`); actions.children[1].setAttribute('aria-label', `Move module ${m + 1} down`);
                    actions.appendChild(control('Remove', () => { if (!confirm('Remove this module and all its lessons?')) return; doc.modules.splice(m, 1); moduleIndex = Math.min(moduleIndex, doc.modules.length - 1); lessonIndex = 0; render(); sync(); }, doc.modules.length === 1));
                    section.appendChild(actions);
                    module.lessons.forEach((item, l) => {
                        const row = element('div', undefined, 'builder-outline-row');
                        const button = control(item.title || 'Untitled lesson', () => { moduleIndex = m; lessonIndex = l; render(); onChange(); });
                        button.classList.add('builder-lesson-link'); button.setAttribute('aria-pressed', String(moduleIndex === m && lessonIndex === l));
                        row.appendChild(button);
                        const up = control('↑', () => { move(module.lessons, l, -1); moduleIndex = m; lessonIndex = l - 1; render(); sync(); }, l === 0); up.setAttribute('aria-label', `Move ${item.title} up`);
                        const down = control('↓', () => { move(module.lessons, l, 1); moduleIndex = m; lessonIndex = l + 1; render(); sync(); }, l === module.lessons.length - 1); down.setAttribute('aria-label', `Move ${item.title} down`);
                        row.append(up, down); section.appendChild(row);
                    });
                    section.appendChild(control('+ Lesson', () => { module.lessons.push(newLesson()); moduleIndex = m; lessonIndex = module.lessons.length - 1; render(); sync(); }));
                    outline.appendChild(section);
                });
            }
            function field(parent, label, value, update, multiline = false, maximum = 50000) {
                const wrapper = element('label', label);
                const input = element(multiline ? 'textarea' : 'input', undefined, multiline ? 'studio-textarea' : 'studio-input');
                input.value = value || ''; input.maxLength = maximum; if (multiline) input.rows = 4;
                input.addEventListener('input', () => { update(input.value); sync(); });
                wrapper.appendChild(input); parent.appendChild(wrapper); return input;
            }
            function renderBlocks() {
                const container = $('builderBlocks'); container.replaceChildren();
                lesson().blocks.forEach((block, index) => {
                    const card = element('section', undefined, 'builder-block');
                    const head = element('div', undefined, 'builder-row'); head.appendChild(element('strong', `${index + 1}. ${labels[block.type]}`));
                    const actions = element('div', undefined, 'builder-row builder-small-actions');
                    actions.append(control('↑', () => { move(lesson().blocks, index, -1); renderBlocks(); sync(); }, index === 0), control('↓', () => { move(lesson().blocks, index, 1); renderBlocks(); sync(); }, index === lesson().blocks.length - 1), control('Remove', () => { if (!confirm('Remove this content block?')) return; lesson().blocks.splice(index, 1); renderBlocks(); sync(); }, lesson().blocks.length === 1));
                    actions.children[0].setAttribute('aria-label', `Move block ${index + 1} up`); actions.children[1].setAttribute('aria-label', `Move block ${index + 1} down`);
                    head.appendChild(actions); card.appendChild(head);
                    const titleLabel = block.type === 'quiz' ? 'Question' : block.type === 'image' ? 'Image description (alt text)' : 'Heading / label';
                    field(card, titleLabel, block.title, value => { block.title = value; }, false, 500);
                    if (['image', 'video', 'download'].includes(block.type)) {
                        const urlInput = field(card, 'HTTPS URL or /media/ path', block.url, value => { block.url = value; }, false, 2000);
                        const uploadRow = element('div', undefined, 'builder-block-upload');
                        const fileLabel = element('label', 'Or choose a file');
                        const fileInput = element('input', undefined, 'studio-input'); fileInput.type = 'file';
                        fileInput.accept = block.type === 'image' ? '.jpg,.jpeg,.png,.webp,.gif' : block.type === 'video' ? '.mp4,.webm' : '.pdf,.doc,.docx,.xls,.xlsx,.ppt,.pptx';
                        fileLabel.appendChild(fileInput);
                        const uploadStatus = element('span', '', 'builder-upload-status'); uploadStatus.setAttribute('role', 'status');
                        const upload = control('Upload file', async () => {
                            const file = fileInput.files[0];
                            if (!file) { uploadStatus.textContent = 'Choose a file first.'; return; }
                            const maximumMb = block.type === 'image' ? 5 : block.type === 'video' ? 500 : 25;
                            if (file.size > maximumMb * 1024 * 1024) { uploadStatus.textContent = `Choose a file under ${maximumMb} MB.`; return; }
                            const cancellation = new AbortController(), documentAtUpload = doc;
                            activeUploads.add(cancellation); upload.disabled = true; uploadStatus.textContent = 'Uploading…';
                            try {
                                const payload = new FormData(); payload.append('file', file); payload.append('target', block.type === 'image' ? 'thumbnail' : 'media');
                                const token = document.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
                                const response = await fetch('/Admin/LearningContent/Upload', { method: 'POST', headers: { RequestVerificationToken: token }, body: payload, signal: cancellation.signal });
                                const result = await response.json();
                                if (!response.ok || result.success === false || !safeUrl(result.url)) throw new Error(result.message || 'Upload failed. Please try again.');
                                if (doc === documentAtUpload) { block.url = result.url; urlInput.value = result.url; sync(); uploadStatus.textContent = 'File ready.'; }
                            } catch (error) { if (error.name !== 'AbortError') uploadStatus.textContent = error.message || 'Upload failed.'; }
                            finally { activeUploads.delete(cancellation); upload.disabled = false; }
                        });
                        if (previewOnly) { fileInput.disabled = true; upload.disabled = true; uploadStatus.textContent = 'File uploads are disabled in demo preview.'; }
                        uploadRow.append(fileLabel, upload, uploadStatus); card.appendChild(uploadRow);
                    }
                    field(card, block.type === 'quiz' ? 'Answer explanation' : block.type === 'steps' ? 'Steps (one per line)' : block.type === 'image' ? 'Caption' : block.type === 'video' ? 'Transcript / notes' : block.type === 'comparison' ? 'Left column (Markdown)' : 'Content (Markdown supported)', block.text, value => { block.text = value; }, true);
                    if (block.type === 'comparison') field(card, 'Right column (Markdown)', block.extra, value => { block.extra = value; }, true);
                    if (block.type === 'quiz') {
                        field(card, 'Choices (one per line, 2 to 6)', block.options.join('\n'), value => { block.options = value.split('\n').map(x => x.trim()).filter(Boolean); }, true, 3500);
                        const input = field(card, 'Correct choice number (starting at 1)', String(block.answer + 1), value => { block.answer = Number(value) - 1; }, false, 1); input.type = 'number'; input.min = 1; input.max = 6;
                    }
                    container.appendChild(card);
                });
            }
            function render() {
                renderOutline(); renderBlocks();
                $('builderLessonTitle').value = lesson().title;
                $('builderLessonMinutes').value = lesson().minutes;
                $('builderEditingLabel').textContent = `Module ${moduleIndex + 1} · Lesson ${lessonIndex + 1}`;
                $('builderRemoveLesson').disabled = doc.modules[moduleIndex].lessons.length === 1;
            }
            function setMode() {
                root.hidden = !doc; $('courseBuilderLaunch').hidden = !!doc; $('legacyBodyEditor').hidden = !!doc;
                $('contentType').disabled = !!doc;
            }
            function enable() {
                if (doc) return;
                if (body.value.startsWith('yakult-course:')) { announce('This course format could not be opened. Keep the original content and check its version.'); return; }
                const first = newLesson(); first.title = $('title').value.trim() || 'Introduction'; first.blocks[0].text = body.value;
                const media = $('mediaUrl').value.trim(); if (media) first.blocks.unshift({ ...newBlock('video'), title: 'Watch the lesson', url: media });
                doc = { version: 1, audience: 'All employees', level: 'Foundation', objectives: [], modules: [{ title: 'Getting started', lessons: [first] }] };
                moduleIndex = lessonIndex = 0; loadSettings(); setMode(); render(); sync();
            }
            function loadSettings() { $('builderAudience').value = doc.audience; $('builderLevel').value = doc.level; $('builderObjectives').value = doc.objectives.join('\n'); }
            $('enableCourseBuilder').addEventListener('click', enable);
            $('builderAudience').addEventListener('input', event => { doc.audience = event.target.value; sync(); });
            $('builderLevel').addEventListener('change', event => { doc.level = event.target.value; sync(); });
            $('builderObjectives').addEventListener('input', event => { doc.objectives = event.target.value.split('\n').map(x => x.trim()).filter(Boolean); sync(); });
            $('builderLessonTitle').addEventListener('input', event => { lesson().title = event.target.value; renderOutline(); sync(); });
            $('builderLessonMinutes').addEventListener('input', event => { lesson().minutes = Number(event.target.value); sync(); });
            $('builderAddModule').addEventListener('click', () => { doc.modules.push({ title: 'New module', lessons: [newLesson()] }); moduleIndex = doc.modules.length - 1; lessonIndex = 0; render(); sync(); });
            $('builderDuplicateLesson').addEventListener('click', () => { const copy = JSON.parse(JSON.stringify(lesson())); copy.id = uid(); copy.title += ' (copy)'; doc.modules[moduleIndex].lessons.splice(lessonIndex + 1, 0, copy); lessonIndex++; render(); sync(); });
            $('builderRemoveLesson').addEventListener('click', () => { if (!confirm('Remove this lesson and its content?')) return; doc.modules[moduleIndex].lessons.splice(lessonIndex, 1); lessonIndex = Math.max(0, lessonIndex - 1); render(); sync(); });
            $('builderAddBlock').addEventListener('click', () => { lesson().blocks.push(newBlock($('builderBlockType').value)); renderBlocks(); sync(); });
            root.querySelectorAll('[data-lesson-template]').forEach(button => button.addEventListener('click', () => {
                if (lesson().blocks.some(block => block.text || block.title || block.url) && !confirm('Replace the selected lesson’s content with this template?')) return;
                const type = button.dataset.lessonTemplate;
                const text = { ...newBlock('text'), title: 'What you’ll learn', text: 'Explain the goal of this lesson and why it matters.' };
                const reflection = { ...newBlock('quiz'), title: 'Which action is the safest?', options: ['Check the request using an official contact', 'Act immediately without checking'], text: 'Use a trusted contact to verify an unexpected request.' };
                lesson().blocks = type === 'video' ? [text, { ...newBlock('video'), title: 'Watch the lesson' }, { ...newBlock('callout'), title: 'Key takeaway', text: 'Summarize the most important action.' }]
                    : type === 'walkthrough' ? [text, { ...newBlock('steps'), title: 'Follow these steps', text: 'Open the system using its official link.\nSelect the required action.\nReview the result.' }, { ...newBlock('image'), title: 'Describe the system screen' }]
                    : [text, { ...newBlock('comparison'), title: 'Spot the difference', text: '## Safe approach\nVerify the sender through a trusted channel.', extra: '## Warning signs\nUnexpected urgency or an unfamiliar link.' }, reflection];
                renderBlocks(); sync(); announce('Template applied. Review all content and add any required media before saving.');
            }));
            return {
                get enabled() { return !!doc; }, enable,
                load(value) { activeUploads.forEach(upload => upload.abort()); activeUploads.clear(); doc = parse(value); moduleIndex = lessonIndex = 0; setMode(); if (doc) { loadSettings(); render(); } },
                preview(container) { if (doc) previewBlocks(container, lesson().blocks); },
                validate() {
                    if (!doc) return;
                    if (activeUploads.size) throw new Error('Wait for the lesson files to finish uploading before saving.');
                    if (!doc.audience.trim() || doc.objectives.length > 20) throw new Error('Add an audience and use up to 20 objectives.');
                    if (doc.modules.length > 100 || doc.modules.reduce((n, m) => n + m.lessons.length, 0) > 500) throw new Error('Use up to 100 modules and 500 lessons per course.');
                    doc.modules.forEach(module => {
                        if (!module.title.trim() || module.lessons.length > 100) throw new Error('Give each module a title and use up to 100 lessons per module.');
                        module.lessons.forEach(item => {
                            if (!item.title.trim() || !Number.isInteger(item.minutes) || item.minutes < 1 || item.minutes > 480) throw new Error('Each lesson needs a title and a duration from 1 to 480 minutes.');
                            if (item.blocks.length > 100) throw new Error('Use up to 100 blocks in a lesson.');
                            item.blocks.forEach(block => {
                                if (['image', 'video', 'download'].includes(block.type) && !safeUrl(block.url)) throw new Error(`Add a valid HTTPS URL or /media/ path in “${item.title}”.`);
                                if (block.type === 'quiz' && (!block.title.trim() || block.options.length < 2 || block.options.length > 6 || !Number.isInteger(block.answer) || block.answer < 0 || block.answer >= block.options.length)) throw new Error(`Check the question, choices, and correct answer in “${item.title}”.`);
                            });
                        });
                    });
                }
            };
        }
    };
})();
