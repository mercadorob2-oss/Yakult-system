(() => {
    const app = document.getElementById('employeeResourcesApp');
    if (!app) return;

    const form = document.getElementById('employeeResourcesFilters');
    const search = document.getElementById('employeeResourceSearch');
    const category = document.getElementById('employeeResourceCategory');
    const type = document.getElementById('employeeResourceType');
    const clear = document.getElementById('employeeResourceClear');
    const resultCount = document.getElementById('employeeResourceResultCount');
    const emptyState = document.getElementById('employeeResourceEmpty');
    const cards = Array.from(document.querySelectorAll('[data-resource-card]'));

    const normalize = value => (value || '').trim().toLocaleLowerCase();
    const initialQuery = app.dataset.initialQuery || '';
    const initialCategory = app.dataset.initialCategory || '';
    const initialType = app.dataset.initialType || '';

    if (search && !search.value && initialQuery) search.value = initialQuery;
    if (category && initialCategory) category.value = initialCategory;
    if (type && initialType) type.value = initialType;

    function updateUrl() {
        const url = new URL(window.location.href);
        const values = [
            ['q', search?.value],
            ['category', category?.value],
            ['type', type?.value]
        ];

        values.forEach(([key, value]) => {
            const normalized = (value || '').trim();
            if (normalized) url.searchParams.set(key, normalized);
            else url.searchParams.delete(key);
        });

        window.history.replaceState({}, '', `${url.pathname}${url.search}${url.hash}`);
    }

    function applyFilters(shouldUpdateUrl = true) {
        const query = normalize(search?.value);
        const selectedCategory = normalize(category?.value);
        const selectedType = normalize(type?.value);
        let visibleCount = 0;

        cards.forEach(card => {
            const matchesQuery = !query || normalize(card.dataset.searchText).includes(query);
            const matchesCategory = !selectedCategory || normalize(card.dataset.category) === selectedCategory;
            const matchesType = !selectedType || normalize(card.dataset.type) === selectedType;
            const visible = matchesQuery && matchesCategory && matchesType;

            card.classList.toggle('is-hidden', !visible);
            card.setAttribute('aria-hidden', visible ? 'false' : 'true');
            if (visible) visibleCount += 1;
        });

        if (resultCount) {
            resultCount.textContent = `${visibleCount} resource${visibleCount === 1 ? '' : 's'} shown`;
        }
        if (emptyState) emptyState.hidden = visibleCount > 0;
        if (shouldUpdateUrl) updateUrl();
    }

    function clearFilters() {
        if (search) search.value = '';
        if (category) category.value = '';
        if (type) type.value = '';
        applyFilters();
        search?.focus();
    }

    search?.addEventListener('input', () => applyFilters());
    category?.addEventListener('change', () => applyFilters());
    type?.addEventListener('change', () => applyFilters());
    clear?.addEventListener('click', clearFilters);
    document.querySelectorAll('[data-clear-resources]').forEach(button => button.addEventListener('click', clearFilters));
    form?.addEventListener('submit', event => {
        event.preventDefault();
        applyFilters();
    });

    applyFilters(false);
})();
