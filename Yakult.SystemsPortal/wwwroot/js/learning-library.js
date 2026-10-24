(() => {
    'use strict';
    const root = document.getElementById('learningHub') || document.getElementById('academyMyLearning');
    if (!root) return;
    try {
        root.querySelectorAll('[data-course-key]').forEach(card => {
            if (!card.dataset.courseKey) return;
            const saved = JSON.parse(localStorage.getItem(card.dataset.courseKey) || 'null');
            if (!saved || !Array.isArray(saved.completed)) return;
            const total = Number(card.dataset.lessonCount), count = Math.min(total, new Set(saved.completed).size);
            card.querySelector('[data-course-status]').textContent = `${count} of ${total} complete · this browser`;
            card.querySelector('[data-course-action]').textContent = count === total ? 'Review course →' : 'Continue learning →';
        });
        const courses = new Map();
        for (let index = 0; index < localStorage.length; index++) {
            const key = localStorage.key(index);
            if (!key?.startsWith(root.dataset.progressPrefix)) continue;
            if (!/^-?\d+\.[A-F0-9]{16}$/.test(key.slice(root.dataset.progressPrefix.length))) continue;
            let saved;
            try { saved = JSON.parse(localStorage.getItem(key)); } catch (_) { continue; }
            if (!saved || !Array.isArray(saved.completed) || !saved.title || !saved.href || !Number.isInteger(saved.lessonCount) || saved.lessonCount < 1) continue;
            const url = new URL(saved.href, location.origin);
            if (url.origin !== location.origin || !/^\/content\/[^/]+$/.test(url.pathname)) continue;
            if (!courses.has(url.pathname) || saved.updatedAt > courses.get(url.pathname).updatedAt) courses.set(url.pathname, { ...saved, href: url.pathname });
        }
        const ordered = [...courses.values()].sort((a, b) => b.updatedAt - a.updatedAt);
        if (root.id === 'academyMyLearning') {
            const list = root.querySelector('#academyBrowserCourses');
            ordered.forEach(course => {
                const link = document.createElement('a'); link.href = course.href; link.className = 'academy-browser-course';
                const name = document.createElement('strong'); name.textContent = course.title;
                const status = document.createElement('span'); status.textContent = `${Math.min(course.completed.length, course.lessonCount)} of ${course.lessonCount} lessons complete`;
                const action = document.createElement('span'); action.textContent = course.completed.length >= course.lessonCount ? 'Review course →' : 'Continue learning →';
                link.append(name, status, action); list.appendChild(link);
            });
            root.hidden = ordered.length === 0;
            if (ordered.length) { const empty = document.querySelector('.my-learning-empty'); if (empty) empty.hidden = true; }
            return;
        }
        const latest = ordered.find(course => course.completed.length < course.lessonCount);
        if (latest) {
            root.querySelector('#learningContinue').hidden = false;
            root.querySelector('#learningContinueHeading').textContent = latest.title;
            root.querySelector('#learningContinueDetail').textContent = `${latest.completed.length} of ${latest.lessonCount} lessons complete on this browser.`;
            root.querySelector('#learningContinueLink').href = latest.href;
        }
    } catch (_) { /* Server-rendered catalog remains usable without storage. */ }
})();
