document.addEventListener('DOMContentLoaded', function() {
    const page = document.getElementById('servicesPage');
    if (!page) return;
    let savedCards = JSON.parse(document.getElementById('serviceCardsData').textContent);
    let workingCards = savedCards.map(card => ({ ...card }));
    const dirtyCards = new Set();
    let selectedId = savedCards[0]?.id || '';
    let newCardCounter = 1;
    let directoryFilter = 'all';
    let busy = false;

    const list = document.getElementById('adminCardList');
    const cardSearch = document.getElementById('cardSearch');
    const cardFilter = document.getElementById('cardFilter');
    const visibleCount = document.getElementById('visibleCount');
    const hiddenCount = document.getElementById('hiddenCount');
    const draftCount = document.getElementById('draftCount');
    const newCardBtn = document.getElementById('newCardBtn');
    const resetDraftsBtn = document.getElementById('resetDraftsBtn');
    const resetCardBtn = document.getElementById('resetCardBtn');
    const saveCardBtn = document.getElementById('saveCardBtn');
    const archiveCardBtn = document.getElementById('archiveCardBtn');
    const saveStatus = document.getElementById('saveStatus');
    const editorSourceLabel = document.getElementById('editorSourceLabel');
    const previewCard = document.getElementById('adminPreviewCard');
    const previewIcon = document.getElementById('adminPreviewIcon');
    const previewLaunch = document.getElementById('adminPreviewLaunch');
    const selectedCardKey = document.getElementById('selectedCardKey');
    const selectedCardName = document.getElementById('selectedCardName');
    const selectedLaunchChip = document.getElementById('selectedLaunchChip');
    const selectedVisibilityChip = document.getElementById('selectedVisibilityChip');
    const selectedStatusChip = document.getElementById('selectedStatusChip');
    const selectedInstallerChip = document.getElementById('selectedInstallerChip');
    const healthStatusText = document.getElementById('cardHealthStatusText');
    const healthCheckedText = document.getElementById('cardHealthCheckedText');
    const antiForgeryToken = document.querySelector('input[name="__RequestVerificationToken"]')?.value || '';
    const installerFileInput = document.getElementById('cardInstallerFile');
    const uploadInstallerBtn = document.getElementById('uploadInstallerBtn');
    const installerUploadStatus = document.getElementById('installerUploadStatus');
    const installerSelectedFile = document.getElementById('installerSelectedFile');
    const editForm = document.getElementById('cardEditForm');
    const editorFields = document.getElementById('serviceEditorFields');
    const editor = document.getElementById('adminEditor');
    const filterButtons = [...page.querySelectorAll('[data-service-filter]')];
    const clearFiltersBtn = document.getElementById('clearServiceFilters');
    const resultCount = document.getElementById('serviceResultCount');
    const advancedSettings = document.getElementById('serviceAdvancedSettings');
    const query = new URLSearchParams(window.location.search);
    const requestedFilter = query.get('filter');
    const requestedService = query.get('service');

    if (filterButtons.some(button => button.dataset.serviceFilter === requestedFilter)) {
        directoryFilter = requestedFilter;
    } else if (requestedFilter && Array.from(cardFilter.options).some(option => option.value === requestedFilter)) {
        cardFilter.value = requestedFilter;
    }
    if (requestedService) {
        const requestedCard = workingCards.find(card => String(card.cardKey || '').toLowerCase() === requestedService.toLowerCase());
        if (requestedCard) selectedId = requestedCard.id;
    }

    const fields = {
        cardKey: document.getElementById('cardKey'),
        name: document.getElementById('cardName'),
        description: document.getElementById('cardDescription'),
        url: document.getElementById('cardUrl'),
        badge: document.getElementById('cardBadge'),
        status: document.getElementById('cardStatus'),
        launchType: document.getElementById('cardLaunchType'),
        sortOrder: document.getElementById('cardSortOrder'),
        theme: document.getElementById('cardTheme'),
        icon: document.getElementById('cardIcon'),
        isVisible: document.getElementById('cardIsVisible'),
        allowedRoles: document.getElementById('cardAllowedRoles'),
        allowedUserIds: document.getElementById('cardAllowedUserIds'),
        healthCheckUrl: document.getElementById('cardHealthCheckUrl'),
        deepLink: document.getElementById('cardDeepLink'),
        installerPath: document.getElementById('cardInstallerPath'),
        installerVersion: document.getElementById('cardInstallerVersion'),
        installerNotes: document.getElementById('cardInstallerNotes'),
        maintenanceStartUtc: document.getElementById('cardMaintenanceStartUtc'),
        maintenanceEndUtc: document.getElementById('cardMaintenanceEndUtc'),
        maintenanceNote: document.getElementById('cardMaintenanceNote')
    };

    function getSavedCard(id) {
        return savedCards.find(card => card.id === id);
    }

    function getCard(id) {
        return workingCards.find(card => card.id === id);
    }

    function isNewCard(card) {
        return !!card && !card.rowVersion;
    }

    function statusMeta(status) {
        if (status === 'Connected') return { label: 'Connected', className: 'is-operational' };
        if (status === 'Maintenance') return { label: 'Maintenance', className: 'is-maintenance' };
        if (status === 'Disabled') return { label: 'Disabled', className: 'is-disabled' };
        return { label: 'Not connected', className: 'is-offline' };
    }

    function launchTypeOf(card) {
        if (card.status === 'Disabled') return 'disabled';
        if (card.isRdpDownload && card.isInstallerDownload) return 'rdpInstaller';
        if (card.isRdpDownload) return 'rdp';
        if (card.isInstallerDownload) return 'installer';
        return 'url';
    }


    function installerAssociation(card) {
        const path = String(card?.installerPath || '').trim();
        const fileName = path ? path.split(/[\\/]/).pop() : '';
        const launchType = launchTypeOf(card || {});
        const installerExpected = launchType === 'installer' || launchType === 'rdpInstaller';
        if (path) {
            return {
                label: installerExpected ? 'Installer linked' : 'Installer associated',
                detail: fileName,
                className: 'installer-association'
            };
        }
        return {
            label: installerExpected ? 'Installer missing' : 'No installer linked',
            detail: '',
            className: installerExpected ? 'installer-association installer-association--missing' : 'installer-association installer-association--none'
        };
    }

    function truncateMiddle(value, maxLength) {
        const text = String(value || '');
        const limit = Number(maxLength || 0);
        if (!limit || text.length <= limit) return text;
        if (limit <= 6) return text.slice(0, limit);
        const keep = limit - 3;
        const start = Math.ceil(keep * 0.55);
        const end = Math.floor(keep * 0.45);
        return `${text.slice(0, start)}...${text.slice(text.length - end)}`;
    }

    function installerAssociationText(card, maxDetailLength) {
        const association = installerAssociation(card);
        const detail = maxDetailLength ? truncateMiddle(association.detail, maxDetailLength) : association.detail;
        return detail ? `${association.label}: ${detail}` : association.label;
    }
    function flagsFromLaunchType(value) {
        return {
            isRdpDownload: value === 'rdp' || value === 'rdpInstaller',
            isInstallerDownload: value === 'installer' || value === 'rdpInstaller',
            status: value === 'disabled' ? 'Disabled' : fields.status.value
        };
    }

    function getFilteredCards() {
        const query = (cardSearch.value || '').toLowerCase().trim();
        const filter = cardFilter.value || 'all';
        return workingCards.filter(card => {
            const haystack = [card.name, card.cardKey, card.description, card.badge, card.status, launchLabel(launchTypeOf(card))]
                .join(' ')
                .toLowerCase();
            if (query && !haystack.includes(query)) return false;
            if (directoryFilter === 'visible' && card.isVisible === false) return false;
            if (directoryFilter === 'hidden' && card.isVisible !== false) return false;
            if (directoryFilter === 'draft' && !dirtyCards.has(card.id)) return false;
            if (filter === 'connected') return card.status === 'Connected';
            if (filter === 'maintenance') return card.status === 'Maintenance';
            if (filter === 'not-connected') return card.status === 'NotConnected';
            if (filter === 'disabled') return card.status === 'Disabled';
            return true;
        }).sort((a, b) => Number(a.sortOrder || 0) - Number(b.sortOrder || 0)
            || String(a.name || '').localeCompare(String(b.name || '')));
    }

    function renderList() {
        if (directoryFilter === 'draft' && dirtyCards.size === 0) directoryFilter = 'all';
        const filteredCards = getFilteredCards();
        const scrollTop = list.scrollTop;
        resultCount.textContent = `${filteredCards.length} of ${workingCards.length} service${workingCards.length === 1 ? '' : 's'}`;
        clearFiltersBtn.hidden = !cardSearch.value && cardFilter.value === 'all' && directoryFilter === 'all';

        if (filteredCards.length === 0) {
            list.innerHTML = workingCards.length
                ? '<div class="services-list-empty"><strong>No matching services</strong><span>Try another search or clear the filters above.</span></div>'
                : '<div class="services-list-empty"><strong>No services yet</strong><span>Add your first service to build the directory.</span></div>';
            renderCounts();
            return;
        }

        list.innerHTML = filteredCards.map(card => {
            const meta = statusMeta(card.status);
            const hasDraft = dirtyCards.has(card.id);
            const launchType = launchTypeOf(card);
            const visibilityText = card.isVisible === false ? 'Hidden' : 'Visible';
            return `
                <button type="button" class="services-row ${card.id === selectedId ? 'is-selected' : ''}" data-card-id="${escapeHtml(card.id)}" aria-current="${card.id === selectedId ? 'true' : 'false'}" ${busy ? 'disabled' : ''}>
                    <span class="services-row__icon" aria-hidden="true">${serviceIcon(card.icon)}</span>
                    <span class="services-row__main">
                        <span class="services-row__title">${escapeHtml(card.name || 'Untitled service')}${hasDraft ? '<span class="services-dirty-dot" aria-label="Unsaved changes"></span>' : ''}</span>
                        <span class="services-row__meta">${escapeHtml(launchLabel(launchType))}<span aria-hidden="true">·</span><span class="${card.isVisible === false ? 'services-hidden-label' : ''}">${visibilityText}</span></span>
                        <span class="services-status ${meta.className}"><span aria-hidden="true"></span>${meta.label}</span>
                    </span>
                    <span class="services-row__arrow" aria-hidden="true">›</span>
                </button>
            `;
        }).join('');

        list.scrollTop = scrollTop;
        list.querySelectorAll('.services-row').forEach(row => {
            row.addEventListener('click', function() {
                if (busy) return;
                const nextId = this.dataset.cardId;
                selectedId = nextId;
                loadEditor();
                activateTab('content');
                renderList();
                revealEditor();
            });
        });

        renderCounts();
    }

    function renderCounts() {
        visibleCount.textContent = workingCards.filter(card => card.isVisible !== false).length.toString();
        hiddenCount.textContent = workingCards.filter(card => card.isVisible === false).length.toString();
        draftCount.textContent = dirtyCards.size.toString();
        document.getElementById('totalCount').textContent = workingCards.length.toString();
        filterButtons.forEach(button => {
            const active = directoryFilter === button.dataset.serviceFilter;
            button.classList.toggle('is-active', active);
            button.setAttribute('aria-pressed', String(active));
            if (button.dataset.serviceFilter === 'draft') button.hidden = dirtyCards.size === 0;
            button.disabled = busy;
        });
        resetDraftsBtn.hidden = dirtyCards.size === 0;
        resetDraftsBtn.disabled = busy;
    }

    function loadEditor() {
        const card = getCard(selectedId);
        editForm.hidden = !card;
        document.getElementById('serviceEditorEmpty').hidden = !!card;
        if (!card) {
            selectedCardKey.textContent = 'Service settings';
            selectedCardName.textContent = 'Select a service';
            editorSourceLabel.textContent = 'Choose a service or add a new one to get started.';
            saveCardBtn.disabled = true;
            archiveCardBtn.disabled = true;
            return;
        }

        fields.cardKey.value = card.cardKey || '';
        fields.cardKey.readOnly = !isNewCard(card);
        fields.name.value = card.name || '';
        fields.description.value = card.description || '';
        fields.url.value = card.url || '';
        fields.badge.value = card.badge || '';
        fields.status.value = card.status || 'NotConnected';
        fields.launchType.value = launchTypeOf(card);
        fields.sortOrder.value = Number.isFinite(card.sortOrder) ? card.sortOrder : 0;
        fields.theme.value = card.theme || '';
        fields.icon.value = card.icon || 'package';
        fields.isVisible.checked = card.isVisible !== false;
        fields.allowedRoles.value = card.allowedRoles || '';
        fields.allowedUserIds.value = card.allowedUserIds || '';
        fields.healthCheckUrl.value = card.healthCheckUrl || '';
        fields.deepLink.value = card.deepLink || '';
        fields.installerPath.value = card.installerPath || '';
        fields.installerVersion.value = card.installerVersion || '';
        fields.installerNotes.value = card.installerNotes || '';
        if (installerFileInput) installerFileInput.value = '';
        if (installerSelectedFile) installerSelectedFile.textContent = card.installerPath ? `Current saved path: ${card.installerPath}` : 'No upload file selected.';
        if (installerUploadStatus) installerUploadStatus.textContent = card.installerPath ? `Current: ${card.installerPath}` : 'No installer uploaded in this session.';
        fields.maintenanceStartUtc.value = toDateTimeLocal(card.maintenanceStartUtc);
        fields.maintenanceEndUtc.value = toDateTimeLocal(card.maintenanceEndUtc);
        fields.maintenanceNote.value = card.maintenanceNote || '';
        advancedSettings.open = isNewCard(card);
        document.getElementById('serviceKeyHelp').textContent = isNewCard(card)
            ? 'Choose a unique key before saving. It stays fixed after the first save.'
            : 'This identifier stays fixed. You can change the service name above.';
        setEditorState();
        healthStatusText.textContent = card.lastHealthStatus || 'Not checked';
        healthCheckedText.textContent = card.lastHealthCheckedAt ? `Last checked: ${formatPht(card.lastHealthCheckedAt)}` : 'No recorded check';
        applyLaunchFieldVisibility();
        renderSelectedSummary(card);
        renderPreview(card);
    }

    function renderPreview(card) {
        const meta = statusMeta(card.status);
        previewCard.querySelector('.system-badge').textContent = card.badge || 'System';
        previewCard.querySelector('.card-title').textContent = card.name || 'Untitled card';
        previewCard.querySelector('.card-desc').textContent = card.description || '';
        previewCard.className = `admin-preview-card theme-${(card.theme || 'default').toLowerCase()}`;
        previewIcon.innerHTML = serviceIcon(card.icon);
        const dot = previewCard.querySelector('.status-dot');
        const text = previewCard.querySelector('.status-text');
        dot.className = `status-dot ${meta.className}`;
        text.className = `status-text ${meta.className}`;
        text.textContent = meta.label;
        previewLaunch.textContent = launchLabel(launchTypeOf(card));
        const note = previewCard.querySelector('.card-note');
        note.textContent = card.status === 'Maintenance'
            ? (card.maintenanceNote || 'Maintenance details are not set.')
            : card.isInstallerDownload ? (card.installerPath
                ? (card.installerVersion ? `Version ${card.installerVersion}` : 'Installer package available')
                : 'Installer package not available') : '';
        note.hidden = !note.textContent;
    }

    function renderSelectedSummary(card) {
        const meta = statusMeta(card.status);
        const launchType = launchTypeOf(card);
        selectedCardKey.textContent = card.cardKey || 'new-card';
        selectedCardName.textContent = card.name || 'Untitled card';
        selectedLaunchChip.textContent = launchLabel(launchType);
        selectedVisibilityChip.textContent = card.isVisible === false ? 'Hidden' : 'Visible';
        selectedStatusChip.textContent = meta.label;
        selectedStatusChip.className = `admin-mini-chip ${meta.className}`;
        const installer = installerAssociation(card);
        selectedInstallerChip.textContent = installerAssociationText(card, 42);
        selectedInstallerChip.title = installerAssociationText(card);
        selectedInstallerChip.className = installer.className.replace('installer-association', 'admin-mini-chip');
    }

    function updateDraftFromEditor() {
        const source = getSavedCard(selectedId);
        const card = getCard(selectedId);
        if (!card) return;
        const launchFlags = flagsFromLaunchType(fields.launchType.value);

        const next = {
            cardKey: fields.cardKey.value.trim(),
            name: fields.name.value.trim(),
            description: fields.description.value.trim(),
            url: fields.url.value.trim(),
            badge: fields.badge.value.trim(),
            status: launchFlags.status,
            isRdpDownload: launchFlags.isRdpDownload,
            isInstallerDownload: launchFlags.isInstallerDownload,
            isVisible: fields.isVisible.checked,
            sortOrder: parseInt(fields.sortOrder.value || '0', 10),
            allowedRoles: fields.allowedRoles.value.trim(),
            allowedUserIds: fields.allowedUserIds.value.trim(),
            healthCheckUrl: fields.healthCheckUrl.value.trim(),
            deepLink: fields.deepLink.value.trim(),
            installerPath: fields.installerPath.value.trim(),
            installerVersion: fields.installerVersion.value.trim(),
            installerNotes: fields.installerNotes.value.trim(),
            maintenanceStartUtc: fromDateTimeLocal(fields.maintenanceStartUtc.value, source?.maintenanceStartUtc),
            maintenanceEndUtc: fromDateTimeLocal(fields.maintenanceEndUtc.value, source?.maintenanceEndUtc),
            theme: fields.theme.value,
            icon: fields.icon.value,
            maintenanceNote: fields.maintenanceNote.value.trim()
        };

        Object.assign(card, next);
        const changed = !source || Object.keys(next).some(key => normalizeField(key, next[key]) !== normalizeField(key, source[key]));
        if (changed) {
            dirtyCards.add(selectedId);
        } else {
            dirtyCards.delete(selectedId);
        }

        renderPreview(getCard(selectedId));
        renderSelectedSummary(getCard(selectedId));
        renderList();
        setEditorState();
        applyLaunchFieldVisibility();
    }

    function setEditorState(message) {
        const card = getCard(selectedId);
        const dirty = dirtyCards.has(selectedId);
        editorSourceLabel.textContent = !card ? 'Choose a service or add a new one to get started.' : isNewCard(card) ? 'New service · not saved yet'
            : dirty ? 'Your changes have not been saved.' : 'Manage details, visibility, and launch options.';
        saveStatus.textContent = message || (busy ? saveStatus.textContent
            : dirty ? 'Unsaved changes to this service' : dirtyCards.size ? 'This service is saved' : 'All changes saved');
        saveStatus.classList.toggle('is-dirty', dirty);
        saveStatus.classList.toggle('is-error', !!message && !busy);
        saveCardBtn.disabled = busy || !card || !dirty;
        resetCardBtn.disabled = busy || !dirty;
        archiveCardBtn.disabled = busy || !card || isNewCard(card);
        editorFields.disabled = busy || !card;
        newCardBtn.disabled = busy;
        uploadInstallerBtn.disabled = busy || !card;
        cardSearch.disabled = busy;
        cardFilter.disabled = busy;
        clearFiltersBtn.disabled = busy;
        list.querySelectorAll('.services-row').forEach(button => { button.disabled = busy; });
    }

    function setBusy(value, message) {
        busy = value;
        editor.setAttribute('aria-busy', String(value));
        setEditorState(message);
        renderCounts();
    }

    function revealEditor() {
        if (window.matchMedia('(max-width: 1100px)').matches) {
            editor.scrollIntoView({ block: 'start', behavior: 'instant' });
            editor.focus({ preventScroll: true });
        } else {
            [...list.querySelectorAll('.services-row')].find(row => row.dataset.cardId === selectedId)?.focus({ preventScroll: true });
        }
    }

    function resetSelectedCard() {
        if (busy) return;
        const discardedId = selectedId;
        const saved = getSavedCard(selectedId);
        const card = getCard(selectedId);
        if (dirtyCards.has(selectedId) && !window.confirm(`Discard unsaved changes to "${card?.name || 'this service'}"?`)) {
            return;
        }
        const index = workingCards.findIndex(card => card.id === selectedId);
        if (saved && index >= 0) {
            workingCards[index] = { ...saved };
        } else if (index >= 0) {
            workingCards.splice(index, 1);
            selectedId = workingCards[Math.min(index, workingCards.length - 1)]?.id || '';
        }
        dirtyCards.delete(discardedId);
        loadEditor();
        renderList();
    }

    function resetAllDrafts() {
        if (busy) return;
        if (dirtyCards.size > 0 && !window.confirm(`Discard ${dirtyCards.size} unsaved service edit${dirtyCards.size === 1 ? '' : 's'}?`)) {
            return;
        }
        workingCards = savedCards.map(card => ({ ...card }));
        dirtyCards.clear();
        selectedId = workingCards.some(card => card.id === selectedId) ? selectedId : workingCards[0]?.id || '';
        loadEditor();
        renderList();
    }

    function createNewCard() {
        if (busy) return;
        const id = `new-${Date.now()}-${newCardCounter++}`;
        const sortOrder = workingCards.length === 0
            ? 10
            : Math.max(...workingCards.map(card => Number(card.sortOrder || 0))) + 10;
        const card = {
            id,
            cardKey: `new-card-${newCardCounter}`,
            index: workingCards.length,
            name: 'New Service Entry',
            description: '',
            url: '#',
            badge: 'System',
            theme: '',
            icon: 'grid',
            isRdpDownload: false,
            isInstallerDownload: false,
            isOperational: false,
            status: 'NotConnected',
            maintenanceNote: '',
            allowedRoles: '',
            allowedUserIds: '',
            healthCheckUrl: '',
            lastHealthStatus: '',
            lastHealthCheckedAt: null,
            maintenanceStartUtc: null,
            maintenanceEndUtc: null,
            deepLink: '',
            installerPath: '',
            installerVersion: '',
            installerNotes: '',
            isVisible: true,
            sortOrder,
            rowVersion: ''
        };

        workingCards.push(card);
        selectedId = id;
        dirtyCards.add(id);
        cardSearch.value = '';
        cardFilter.value = 'all';
        directoryFilter = 'all';
        renderList();
        loadEditor();
        activateTab('content');
        revealEditor();
        fields.cardKey.focus();
        fields.cardKey.select();
        window.yakultToast?.('New draft service created.', 'info');
    }

    async function saveSelectedCard() {
        const card = getCard(selectedId);
        if (busy || !card || !dirtyCards.has(selectedId)) return;
        if (!validateService()) return;

        let failureMessage = '';
        setBusy(true, 'Saving changes…');
        saveCardBtn.classList.add('is-loading');

        try {
            const response = await fetch(page.dataset.saveUrl, {
                method: 'POST',
                credentials: 'same-origin',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': antiForgeryToken
                },
                body: JSON.stringify({
                    cardKey: card.cardKey,
                    name: card.name,
                    description: card.description,
                    url: card.url,
                    badge: card.badge,
                    theme: card.theme,
                    icon: card.icon,
                    isRdpDownload: card.isRdpDownload,
                    isInstallerDownload: card.isInstallerDownload,
                    isVisible: card.isVisible,
                    sortOrder: card.sortOrder,
                    allowedRoles: card.allowedRoles,
                    allowedUserIds: card.allowedUserIds,
                    healthCheckUrl: card.healthCheckUrl,
                    deepLink: card.deepLink,
                    installerPath: card.installerPath,
                    installerVersion: card.installerVersion,
                    installerNotes: card.installerNotes,
                    maintenanceStartUtc: card.maintenanceStartUtc,
                    maintenanceEndUtc: card.maintenanceEndUtc,
                    status: card.status,
                    maintenanceNote: card.maintenanceNote,
                    rowVersion: card.rowVersion
                })
            });

            const responseText = await response.text();
            let result = {};
            try {
                result = responseText ? JSON.parse(responseText) : {};
            } catch {
                const message = responseText.replace(/<[^>]*>/g, ' ').replace(/\s+/g, ' ').trim();
                throw new Error(message || `Save failed with HTTP ${response.status}.`);
            }
            if (!response.ok || !result.success) {
                throw new Error(result.message || 'Save failed.');
            }

            const previousId = card.id;
            replaceCard(result.card, previousId);
            dirtyCards.delete(previousId);
            dirtyCards.delete(result.card.id);
            selectedId = result.card.id;
            saveStatus.textContent = 'Saved.';
            window.yakultToast?.('Service saved.', 'success');
            renderList();
            loadEditor();
        } catch (error) {
            failureMessage = error.message || 'Unable to save. Your edits are still available.';
            window.yakultToast?.(error.message || 'Unable to save service.', 'error');
            saveCardBtn.disabled = false;
        } finally {
            saveCardBtn.classList.remove('is-loading');
            setBusy(false, failureMessage);
        }
    }

    async function archiveSelectedCard() {
        const card = getCard(selectedId);
        if (busy || !card || isNewCard(card)) return;
        if (!confirm(`Archive "${card.name}"? It will be hidden from Services & Portals.${dirtyCards.has(selectedId) ? ' Unsaved changes to this service will be discarded.' : ''}`)) return;

        let failureMessage = '';
        setBusy(true, 'Archiving service…');
        archiveCardBtn.classList.add('is-loading');

        try {
            const response = await fetch(page.dataset.archiveUrl, {
                method: 'POST',
                credentials: 'same-origin',
                headers: {
                    'Content-Type': 'application/json',
                    'RequestVerificationToken': antiForgeryToken
                },
                body: JSON.stringify({
                    cardKey: card.cardKey,
                    rowVersion: card.rowVersion
                })
            });

            const result = await response.json();
            if (!response.ok || !result.success) {
                throw new Error(result.message || 'Archive failed.');
            }

            savedCards = savedCards.filter(item => item.id !== card.id);
            workingCards = workingCards.filter(item => item.id !== card.id);
            dirtyCards.delete(card.id);
            selectedId = workingCards[0]?.id || '';
            saveStatus.textContent = 'Archived.';
            window.yakultToast?.('Service archived.', 'success');
            renderList();
            loadEditor();
        } catch (error) {
            failureMessage = error.message || 'Unable to archive.';
            window.yakultToast?.(error.message || 'Unable to archive service.', 'error');
            archiveCardBtn.disabled = false;
        } finally {
            archiveCardBtn.classList.remove('is-loading');
            setBusy(false, failureMessage);
        }
    }

    function replaceCard(card, previousId) {
        const savedIndex = savedCards.findIndex(item => item.id === card.id);
        if (savedIndex >= 0) {
            savedCards[savedIndex] = { ...card };
        } else {
            savedCards.push({ ...card });
        }

        const workingIndex = workingCards.findIndex(item => item.id === (previousId || card.id));
        if (workingIndex >= 0) {
            workingCards[workingIndex] = { ...card };
        } else {
            workingCards.push({ ...card });
        }
    }

    function escapeHtml(value) {
        return String(value || '')
            .replaceAll('&', '&amp;')
            .replaceAll('<', '&lt;')
            .replaceAll('>', '&gt;')
            .replaceAll('"', '&quot;')
            .replaceAll("'", '&#039;');
    }

    function normalizeValue(value) {
        if (typeof value === 'boolean') return value ? 'true' : 'false';
        if (typeof value === 'number') return value.toString();
        return String(value || '');
    }

    function normalizeField(key, value) {
        if (key === 'maintenanceStartUtc' || key === 'maintenanceEndUtc') {
            const date = utcDate(value);
            return date && !Number.isNaN(date.getTime()) ? date.toISOString() : '';
        }
        return normalizeValue(value);
    }

    function launchLabel(value) {
        if (value === 'rdp') return 'RDP launch';
        if (value === 'installer') return 'Installer';
        if (value === 'rdpInstaller') return 'RDP + installer';
        if (value === 'disabled') return 'Disabled';
        return 'Web link';
    }

    function serviceIcon(name) {
        const icons = {
            package: '<path d="m12 3 9 5v8l-9 5-9-5V8l9-5Z"/><path d="m3 8 9 5 9-5M12 13v8M7.5 5.5l9 5"/>',
            users: '<circle cx="9" cy="8" r="3"/><path d="M3 21v-3a6 6 0 0 1 12 0v3M16 5a3 3 0 0 1 0 6M21 21v-3a6 6 0 0 0-3-5"/>',
            'bar-chart': '<path d="M4 3v17h17M9 15V9M14 15V5M19 15v-4"/>',
            clipboard: '<rect x="5" y="5" width="14" height="16" rx="2"/><rect x="9" y="2" width="6" height="5" rx="1"/><path d="M9 12h6M9 16h4"/>',
            smartphone: '<rect x="6" y="2" width="12" height="20" rx="2"/><path d="M10 18h4"/>',
            tool: '<path d="M14 6a5 5 0 0 0-6 6l-5 5a2.8 2.8 0 0 0 4 4l5-5a5 5 0 0 0 6-6l-3 3-4-4 3-3Z"/>',
            database: '<ellipse cx="12" cy="5" rx="8" ry="3"/><path d="M4 5v14c0 4 16 4 16 0V5M4 12c0 4 16 4 16 0"/>',
            book: '<path d="M12 5v16M12 5C8 2 3 3 3 3v16s5-1 9 2c4-3 9-2 9-2V3s-5-1-9 2Z"/>',
            grid: '<rect x="3" y="3" width="7" height="7" rx="1"/><rect x="14" y="3" width="7" height="7" rx="1"/><rect x="3" y="14" width="7" height="7" rx="1"/><rect x="14" y="14" width="7" height="7" rx="1"/>',
            layers: '<path d="m12 3 10 5-10 5L2 8l10-5ZM2 12l10 5 10-5M2 16l10 5 10-5"/>'
        };
        return `<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.6" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true">${Object.hasOwn(icons, name) ? icons[name] : icons.grid}</svg>`;
    }

    function utcDate(value) {
        if (!value) return null;
        const text = String(value);
        return new Date(/[zZ]$|[+-]\d{2}:?\d{2}$/.test(text) ? text : `${text}Z`);
    }

    function formatPht(value) {
        const date = utcDate(value);
        if (!date || Number.isNaN(date.getTime())) return 'No recorded check';
        return new Intl.DateTimeFormat('en-GB', {
            timeZone: 'Asia/Manila', day: '2-digit', month: 'short', year: 'numeric',
            hour: '2-digit', minute: '2-digit', hourCycle: 'h23'
        }).format(date) + ' PHT';
    }

    function toDateTimeLocal(value) {
        if (!value) return '';
        const date = utcDate(value);
        if (Number.isNaN(date.getTime())) return '';
        return new Date(date.getTime() + 8 * 60 * 60 * 1000).toISOString().slice(0, 16);
    }

    function fromDateTimeLocal(value, original) {
        if (!value) return null;
        // Keep the original precision when the schedule itself has not changed.
        if (value === toDateTimeLocal(original)) return original;
        const date = new Date(`${value}+08:00`);
        return Number.isNaN(date.getTime()) ? null : date.toISOString();
    }

    function validateService() {
        const start = fields.maintenanceStartUtc;
        const end = fields.maintenanceEndUtc;
        fields.name.setCustomValidity(fields.name.value.trim() ? '' : 'Enter a service name.');
        fields.cardKey.setCustomValidity(fields.cardKey.value.trim() ? '' : 'Enter a unique service key.');
        start.setCustomValidity(!start.value && end.value ? 'Choose when maintenance starts, or clear both times.' : '');
        end.setCustomValidity(start.value && !end.value ? 'Choose when maintenance ends, or clear both times.'
            : start.value && end.value && end.value <= start.value ? 'The end must be after the start.' : '');
        const invalid = Object.values(fields).find(field => !field.validity.valid);
        if (!invalid) return true;
        const panel = invalid.closest('[data-tab-panel]');
        if (panel) activateTab(panel.dataset.tabPanel);
        if (invalid === fields.cardKey) advancedSettings.open = true;
        invalid.reportValidity();
        invalid.focus();
        return false;
    }

    function applyLaunchFieldVisibility() {
        const launchType = fields.launchType.value;
        const urlField = document.querySelector('[data-launch-field="url"]');
        const installerField = document.querySelector('[data-launch-field="installer"]');
        const shouldShowUrl = launchType === 'url';
        const shouldShowInstaller = launchType === 'installer' || launchType === 'rdpInstaller';
        if (urlField) urlField.style.display = shouldShowUrl ? '' : 'none';
        if (installerField) installerField.style.display = shouldShowInstaller ? '' : 'none';
    }


    function formatUploadSize(bytes) {
        if (!Number.isFinite(bytes) || bytes <= 0) return '';
        const units = ['B', 'KB', 'MB', 'GB'];
        let size = bytes;
        let index = 0;
        while (size >= 1024 && index < units.length - 1) {
            size /= 1024;
            index++;
        }
        return `${size.toFixed(size >= 10 || index === 0 ? 0 : 1)} ${units[index]}`;
    }

    function renderInstallerUploadDetails(details) {
        const detailsBox = document.getElementById('installerUploadDialogDetails');
        if (!detailsBox) return;
        const rows = [
            ['Original file', details?.originalName],
            ['Upload type', details?.uploadType],
            ['Transferred', details?.transferredText],
            ['Stored as', details?.storedName],
            ['Installer path', details?.storedPath]
        ].filter(row => row[1]);
        detailsBox.replaceChildren(...rows.map(([label, value]) => {
            const row = document.createElement('div');
            const labelNode = document.createElement('span');
            const valueNode = document.createElement('strong');
            labelNode.textContent = label;
            valueNode.textContent = value;
            row.append(labelNode, valueNode);
            return row;
        }));
    }

    function setInstallerUploadDialog(state, title, message, percent, fileText, details) {
        const overlay = document.getElementById('installerUploadDialog');
        const panel = document.getElementById('installerUploadDialogPanel');
        const progress = document.getElementById('installerUploadDialogProgress');
        const percentText = document.getElementById('installerUploadDialogPercent');
        const fileLabel = document.getElementById('installerUploadDialogFile');
        const icon = document.getElementById('installerUploadDialogIcon');
        if (!overlay || !panel) return;
        overlay.classList.add('is-visible');
        overlay.setAttribute('aria-hidden', 'false');
        panel.classList.toggle('is-complete', state === 'complete');
        panel.classList.toggle('is-error', state === 'error');
        if (icon) icon.textContent = state === 'error' ? '!' : '✓';
        document.getElementById('installerUploadDialogTitle').textContent = title;
        document.getElementById('installerUploadDialogMessage').textContent = message;
        const boundedPercent = Math.max(0, Math.min(percent || 0, 100));
        if (progress) progress.style.width = `${boundedPercent}%`;
        if (percentText) percentText.textContent = `${Math.round(boundedPercent)}%`;
        if (fileLabel) fileLabel.textContent = fileText || 'Selected file';
        renderInstallerUploadDetails(details);
    }

    function hideInstallerUploadDialog() {
        const overlay = document.getElementById('installerUploadDialog');
        if (!overlay) return;
        overlay.classList.remove('is-visible');
        overlay.setAttribute('aria-hidden', 'true');
    }

    function uploadInstallerRequest(formData, onProgress) {
        return new Promise((resolve, reject) => {
            const xhr = new XMLHttpRequest();
            xhr.open('POST', page.dataset.uploadUrl);
            xhr.setRequestHeader('RequestVerificationToken', antiForgeryToken);
            xhr.upload.onprogress = event => {
                if (event.lengthComputable && onProgress) {
                    onProgress({ percent: Math.round((event.loaded / event.total) * 100), loaded: event.loaded, total: event.total });
                }
            };
            xhr.onload = () => {
                let result = {};
                try {
                    result = xhr.responseText ? JSON.parse(xhr.responseText) : {};
                } catch {
                    const message = xhr.responseText.replace(/<[^>]*>/g, ' ').replace(/\s+/g, ' ').trim();
                    reject(new Error(message || `Upload failed with HTTP ${xhr.status}.`));
                    return;
                }
                if (xhr.status < 200 || xhr.status >= 300 || result.success === false) {
                    reject(new Error(result.message || 'Upload failed.'));
                    return;
                }
                resolve(result);
            };
            xhr.onerror = () => reject(new Error('Upload failed because the connection was interrupted.'));
            xhr.onabort = () => reject(new Error('Upload was cancelled.'));
            xhr.send(formData);
        });
    }

    async function uploadInstallerFile() {
        if (busy) return;
        const card = getCard(selectedId);
        const file = installerFileInput?.files?.[0];
        if (!card || !file) {
            window.yakultToast?.('Choose an installer file first.', 'error');
            return;
        }

        const fileSize = formatUploadSize(file.size);
        const fileText = `${file.name}${fileSize ? ' - ' + fileSize : ''}`;
        const baseDetails = {
            originalName: file.name,
            uploadType: 'Installer package',
            transferredText: fileSize ? `0 B of ${fileSize}` : '',
            storedName: 'Assigned after upload',
            storedPath: 'Assigned after upload'
        };
        setBusy(true, 'Uploading installer…');
        uploadInstallerBtn.classList.add('is-loading');
        if (installerUploadStatus) installerUploadStatus.textContent = `Uploading installer: ${file.name}`;

        try {
            const formData = new FormData();
            formData.append('file', file);
            formData.append('cardKey', fields.cardKey.value.trim() || card.cardKey || 'desktop-app');

            setInstallerUploadDialog('uploading', 'Uploading installer', `Sending ${file.name} to the server...`, 0, fileText, baseDetails);
            const result = await uploadInstallerRequest(formData, progress => {
                const transferred = progress?.total ? `${formatUploadSize(progress.loaded)} of ${formatUploadSize(progress.total)}` : baseDetails.transferredText;
                setInstallerUploadDialog('uploading', 'Uploading installer', `Sending ${file.name} to the server...`, progress?.percent || 0, fileText, { ...baseDetails, transferredText: transferred });
            });

            const installerPath = result.installerPath || '';
            const storedName = installerPath ? installerPath.split(/[\\/]/).pop() : '';
            fields.installerPath.value = installerPath;
            if (installerSelectedFile) installerSelectedFile.textContent = `Uploaded installer: ${storedName || result.fileName || file.name}`;
            if (installerUploadStatus) installerUploadStatus.textContent = `Uploaded and linked: ${storedName || result.fileName || file.name}`;
            updateDraftFromEditor();
            setInstallerUploadDialog('complete', 'Upload complete', `${result.fileName || file.name} was renamed and linked to this service. Save the service to keep the path.`, 100, fileText, {
                ...baseDetails,
                transferredText: fileSize ? `${fileSize} of ${fileSize}` : '',
                storedName: storedName || result.fileName || file.name,
                storedPath: installerPath
            });
            installerFileInput.value = '';
            window.yakultToast?.('Installer uploaded. Save the service to keep this path.', 'success');
        } catch (error) {
            if (installerUploadStatus) installerUploadStatus.textContent = error.message || 'Upload failed.';
            setInstallerUploadDialog('error', 'Upload failed', error.message || 'Unable to upload the selected installer.', 100, fileText, baseDetails);
            window.yakultToast?.(error.message || 'Installer upload failed.', 'error');
        } finally {
            setBusy(false);
            uploadInstallerBtn.classList.remove('is-loading');
        }
    }
    function activateTab(name) {
        page.querySelectorAll('.admin-tab').forEach(tab => {
            const active = tab.dataset.tab === name;
            tab.classList.toggle('is-active', active);
            tab.setAttribute('aria-selected', String(active));
            tab.tabIndex = active ? 0 : -1;
        });
        page.querySelectorAll('.admin-tab-panel').forEach(panel => {
            const active = panel.dataset.tabPanel === name;
            panel.classList.toggle('is-active', active);
            panel.hidden = !active;
        });
    }

    const tabs = [...page.querySelectorAll('.admin-tab')];
    tabs.forEach((tab, index) => {
        tab.addEventListener('click', function() {
            activateTab(this.dataset.tab);
        });
        tab.addEventListener('keydown', event => {
            let next;
            if (event.key === 'ArrowRight') next = (index + 1) % tabs.length;
            if (event.key === 'ArrowLeft') next = (index + tabs.length - 1) % tabs.length;
            if (event.key === 'Home') next = 0;
            if (event.key === 'End') next = tabs.length - 1;
            if (next === undefined || busy) return;
            event.preventDefault();
            activateTab(tabs[next].dataset.tab);
            tabs[next].focus();
        });
    });

    Object.values(fields).forEach(field => {
        field.addEventListener('input', () => {
            field.setCustomValidity('');
            if (field === fields.maintenanceStartUtc || field === fields.maintenanceEndUtc) {
                fields.maintenanceStartUtc.setCustomValidity('');
                fields.maintenanceEndUtc.setCustomValidity('');
            }
            updateDraftFromEditor();
        });
        field.addEventListener('change', updateDraftFromEditor);
    });
    cardSearch.addEventListener('input', renderList);
    cardFilter.addEventListener('change', renderList);
    newCardBtn.addEventListener('click', createNewCard);
    document.getElementById('emptyAddService').addEventListener('click', createNewCard);
    resetCardBtn.addEventListener('click', resetSelectedCard);
    resetDraftsBtn.addEventListener('click', resetAllDrafts);
    saveCardBtn.addEventListener('click', saveSelectedCard);
    archiveCardBtn.addEventListener('click', archiveSelectedCard);
    editForm.addEventListener('submit', event => { event.preventDefault(); saveSelectedCard(); });
    filterButtons.forEach(button => button.addEventListener('click', () => {
        directoryFilter = button.dataset.serviceFilter;
        renderList();
    }));
    clearFiltersBtn.addEventListener('click', () => {
        directoryFilter = 'all';
        cardSearch.value = '';
        cardFilter.value = 'all';
        renderList();
        cardSearch.focus();
    });
    document.getElementById('backToServices').addEventListener('click', () => {
        cardSearch.scrollIntoView({ block: 'center', behavior: 'instant' });
        cardSearch.focus({ preventScroll: true });
    });
    installerFileInput?.addEventListener('change', function() {
        const file = this.files?.[0];
        if (installerSelectedFile) installerSelectedFile.textContent = file ? `Selected upload file: ${file.name}${formatUploadSize(file.size) ? ' - ' + formatUploadSize(file.size) : ''}` : 'No upload file selected.';
    });
    uploadInstallerBtn?.addEventListener('click', uploadInstallerFile);
    document.getElementById('installerUploadDialogClose')?.addEventListener('click', hideInstallerUploadDialog);
    window.addEventListener('beforeunload', function(event) {
        if (dirtyCards.size === 0 && !busy) return;
        event.preventDefault();
        event.returnValue = '';
    });

    if (!requestedService) selectedId = getFilteredCards()[0]?.id || workingCards[0]?.id || '';
    renderList();
    loadEditor();
});
