(() => {
    'use strict';

    const root = document.getElementById('employeeResourcesAdminApp');
    if (!root || root.dataset.frontendMock !== 'true') return;

    const STORAGE_KEY = root.dataset.storageKey || 'yakult.employeeResources.adminMock.v1';
    const MAX_FILE_BYTES = 100 * 1024 * 1024;
    const ALLOWED_TYPES = new Set(['Policy', 'Form', 'Guide', 'FAQ', 'Checklist', 'Directory', 'Handbook', 'Reference', 'Template']);
    const ALLOWED_EXTENSIONS = new Set(['.pdf', '.doc', '.docx', '.xls', '.xlsx', '.ppt', '.pptx', '.zip', '.png', '.jpg', '.jpeg']);
    const IMAGE_EXTENSIONS = new Set(['.png', '.jpg', '.jpeg']);
    const STORAGE_VERSION = 1;
    const canPublish = root.dataset.canPublish === 'true';

    const $ = id => document.getElementById(id);
    const form = $('employeeResourceForm');
    const library = document.querySelector('[data-resource-library-list]');
    const seedElement = $('employeeResourcesMockSeed');
    const mockStateMessage = $('mockStateMessage');
    const errorSummary = $('resourceAdminErrorSummary');
    const errorList = $('resourceAdminErrorList');

    if (!form || !library || !seedElement) return;

    const fields = {
        title: $('resourceTitle'),
        slug: $('resourceSlug'),
        resourceType: $('resourceType'),
        categoryId: $('resourceCategoryId'),
        ownerDepartmentId: $('resourceOwnerDepartmentId'),
        version: $('resourceVersion'),
        reviewDateUtc: $('resourceReviewDateUtc'),
        publishStartUtc: $('resourcePublishStartUtc'),
        publishEndUtc: $('resourcePublishEndUtc'),
        summary: $('resourceSummary'),
        overview: $('resourceOverview'),
        highlightsText: $('resourceHighlightsText')
    };

    const errors = {
        title: $('resourceTitleError'),
        slug: $('resourceSlugError'),
        resourceType: $('resourceTypeError'),
        categoryId: $('resourceCategoryError'),
        ownerDepartmentId: $('resourceOwnerError'),
        version: $('resourceVersionError'),
        reviewDateUtc: $('resourceReviewDateError'),
        publishStartUtc: $('resourcePublishStartError'),
        publishEndUtc: $('resourcePublishEndError'),
        summary: $('resourceSummaryError'),
        overview: $('resourceOverviewError'),
        highlightsText: $('resourceHighlightsError')
    };

    const editorEyebrow = $('resourceEditorEyebrow');
    const editorTitle = $('resourceEditorTitle');
    const editorMeta = $('resourceEditorMeta');
    const editorStatus = $('resourceEditorStatus');
    const saveButton = $('saveResourceDraftButton');
    const newResourceLink = $('newEmployeeResourceLink');
    const resetButton = $('resetEmployeeResourceMock');
    const deleteDraftButton = $('mockDeleteDraft');
    const libraryCount = $('resourceAdminLibraryCount');
    const mockAttachmentSection = $('mockAttachmentSection');
    const mockAttachmentForm = $('mockAttachmentForm');
    const mockAttachmentFiles = $('mockAttachmentFiles');
    const mockAttachmentReplaceInput = $('mockAttachmentReplaceInput');
    const mockAttachmentDescription = $('mockAttachmentDescription');
    const mockAttachmentList = $('mockAttachmentList');
    const mockAttachmentStatus = $('mockAttachmentStatus');
    const mockWorkflowSection = $('mockWorkflowSection');
    const mockWorkflowActions = $('mockWorkflowActions');
    const mockWorkflowHint = $('mockWorkflowHint');
    const mockPreviewSection = $('mockPreviewSection');
    const mockPreviewBody = $('mockPreviewBody');

    let demoResources = [];
    let drafts = loadDrafts();
    let currentDraft = null;
    let dirty = false;
    let slugWasEdited = false;
    let generatedSlug = '';
    let replacementTargetId = null;
    const pendingFiles = new Map();
    const objectUrls = new Map();

    function showToast(message, type = 'info') {
        if (window.yakultToast) {
            window.yakultToast(message, type);
            return;
        }
        if (mockStateMessage) mockStateMessage.textContent = message;
    }

    function announce(message) {
        if (mockStateMessage) mockStateMessage.textContent = message;
    }

    function makeId(prefix) {
        const random = window.crypto?.randomUUID?.() || `${Date.now()}-${Math.random().toString(36).slice(2, 10)}`;
        return `${prefix}-${random}`;
    }

    function nowIso() {
        return new Date().toISOString();
    }

    function normalize(value) {
        return String(value || '').trim().toLocaleLowerCase();
    }

    function getExtension(fileName) {
        const name = String(fileName || '').toLowerCase();
        const index = name.lastIndexOf('.');
        return index >= 0 ? name.slice(index) : '';
    }

    function formatBytes(bytes) {
        const value = Number(bytes) || 0;
        if (value < 1024) return `${value} B`;
        const kb = value / 1024;
        if (kb < 1024) return `${kb.toFixed(kb >= 10 ? 0 : 1)} KB`;
        const mb = kb / 1024;
        if (mb < 1024) return `${mb.toFixed(mb >= 10 ? 0 : 1)} MB`;
        return `${(mb / 1024).toFixed(1)} GB`;
    }

    function formatDate(value, fallback = 'Not set') {
        if (!value) return fallback;
        const date = new Date(value);
        if (Number.isNaN(date.getTime())) return fallback;
        return new Intl.DateTimeFormat(undefined, { month: 'short', day: 'numeric', year: 'numeric' }).format(date);
    }

    function formatDateTime(value, fallback = 'Not saved') {
        if (!value) return fallback;
        const date = new Date(value);
        if (Number.isNaN(date.getTime())) return fallback;
        return new Intl.DateTimeFormat(undefined, { dateStyle: 'medium', timeStyle: 'short' }).format(date);
    }

    function toInputDate(value) {
        if (!value) return '';
        const date = new Date(value);
        if (Number.isNaN(date.getTime())) return String(value).slice(0, 16);
        const pad = number => String(number).padStart(2, '0');
        return `${date.getFullYear()}-${pad(date.getMonth() + 1)}-${pad(date.getDate())}T${pad(date.getHours())}:${pad(date.getMinutes())}`;
    }

    function fromInputDate(value) {
        if (!value) return null;
        const date = new Date(value);
        return Number.isNaN(date.getTime()) ? null : date.toISOString();
    }

    function slugify(value) {
        return normalize(value)
            .replace(/[^a-z0-9]+/g, '-')
            .replace(/^-+|-+$/g, '')
            .slice(0, 200);
    }

    function statusClass(value) {
        return normalize(value).replace(/[^a-z0-9]+/g, '-') || 'draft';
    }

    function parseSeed() {
        try {
            const parsed = JSON.parse(seedElement.textContent || '[]');
            return Array.isArray(parsed) ? parsed : [];
        } catch {
            announce('The frontend demo catalog could not be read. Start with a new local draft.');
            return [];
        }
    }

    function normalizeDemoAttachment(attachment, resourceSlug, index) {
        const fileName = attachment.fileName || `Demo attachment ${index + 1}`;
        const extension = getExtension(fileName);
        return {
            clientId: `demo-attachment-${resourceSlug}-${index}`,
            fileName,
            format: String(attachment.format || extension.replace('.', '') || 'FILE').toUpperCase(),
            sizeLabel: attachment.sizeLabel || 'Demo metadata',
            sizeBytes: Number(attachment.sizeBytes) || 0,
            description: attachment.description || 'Demo attachment metadata',
            source: 'demo',
            isDemoOnly: true,
            referenceOnly: true,
            needsFile: false,
            uploadReady: false
        };
    }

    function normalizeDemoResource(resource, index) {
        const slug = resource.slug || `demo-resource-${index + 1}`;
        const attachments = Array.isArray(resource.attachments)
            ? resource.attachments.map((attachment, attachmentIndex) => normalizeDemoAttachment(attachment, slug, attachmentIndex))
            : [];
        return {
            clientId: `demo:${slug}`,
            kind: 'demo',
            isDemoData: true,
            resourceId: null,
            rowVersion: '',
            sourceTemplateSlug: null,
            slug,
            title: resource.title || 'Untitled demo resource',
            summary: resource.summary || '',
            overview: resource.overview || '',
            categoryId: resource.categoryId ?? '',
            category: resource.category || '',
            categorySlug: resource.categorySlug || '',
            resourceType: resource.resourceType === 'Forms' ? 'Form' : (resource.resourceType || 'Guide'),
            ownerDepartmentId: resource.ownerDepartmentId ?? '',
            ownerDepartment: resource.ownerDepartment || '',
            version: resource.version || 'v1.0',
            status: 'Published',
            publishStartUtc: resource.publishStartUtc || null,
            publishEndUtc: resource.publishEndUtc || null,
            reviewDateUtc: resource.reviewDateUtc || null,
            lastUpdatedUtc: resource.lastUpdatedUtc || null,
            createdAtUtc: resource.createdAtUtc || null,
            authorName: resource.authorName || null,
            approverName: resource.approverName || null,
            highlights: Array.isArray(resource.highlights) ? resource.highlights.filter(Boolean) : [],
            attachments,
            revisions: []
        };
    }

    function normalizeStoredAttachment(attachment, index, draftId) {
        const source = attachment.source === 'demo' || attachment.isDemoOnly ? 'demo' : 'local';
        return {
            clientId: attachment.clientId || `attachment-${draftId}-${index}`,
            fileName: attachment.fileName || 'Unnamed attachment',
            format: String(attachment.format || getExtension(attachment.fileName).replace('.', '') || 'FILE').toUpperCase(),
            sizeLabel: attachment.sizeLabel || formatBytes(attachment.sizeBytes),
            sizeBytes: Number(attachment.sizeBytes) || 0,
            description: attachment.description || '',
            source,
            isDemoOnly: source === 'demo',
            referenceOnly: source === 'demo' || Boolean(attachment.referenceOnly),
            needsFile: source === 'local',
            uploadReady: false
        };
    }

    function normalizeStoredDraft(draft, index) {
        const clientId = draft.clientId || `draft-${index + 1}`;
        const status = ['Draft', 'Rejected', 'Published', 'Archived'].includes(draft.status) ? draft.status : 'Draft';
        return {
            clientId,
            kind: 'local',
            isDemoData: false,
            resourceId: null,
            rowVersion: '',
            sourceTemplateSlug: draft.sourceTemplateSlug || null,
            slug: draft.slug || '',
            title: draft.title || '',
            summary: draft.summary || '',
            overview: draft.overview || '',
            categoryId: draft.categoryId ?? '',
            category: draft.category || '',
            categorySlug: draft.categorySlug || '',
            resourceType: draft.resourceType === 'Forms' ? 'Form' : (draft.resourceType || 'Guide'),
            ownerDepartmentId: draft.ownerDepartmentId ?? '',
            ownerDepartment: draft.ownerDepartment || '',
            version: draft.version || 'v1.0',
            status,
            publishStartUtc: draft.publishStartUtc || null,
            publishEndUtc: draft.publishEndUtc || null,
            reviewDateUtc: draft.reviewDateUtc || null,
            lastUpdatedUtc: draft.lastUpdatedUtc || draft.updatedAtUtc || null,
            createdAtUtc: draft.createdAtUtc || null,
            authorName: draft.authorName || 'Frontend mock user',
            approverName: draft.approverName || null,
            highlights: Array.isArray(draft.highlights) ? draft.highlights.filter(Boolean).slice(0, 20) : [],
            attachments: Array.isArray(draft.attachments)
                ? draft.attachments.map((attachment, attachmentIndex) => normalizeStoredAttachment(attachment, attachmentIndex, clientId))
                : [],
            revisions: Array.isArray(draft.revisions) ? draft.revisions : []
        };
    }

    function loadDrafts() {
        try {
            const raw = window.localStorage.getItem(STORAGE_KEY);
            if (!raw) return [];
            const parsed = JSON.parse(raw);
            if (!parsed || parsed.version !== STORAGE_VERSION || !Array.isArray(parsed.drafts)) return [];
            return parsed.drafts.map(normalizeStoredDraft);
        } catch {
            return [];
        }
    }

    function persistDrafts(nextDrafts) {
        try {
            window.localStorage.setItem(STORAGE_KEY, JSON.stringify({ version: STORAGE_VERSION, drafts: nextDrafts }));
            drafts = nextDrafts;
            return true;
        } catch {
            showToast('This browser could not save the local draft. Check storage permissions or available space.', 'error');
            return false;
        }
    }

    function createEmptyDraft() {
        return {
            clientId: null,
            kind: 'local',
            isDemoData: false,
            resourceId: null,
            rowVersion: '',
            sourceTemplateSlug: null,
            slug: '',
            title: '',
            summary: '',
            overview: '',
            categoryId: '',
            category: '',
            categorySlug: '',
            resourceType: 'Guide',
            ownerDepartmentId: '',
            ownerDepartment: '',
            version: 'v1.0',
            status: 'Draft',
            publishStartUtc: null,
            publishEndUtc: null,
            reviewDateUtc: null,
            lastUpdatedUtc: null,
            createdAtUtc: null,
            authorName: 'Frontend mock user',
            approverName: null,
            highlights: [],
            attachments: [],
            revisions: []
        };
    }

    function getSelectedOption(select) {
        return select?.selectedOptions?.[0] || null;
    }

    function findOptionValue(select, label) {
        if (!select || !label) return '';
        const desired = normalize(label);
        const option = Array.from(select.options).find(item => normalize(item.dataset.name || item.textContent) === desired);
        return option?.value || '';
    }

    function ensureOption(select, value, label) {
        if (!select || !value) return;
        let option = Array.from(select.options).find(item => item.value === String(value));
        if (!option) {
            option = document.createElement('option');
            option.value = String(value);
            option.textContent = label || String(value);
            option.dataset.name = label || String(value);
            select.appendChild(option);
        }
        select.value = String(value);
    }

    function optionValueForLabel(select, label, prefix) {
        if (!label) return '';
        return findOptionValue(select, label) || (() => {
            const value = `${prefix}-${slugify(label)}`;
            ensureOption(select, value, label);
            return value;
        })();
    }

    function parseHighlights(value) {
        return String(value || '')
            .split(/\r?\n/)
            .map(item => item.trim())
            .filter(Boolean);
    }

    function readForm(base) {
        const categoryOption = getSelectedOption(fields.categoryId);
        const ownerOption = getSelectedOption(fields.ownerDepartmentId);
        const source = base || createEmptyDraft();
        return {
            ...source,
            title: fields.title.value.trim(),
            slug: fields.slug.value.trim().toLowerCase(),
            resourceType: fields.resourceType.value.trim(),
            categoryId: fields.categoryId.value,
            category: categoryOption?.dataset.name || categoryOption?.textContent.trim() || '',
            ownerDepartmentId: fields.ownerDepartmentId.value,
            ownerDepartment: ownerOption?.dataset.name || ownerOption?.textContent.trim() || '',
            version: fields.version.value.trim(),
            reviewDateUtc: fromInputDate(fields.reviewDateUtc.value),
            publishStartUtc: fromInputDate(fields.publishStartUtc.value),
            publishEndUtc: fromInputDate(fields.publishEndUtc.value),
            summary: fields.summary.value.trim(),
            overview: fields.overview.value.trim(),
            highlights: parseHighlights(fields.highlightsText.value),
            attachments: source.attachments || []
        };
    }

    function populateForm(resource) {
        const source = resource || createEmptyDraft();
        fields.title.value = source.title || '';
        fields.slug.value = source.slug || '';
        fields.resourceType.value = source.resourceType || 'Guide';
        fields.categoryId.value = '';
        fields.ownerDepartmentId.value = '';
        ensureOption(fields.categoryId, source.categoryId, source.category);
        ensureOption(fields.ownerDepartmentId, source.ownerDepartmentId, source.ownerDepartment);
        fields.version.value = source.version || 'v1.0';
        fields.reviewDateUtc.value = toInputDate(source.reviewDateUtc);
        fields.publishStartUtc.value = toInputDate(source.publishStartUtc);
        fields.publishEndUtc.value = toInputDate(source.publishEndUtc);
        fields.summary.value = source.summary || '';
        fields.overview.value = source.overview || '';
        fields.highlightsText.value = Array.isArray(source.highlights) ? source.highlights.join('\n') : '';
        generatedSlug = source.slug || '';
        slugWasEdited = Boolean(source.slug);
        clearValidation();
        updateEditorHeader();
        updateFormState();
        renderMockSections();
    }

    function updateEditorHeader() {
        const status = currentDraft?.status || 'Draft';
        if (editorEyebrow) editorEyebrow.textContent = currentDraft ? 'Edit local draft' : 'New local draft';
        if (editorTitle) editorTitle.textContent = currentDraft?.title || 'Create Employee Resource';
        if (editorMeta) {
            editorMeta.textContent = currentDraft
                ? `${status} · Last updated ${formatDateTime(currentDraft.lastUpdatedUtc)}`
                : 'Frontend mock draft · saved only in this browser';
        }
        if (editorStatus) {
            editorStatus.hidden = false;
            editorStatus.textContent = currentDraft ? status : 'Draft';
            editorStatus.className = `resource-admin-status resource-admin-status--${statusClass(status)}`;
        }
    }

    function updateFormState() {
        const editable = !currentDraft || ['Draft', 'Rejected'].includes(currentDraft.status);
        Object.values(fields).forEach(field => {
            if (field) field.disabled = !editable;
        });
        if (saveButton) saveButton.disabled = !editable;
        if (deleteDraftButton) {
            deleteDraftButton.hidden = !currentDraft || !['Draft', 'Rejected'].includes(currentDraft.status);
            deleteDraftButton.disabled = !editable;
        }
        if (mockAttachmentFiles) mockAttachmentFiles.disabled = !editable;
        if (mockAttachmentDescription) mockAttachmentDescription.disabled = !editable;
        const addFilesButton = $('mockAddFilesButton');
        if (addFilesButton) addFilesButton.disabled = !editable;
    }

    function clearValidation() {
        Object.entries(errors).forEach(([name, element]) => {
            if (element) element.textContent = '';
            const field = fields[name];
            if (field) {
                field.removeAttribute('aria-invalid');
                field.classList.remove('has-error');
            }
        });
        if (errorSummary) errorSummary.hidden = true;
        if (errorList) errorList.replaceChildren();
    }

    function showValidation(validationErrors) {
        clearValidation();
        window.employeeResourcesAdminTabs?.select('details');
        const firstField = Object.keys(validationErrors)[0];
        Object.entries(validationErrors).forEach(([name, message]) => {
            if (errors[name]) errors[name].textContent = message;
            if (fields[name]) {
                fields[name].setAttribute('aria-invalid', 'true');
                fields[name].classList.add('has-error');
            }
            if (errorList) {
                const item = document.createElement('li');
                item.textContent = message;
                errorList.appendChild(item);
            }
        });
        if (errorSummary) {
            errorSummary.hidden = false;
            errorSummary.focus();
        }
        if (firstField && fields[firstField]) {
            window.setTimeout(() => fields[firstField].focus(), 0);
        }
    }

    function validateDraft(data) {
        const validationErrors = {};
        if (!data.title) validationErrors.title = 'Title is required.';
        if (data.title.length > 180) validationErrors.title = 'Title must not exceed 180 characters.';
        if (!/^[a-z0-9]+(?:-[a-z0-9]+)*$/.test(data.slug)) validationErrors.slug = 'Use lowercase letters, numbers, and hyphens in the slug.';
        if (!ALLOWED_TYPES.has(data.resourceType)) validationErrors.resourceType = 'Select a supported resource type.';
        if (!data.categoryId) validationErrors.categoryId = 'Select an IT category.';
        if (data.summary.length > 500) validationErrors.summary = 'Summary must not exceed 500 characters.';
        if (!data.overview) validationErrors.overview = 'Overview is required.';
        if (!data.version || data.version.length > 20 || !/^v\d+\.\d+$/i.test(data.version)) validationErrors.version = 'Use a version such as v1.0.';
        if (data.publishStartUtc && data.publishEndUtc && new Date(data.publishStartUtc) >= new Date(data.publishEndUtc)) {
            validationErrors.publishEndUtc = 'Publish end must be after publish start.';
        }
        if (data.highlights.some(value => value.length > 200)) validationErrors.highlightsText = 'Each highlight must not exceed 200 characters.';
        if (data.highlights.length > 20) validationErrors.highlightsText = 'Use no more than 20 highlights.';

        const duplicate = drafts.some(draft => draft.clientId !== data.clientId && normalize(draft.slug) === normalize(data.slug));
        if (duplicate) validationErrors.slug = 'That slug is already used by another local draft.';
        return validationErrors;
    }

    function updateCounts() {
        const total = demoResources.length + drafts.length;
        const draftCount = drafts.filter(item => item.status === 'Draft' || item.status === 'Rejected').length;
        const publishedCount = demoResources.filter(item => item.status === 'Published').length + drafts.filter(item => item.status === 'Published').length;
        const archivedCount = drafts.filter(item => item.status === 'Archived').length;
        const values = {
            resourceSummaryTotal: total,
            resourceSummaryDrafts: draftCount,
            resourceSummaryPublished: publishedCount,
            resourceSummaryArchived: archivedCount,
            resourceSummaryDemo: demoResources.length
        };
        Object.entries(values).forEach(([id, value]) => {
            const element = $(id);
            if (element) element.textContent = String(value);
        });
        if (libraryCount) libraryCount.textContent = String(total);
    }

    function buildDraftRow(draft) {
        const button = document.createElement('button');
        button.type = 'button';
        button.className = 'resource-admin-library__item resource-admin-library__item--draft';
        button.dataset.selectDraft = draft.clientId;
        button.dataset.localDraft = 'true';
        button.setAttribute('aria-label', `Edit local draft ${draft.title || 'Untitled resource'}`);
        if (currentDraft?.clientId === draft.clientId) button.classList.add('is-selected');

        const badge = document.createElement('span');
        badge.className = 'dummy-data-badge dummy-data-badge--compact resource-admin-local-badge';
        badge.textContent = 'LOCAL DRAFT';
        const title = document.createElement('strong');
        title.textContent = draft.title || 'Untitled resource';
        const detail = document.createElement('span');
        detail.textContent = `${draft.category || 'Uncategorized'} · ${draft.resourceType || 'Guide'}`;
        const status = document.createElement('small');
        status.className = `resource-admin-status resource-admin-status--${statusClass(draft.status)}`;
        status.textContent = draft.status;

        button.append(badge, title, detail, status);
        return button;
    }

    function updateLibrary() {
        library.querySelectorAll('[data-local-draft]').forEach(element => element.remove());
        drafts
            .slice()
            .sort((left, right) => String(right.lastUpdatedUtc || '').localeCompare(String(left.lastUpdatedUtc || '')))
            .forEach(draft => library.prepend(buildDraftRow(draft)));
        library.querySelectorAll('[data-select-draft]').forEach(element => {
            element.classList.toggle('is-selected', element.dataset.selectDraft === currentDraft?.clientId);
        });
        updateCounts();
    }

    function setHash(clientId) {
        const nextHash = clientId ? `#draft=${encodeURIComponent(clientId)}` : '';
        if (window.location.hash === nextHash) return;
        window.history.replaceState({}, '', `${window.location.pathname}${window.location.search}${nextHash}`);
    }

    function readHashDraft() {
        const match = window.location.hash.match(/^#draft=([^&]+)/);
        return match ? decodeURIComponent(match[1]) : null;
    }

    function confirmDiscard() {
        return !dirty || window.confirm('You have unsaved changes. Leave this draft?');
    }

    function openNewDraft(focus = true) {
        if (!confirmDiscard()) return;
        currentDraft = null;
        dirty = false;
        generatedSlug = '';
        slugWasEdited = false;
        setHash(null);
        populateForm(null);
        updateLibrary();
        if (focus) window.setTimeout(() => fields.title?.focus(), 0);
    }

    function selectDraft(clientId, focus = true) {
        if (!confirmDiscard()) return;
        const draft = drafts.find(item => item.clientId === clientId);
        if (!draft) return;
        currentDraft = draft;
        dirty = false;
        setHash(clientId);
        populateForm(currentDraft);
        updateLibrary();
        if (focus) window.setTimeout(() => fields.title?.focus(), 0);
    }

    function templateDraftFrom(resource) {
        const categoryId = optionValueForLabel(fields.categoryId, resource.category, 'mock-category');
        const ownerDepartmentId = optionValueForLabel(fields.ownerDepartmentId, resource.ownerDepartment, 'mock-owner');
        const clientId = makeId('draft');
        const baseSlug = `${resource.slug}-draft`;
        let slug = baseSlug;
        let suffix = 2;
        while (drafts.some(item => normalize(item.slug) === normalize(slug))) {
            slug = `${baseSlug}-${suffix}`;
            suffix += 1;
        }
        return {
            ...createEmptyDraft(),
            clientId,
            sourceTemplateSlug: resource.slug,
            title: resource.title,
            slug,
            summary: resource.summary,
            overview: resource.overview,
            resourceType: resource.resourceType === 'Forms' ? 'Form' : resource.resourceType,
            categoryId,
            category: resource.category,
            categorySlug: resource.categorySlug || slugify(resource.category),
            ownerDepartmentId,
            ownerDepartment: resource.ownerDepartment,
            version: resource.version || 'v1.0',
            highlights: Array.isArray(resource.highlights) ? resource.highlights.slice(0, 20) : [],
            attachments: (resource.attachments || []).map(attachment => ({
                ...attachment,
                clientId: makeId('reference'),
                source: 'demo',
                isDemoOnly: true,
                referenceOnly: true,
                needsFile: false,
                uploadReady: false
            })),
            createdAtUtc: nowIso(),
            lastUpdatedUtc: nowIso(),
            status: 'Draft'
        };
    }

    function useTemplate(slug) {
        if (!confirmDiscard()) return;
        const resource = demoResources.find(item => item.slug === slug);
        if (!resource) return;
        const draft = templateDraftFrom(resource);
        const nextDrafts = [draft, ...drafts];
        if (!persistDrafts(nextDrafts)) return;
        currentDraft = draft;
        dirty = false;
        setHash(draft.clientId);
        populateForm(draft);
        updateLibrary();
        showToast(`Created a local draft from “${resource.title}”.`, 'success');
        window.setTimeout(() => fields.title?.focus(), 0);
    }

    function saveDraft() {
        const base = currentDraft || createEmptyDraft();
        const candidate = readForm(base);
        candidate.clientId = base.clientId || makeId('draft');
        candidate.kind = 'local';
        candidate.isDemoData = false;
        candidate.resourceId = null;
        candidate.rowVersion = '';
        candidate.status = 'Draft';
        candidate.createdAtUtc = base.createdAtUtc || nowIso();
        candidate.lastUpdatedUtc = nowIso();
        candidate.authorName = base.authorName || 'Frontend mock user';
        candidate.approverName = null;
        candidate.revisions = Array.isArray(base.revisions) ? base.revisions : [];

        const validationErrors = validateDraft(candidate);
        if (Object.keys(validationErrors).length > 0) {
            showValidation(validationErrors);
            return;
        }

        const existingIndex = drafts.findIndex(item => item.clientId === candidate.clientId);
        const nextDrafts = existingIndex >= 0
            ? drafts.map(item => item.clientId === candidate.clientId ? candidate : item)
            : [candidate, ...drafts];
        if (!persistDrafts(nextDrafts)) return;

        currentDraft = candidate;
        dirty = false;
        setHash(candidate.clientId);
        populateForm(candidate);
        updateLibrary();
        showToast('Local Employee Resource draft saved.', 'success');
        announce(`Draft ${candidate.title || 'Employee Resource'} saved locally.`);
    }

    function deleteCurrentDraft() {
        if (!currentDraft || !window.confirm('Delete this local draft and its local attachment metadata?')) return;
        currentDraft.attachments.forEach(revokeAttachmentResources);
        const nextDrafts = drafts.filter(item => item.clientId !== currentDraft.clientId);
        if (!persistDrafts(nextDrafts)) return;
        currentDraft = null;
        dirty = false;
        setHash(null);
        openNewDraft(false);
        showToast('Local draft deleted.', 'success');
    }

    function getAttachmentIssues() {
        if (!currentDraft) return [];
        return currentDraft.attachments.filter(attachment => attachment.referenceOnly || attachment.needsFile);
    }

    function updateAttachmentStatus() {
        if (!mockAttachmentStatus) return;
        if (!currentDraft) {
            mockAttachmentStatus.textContent = 'Save a valid draft before selecting local files.';
            return;
        }
        const issues = getAttachmentIssues();
        if (issues.length > 0) {
            mockAttachmentStatus.textContent = `${issues.length} attachment${issues.length === 1 ? '' : 's'} still need a real local file or removal before publishing.`;
        } else if (currentDraft.attachments.length > 0) {
            mockAttachmentStatus.textContent = `${currentDraft.attachments.length} local attachment${currentDraft.attachments.length === 1 ? '' : 's'} ready for the frontend preview.`;
        } else {
            mockAttachmentStatus.textContent = 'No attachments added yet.';
        }
    }

    function getObjectUrl(attachmentId, file) {
        if (!objectUrls.has(attachmentId)) objectUrls.set(attachmentId, URL.createObjectURL(file));
        return objectUrls.get(attachmentId);
    }

    function revokeAttachmentResources(attachment) {
        pendingFiles.delete(attachment.clientId);
        const url = objectUrls.get(attachment.clientId);
        if (url) URL.revokeObjectURL(url);
        objectUrls.delete(attachment.clientId);
    }

    function persistCurrentAttachments() {
        if (!currentDraft) return;
        persistDrafts(drafts.map(item => item.clientId === currentDraft.clientId ? currentDraft : item));
    }

    function addFiles(fileList, replaceId = null) {
        if (!currentDraft) {
            showToast('Save the draft before selecting attachments.', 'warning');
            return;
        }
        const files = Array.from(fileList || []);
        if (files.length === 0) return;
        if (replaceId && files.length > 1) files.splice(1);

        const added = [];
        for (const file of files) {
            const extension = getExtension(file.name);
            if (!ALLOWED_EXTENSIONS.has(extension)) {
                showToast(`${file.name} is not an allowed attachment type.`, 'error');
                continue;
            }
            if (file.size > MAX_FILE_BYTES) {
                showToast(`${file.name} exceeds the 100 MB attachment limit.`, 'error');
                continue;
            }

            const clientId = replaceId || makeId('attachment');
            const attachment = {
                clientId,
                fileName: file.name,
                format: extension.replace('.', '').toUpperCase(),
                sizeLabel: formatBytes(file.size),
                sizeBytes: file.size,
                description: mockAttachmentDescription?.value.trim() || '',
                source: 'local',
                isDemoOnly: false,
                referenceOnly: false,
                needsFile: false,
                uploadReady: true
            };
            if (replaceId) {
                const old = currentDraft.attachments.find(item => item.clientId === replaceId);
                if (old) revokeAttachmentResources(old);
                currentDraft.attachments = currentDraft.attachments.map(item => item.clientId === replaceId ? attachment : item);
            } else {
                currentDraft.attachments = [...currentDraft.attachments, attachment];
            }
            pendingFiles.set(clientId, file);
            added.push(attachment);
            replaceId = null;
        }

        if (mockAttachmentFiles) mockAttachmentFiles.value = '';
        if (mockAttachmentDescription) mockAttachmentDescription.value = '';
        if (added.length > 0) {
            dirty = true;
            persistCurrentAttachments();
            renderMockSections();
            showToast(`${added.length} local file${added.length === 1 ? '' : 's'} added to the draft.`, 'success');
        }
    }

    function renderAttachmentList() {
        if (!mockAttachmentList) return;
        mockAttachmentList.replaceChildren();
        if (!currentDraft || currentDraft.attachments.length === 0) {
            const empty = document.createElement('p');
            empty.className = 'resource-admin-muted';
            empty.textContent = 'No local or reference attachments yet.';
            mockAttachmentList.appendChild(empty);
            updateAttachmentStatus();
            return;
        }

        const editable = ['Draft', 'Rejected'].includes(currentDraft.status);
        currentDraft.attachments.forEach(attachment => {
            const row = document.createElement('article');
            row.className = `resource-admin-attachment ${attachment.referenceOnly ? 'resource-admin-attachment--reference' : ''} ${attachment.needsFile ? 'resource-admin-attachment--needs-file' : ''}`;

            const body = document.createElement('div');
            body.className = 'resource-admin-attachment__body';
            const badge = document.createElement('span');
            badge.className = `dummy-data-badge dummy-data-badge--compact ${attachment.referenceOnly ? '' : 'resource-admin-local-badge'}`;
            badge.textContent = attachment.referenceOnly ? 'REFERENCE ONLY' : 'LOCAL FILE';
            const name = document.createElement('strong');
            name.textContent = attachment.fileName;
            const details = document.createElement('span');
            details.textContent = `${attachment.format} · ${attachment.sizeLabel}${attachment.needsFile ? ' · file needs re-selection' : ''}`;
            body.append(badge, name, details);
            if (attachment.description) {
                const description = document.createElement('small');
                description.textContent = attachment.description;
                body.appendChild(description);
            }

            const file = pendingFiles.get(attachment.clientId);
            if (file && IMAGE_EXTENSIONS.has(getExtension(file.name))) {
                const image = document.createElement('img');
                image.className = 'resource-admin-attachment__preview';
                image.src = getObjectUrl(attachment.clientId, file);
                image.alt = `Preview of ${attachment.fileName}`;
                body.appendChild(image);
            }

            const actions = document.createElement('div');
            actions.className = 'resource-admin-attachment__actions';
            if (file) {
                const preview = document.createElement('button');
                preview.type = 'button';
                preview.dataset.attachmentAction = 'preview';
                preview.dataset.attachmentId = attachment.clientId;
                preview.textContent = 'Preview';
                actions.appendChild(preview);
            }
            if (editable && (attachment.referenceOnly || attachment.needsFile)) {
                const replace = document.createElement('button');
                replace.type = 'button';
                replace.dataset.attachmentAction = 'replace';
                replace.dataset.attachmentId = attachment.clientId;
                replace.textContent = 'Replace';
                actions.appendChild(replace);
            }
            if (editable) {
                const remove = document.createElement('button');
                remove.type = 'button';
                remove.dataset.attachmentAction = 'remove';
                remove.dataset.attachmentId = attachment.clientId;
                remove.textContent = 'Remove';
                actions.appendChild(remove);
            }
            row.append(body, actions);
            mockAttachmentList.appendChild(row);
        });
        updateAttachmentStatus();
    }

    function renderWorkflow() {
        if (!mockWorkflowActions || !mockWorkflowSection) return;
        mockWorkflowSection.hidden = !currentDraft;
        if (!currentDraft) return;
        const status = currentDraft.status;
        const issues = getAttachmentIssues();
        const publish = mockWorkflowActions.querySelector('[data-mock-action="publish"]');
        const archive = mockWorkflowActions.querySelector('[data-mock-action="archive"]');
        const restore = mockWorkflowActions.querySelector('[data-mock-action="restore"]');
        if (publish) publish.hidden = !canPublish || !['Draft', 'Rejected'].includes(status);
        if (archive) archive.hidden = !canPublish || status !== 'Published';
        if (restore) restore.hidden = !canPublish || status !== 'Archived';
        if (mockWorkflowHint) {
            mockWorkflowHint.textContent = issues.length > 0
                ? 'Replace or remove reference-only attachments before simulating publication.'
                : `Frontend mock lifecycle: ${status}. Backend publishing is not called in this phase.`;
        }
    }

    function renderMockPreview() {
        if (!mockPreviewBody) return;
        mockPreviewBody.replaceChildren();
        if (!currentDraft) return;
        const title = document.createElement('h3');
        title.textContent = currentDraft.title || 'Untitled Employee Resource';
        const summary = document.createElement('p');
        summary.textContent = currentDraft.summary || 'No summary entered.';
        const overview = document.createElement('p');
        overview.textContent = currentDraft.overview || 'No overview entered.';
        const metadata = document.createElement('p');
        metadata.className = 'resource-admin-mock-preview__meta';
        metadata.textContent = `${currentDraft.category || 'Uncategorized'} · ${currentDraft.resourceType || 'Guide'} · ${currentDraft.version || 'v1.0'}`;
        mockPreviewBody.append(title, metadata, summary, overview);

        if (currentDraft.highlights.length > 0) {
            const heading = document.createElement('strong');
            heading.textContent = 'Highlights';
            const list = document.createElement('ul');
            currentDraft.highlights.forEach(highlight => {
                const item = document.createElement('li');
                item.textContent = highlight;
                list.appendChild(item);
            });
            mockPreviewBody.append(heading, list);
        }
    }

    function renderMockSections() {
        const hasDraft = Boolean(currentDraft);
        root.dataset.mockDraft = hasDraft ? 'true' : 'false';
        if (mockAttachmentSection) mockAttachmentSection.hidden = !hasDraft;
        if (mockPreviewSection && !hasDraft) mockPreviewSection.hidden = true;
        updateFormState();
        renderWorkflow();
        renderAttachmentList();
        renderMockPreview();
        window.employeeResourcesAdminTabs?.sync();
    }

    function applyStatus(action) {
        if (!currentDraft) return;
        if (dirty) {
            showToast('Save the draft before changing its mock lifecycle status.', 'warning');
            return;
        }
        if (action === 'publish') {
            if (!canPublish) {
                showToast('Publisher permission is required for this mock action.', 'error');
                return;
            }
            const issues = getAttachmentIssues();
            if (issues.length > 0) {
                showToast('Replace or remove reference-only attachments before publishing.', 'warning');
                return;
            }
            currentDraft.status = 'Published';
            currentDraft.publishStartUtc = currentDraft.publishStartUtc || nowIso();
            currentDraft.approverName = 'Frontend mock publisher';
        } else if (action === 'archive') {
            if (!canPublish || currentDraft.status !== 'Published') return;
            currentDraft.status = 'Archived';
        } else if (action === 'restore') {
            if (!canPublish || currentDraft.status !== 'Archived') return;
            currentDraft.status = 'Draft';
            currentDraft.approverName = null;
        } else {
            return;
        }
        currentDraft.lastUpdatedUtc = nowIso();
        dirty = false;
        persistCurrentAttachments();
        populateForm(currentDraft);
        updateLibrary();
        showToast(`Mock resource status changed to ${currentDraft.status}.`, 'success');
    }

    function togglePreview() {
        if (!mockPreviewSection) return;
        const willShow = mockPreviewSection.hidden;
        mockPreviewSection.hidden = !willShow;
        if (mockPreviewButton) mockPreviewButton.textContent = willShow ? 'Hide preview' : 'Preview draft';
        if (willShow) renderMockPreview();
    }

    function handleAttachmentAction(event) {
        const actionButton = event.target.closest('[data-attachment-action]');
        if (!actionButton || !currentDraft) return;
        const action = actionButton.dataset.attachmentAction;
        const attachmentId = actionButton.dataset.attachmentId;
        const attachment = currentDraft.attachments.find(item => item.clientId === attachmentId);
        if (!attachment) return;

        if (action === 'preview') {
            const file = pendingFiles.get(attachmentId);
            if (!file) {
                showToast('Select the local file again before previewing it.', 'warning');
                return;
            }
            window.open(getObjectUrl(attachmentId, file), '_blank', 'noopener,noreferrer');
            return;
        }
        if (action === 'replace') {
            replacementTargetId = attachmentId;
            mockAttachmentReplaceInput?.click();
            return;
        }
        if (action === 'remove') {
            if (!window.confirm(`Remove ${attachment.fileName} from this local draft?`)) return;
            revokeAttachmentResources(attachment);
            currentDraft.attachments = currentDraft.attachments.filter(item => item.clientId !== attachmentId);
            dirty = true;
            persistCurrentAttachments();
            renderMockSections();
            showToast('Attachment removed from the local draft.', 'success');
        }
    }

    function resetMockData() {
        if (!window.confirm('Reset all frontend mock drafts and attachment metadata?')) return;
        drafts.forEach(draft => draft.attachments.forEach(revokeAttachmentResources));
        pendingFiles.clear();
        objectUrls.forEach(url => URL.revokeObjectURL(url));
        objectUrls.clear();
        try {
            window.localStorage.removeItem(STORAGE_KEY);
        } catch {
            // The in-memory reset still applies if storage is unavailable.
        }
        drafts = [];
        currentDraft = null;
        dirty = false;
        openNewDraft(false);
        updateLibrary();
        showToast('Frontend mock drafts were reset.', 'success');
    }

    function wireFieldEvents() {
        Object.entries(fields).forEach(([name, field]) => {
            if (!field) return;
            const markDirty = () => {
                dirty = true;
                const error = errors[name];
                if (error) error.textContent = '';
                field.removeAttribute('aria-invalid');
                field.classList.remove('has-error');
            };
            field.addEventListener('input', markDirty);
            field.addEventListener('change', markDirty);
        });
        fields.title?.addEventListener('input', () => {
            if (slugWasEdited) return;
            const nextSlug = slugify(fields.title.value);
            fields.slug.value = nextSlug;
            generatedSlug = nextSlug;
        });
        fields.slug?.addEventListener('input', () => {
            slugWasEdited = fields.slug.value.trim() !== generatedSlug;
        });
    }

    function wireEvents() {
        wireFieldEvents();
        form.addEventListener('submit', event => {
            event.preventDefault();
            saveDraft();
        });
        newResourceLink?.addEventListener('click', event => {
            event.preventDefault();
            openNewDraft();
        });
        resetButton?.addEventListener('click', resetMockData);
        deleteDraftButton?.addEventListener('click', deleteCurrentDraft);
        library.addEventListener('click', event => {
            const templateButton = event.target.closest('[data-use-template]');
            if (templateButton) {
                useTemplate(templateButton.dataset.useTemplate);
                return;
            }
            const draftButton = event.target.closest('[data-select-draft]');
            if (draftButton) selectDraft(draftButton.dataset.selectDraft);
        });
        mockAttachmentForm?.addEventListener('submit', event => {
            event.preventDefault();
            addFiles(mockAttachmentFiles?.files);
        });
        mockAttachmentReplaceInput?.addEventListener('change', () => {
            if (replacementTargetId) addFiles(mockAttachmentReplaceInput.files, replacementTargetId);
            replacementTargetId = null;
            mockAttachmentReplaceInput.value = '';
        });
        mockAttachmentList?.addEventListener('click', handleAttachmentAction);
        mockWorkflowActions?.addEventListener('click', event => {
            const button = event.target.closest('[data-mock-action]');
            if (button) applyStatus(button.dataset.mockAction);
        });
        mockPreviewButton?.addEventListener('click', togglePreview);
        window.addEventListener('hashchange', () => {
            const clientId = readHashDraft();
            if (clientId && drafts.some(item => item.clientId === clientId)) selectDraft(clientId, false);
            else if (!clientId && currentDraft) openNewDraft(false);
        });
        window.addEventListener('beforeunload', event => {
            if (!dirty) return;
            event.preventDefault();
            event.returnValue = '';
        });
        window.addEventListener('pagehide', () => objectUrls.forEach(url => URL.revokeObjectURL(url)));
    }

    demoResources = parseSeed().map(normalizeDemoResource);
    wireEvents();
    const initialDraftId = readHashDraft();
    if (initialDraftId && drafts.some(item => item.clientId === initialDraftId)) selectDraft(initialDraftId, false);
    else openNewDraft(false);
    updateLibrary();
})();
