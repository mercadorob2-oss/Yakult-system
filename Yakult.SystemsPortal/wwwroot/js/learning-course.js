(() => {
    'use strict';
    const root = document.getElementById('academyCourse');
    if (!root) return;
    const lessons = Array.from(root.querySelectorAll('[data-lesson]'));
    const ids = lessons.map(x => x.dataset.lesson);
    const links = Array.from(root.querySelectorAll('[data-lesson-link]'));
    const key = root.dataset.storageKey;
    const start = root.querySelector('#academyStart');
    const overview = root.querySelector('#academyOverview');
    const finished = root.querySelector('#academyFinished');
    let state = { completed: [], current: ids[0] };
    try {
        const saved = JSON.parse(localStorage.getItem(key) || 'null');
        if (saved && Array.isArray(saved.completed)) state = { completed: saved.completed.filter(id => ids.includes(id)), current: ids.includes(saved.current) ? saved.current : ids[0] };
    } catch (_) { /* The course remains usable when browser storage is unavailable. */ }
    const completed = new Set(state.completed);
    let current = state.current;
    function persist() {
        try { localStorage.setItem(key, JSON.stringify({ completed: [...completed], current, updatedAt: Date.now(), title: root.dataset.title, href: location.pathname, lessonCount: ids.length })); }
        catch (_) { root.querySelector('#academyStorageNote').textContent = 'Browser storage is unavailable. Progress will last for this visit only.'; }
    }
    function updateProgress() {
        root.querySelector('#academyProgress').value = completed.size;
        root.querySelector('#academyProgressLabel').textContent = `${completed.size} of ${lessons.length} lessons complete`;
        links.forEach(link => {
            const done = completed.has(link.dataset.lessonLink);
            link.querySelector('.academy-lesson-check').textContent = done ? '✓' : '○';
            link.querySelector('[data-completion-label]').textContent = done ? 'Completed' : 'Not completed';
            link.classList.toggle('is-complete', done);
        });
        start.firstChild.textContent = completed.size === lessons.length ? 'Review course ' : completed.size ? 'Continue learning ' : 'Start course ';
    }
    function show(id, focus = true) {
        if (!ids.includes(id)) return;
        root.querySelectorAll('video').forEach(video => video.pause());
        // Unload inactive embeds so changing lessons cannot leave audio playing.
        root.querySelectorAll('iframe').forEach(frame => {
            frame.dataset.source ||= frame.getAttribute('src');
            if (frame.closest('[data-lesson]').dataset.lesson === id) frame.src = frame.dataset.source;
            else frame.removeAttribute('src');
        });
        current = id;
        lessons.forEach(lesson => { lesson.hidden = lesson.dataset.lesson !== id; });
        links.forEach(link => {
            if (link.dataset.lessonLink === id) link.setAttribute('aria-current', 'step');
            else link.removeAttribute('aria-current');
        });
        finished.hidden = true;
        overview.hidden = true;
        persist();
        updateProgress();
        const lesson = lessons.find(x => x.dataset.lesson === id);
        const last = ids.indexOf(id) === ids.length - 1;
        lesson.querySelector('[data-complete]').textContent = completed.has(id) ? (last ? 'Finish review ✓' : 'Next lesson →') : (last ? 'Mark complete & finish ✓' : 'Mark complete & continue →');
        if (focus) {
            history.replaceState(null, '', '#lesson-' + id);
            lesson.querySelector('h2').focus({ preventScroll: true });
            lesson.scrollIntoView({ block: 'start' });
            if (matchMedia('(max-width: 760px)').matches) root.querySelector('#academyOutline').open = false;
        }
    }
    links.forEach(link => link.addEventListener('click', event => { event.preventDefault(); show(link.dataset.lessonLink); }));
    lessons.forEach((lesson, index) => {
        lesson.querySelector('.academy-lesson-actions').hidden = false;
        lesson.querySelector('[data-previous]').addEventListener('click', () => show(ids[index - 1]));
        lesson.querySelector('[data-complete]').addEventListener('click', () => {
            const pending = lesson.querySelector('.academy-quiz:not([data-passed])');
            if (pending && !completed.has(lesson.dataset.lesson)) {
                const feedback = pending.querySelector('.academy-quiz-feedback');
                feedback.hidden = false;
                feedback.textContent = 'Answer this practice question correctly before continuing.';
                pending.querySelector('input').focus();
                return;
            }
            completed.add(lesson.dataset.lesson);
            persist();
            updateProgress();
            if (index + 1 < lessons.length) show(ids[index + 1]);
            else if (completed.size < lessons.length) show(ids.find(id => !completed.has(id)));
            else {
                root.querySelectorAll('video').forEach(video => video.pause());
                root.querySelectorAll('iframe').forEach(frame => frame.removeAttribute('src'));
                lessons.forEach(x => { x.hidden = true; });
                finished.hidden = false;
                finished.scrollIntoView({ block: 'center' });
            }
        });
    });
    root.querySelectorAll('.academy-quiz').forEach(form => {
        form.addEventListener('submit', event => {
            event.preventDefault();
            const choice = new FormData(form).get('answer');
            const correct = choice !== null && Number(choice) === Number(form.dataset.answer);
            const feedback = form.querySelector('.academy-quiz-feedback');
            feedback.hidden = false;
            feedback.textContent = (correct ? 'Correct. ' : 'Try again. ') + form.dataset.explanation;
            feedback.classList.toggle('is-correct', correct);
            if (correct) form.dataset.passed = 'true';
        });
    });
    start.hidden = false;
    start.addEventListener('click', () => show(current));
    root.querySelector('#academyReview').addEventListener('click', () => show(ids[0]));
    const hashId = location.hash.startsWith('#lesson-') ? location.hash.slice(8) : null;
    show(ids.includes(hashId) ? hashId : current, false);
    overview.hidden = false;
})();
