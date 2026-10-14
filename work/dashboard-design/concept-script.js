
(() => {
  const root = document.getElementById('yakult-design');
  const search = root.querySelector('#yk-system-search');
  const modules = Array.from(root.querySelectorAll('#yk-search-results .yk-module'));
  function syncPins(names) {
    const nav = root.querySelector('.yk-sidebar');
    const home = Array.from(nav.querySelectorAll('.yk-nav')).slice(0, 2);
    Array.from(nav.querySelectorAll('.yk-nav')).forEach(item => { if (!home.includes(item)) item.remove(); });
    root.querySelectorAll('.yk-pin').forEach(pin => {
      const name = pin.getAttribute('aria-label').replace(/^Pin /, '');
      const selected = names.includes(name);
      pin.setAttribute('aria-pressed', String(selected));
      if (!selected) return;
      const shortcut = document.createElement('button');
      shortcut.className = 'yk-nav'; shortcut.textContent = name;
      shortcut.dataset.open = name; shortcut.dataset.pinned = 'true';
      nav.insertBefore(shortcut, nav.querySelector('footer'));
    });
  }
  function restoreState(snapshot) {
    const names = snapshot && snapshot.modelContent && snapshot.modelContent.dashboardPinnedSystems;
    if (Array.isArray(names) && names.every(name => typeof name === 'string')) syncPins(names);
  }
  restoreState(window.openai && window.openai.widgetState);
  window.addEventListener('openai:set_globals', event => {
    if (event.detail && event.detail.globals) restoreState(event.detail.globals.widgetState);
  });
  root.addEventListener('click', event => {
    const target = event.target.closest('button');
    if (!target) return;
    const product = target.closest('.yk-window');
    const feedback = product.querySelector('.yk-feedback');
    if (target.classList.contains('yk-pin')) {
      const selected = target.getAttribute('aria-pressed') !== 'true';
      target.setAttribute('aria-pressed', String(selected));
      const name = target.getAttribute('aria-label').replace(/^Pin /, '');
      const names = Array.from(root.querySelectorAll('.yk-pin[aria-pressed="true"]')).map(item => item.getAttribute('aria-label').replace(/^Pin /, ''));
      syncPins(names);
      feedback.textContent = name + (selected ? ' pinned to your workspace.' : ' removed from pinned systems.');
    } else if (target.dataset.open) {
      feedback.textContent = 'Preview destination: ' + target.dataset.open + '. No live records are opened or changed.';
    } else return;
    feedback.hidden = false;
    if (window.openai && typeof window.openai.setWidgetState === 'function') {
      const pinned = Array.from(root.querySelectorAll('.yk-pin[aria-pressed="true"]')).map(item => item.getAttribute('aria-label').replace(/^Pin /, ''));
      window.openai.setWidgetState({modelContent: {dashboardPinnedSystems: pinned}, privateContent: null}).catch(() => {});
    }
  });
  search.addEventListener('input', () => {
    const needle = search.value.trim().toLocaleLowerCase();
    modules.forEach(module => { module.hidden = !module.textContent.toLocaleLowerCase().includes(needle); });
    root.querySelector('#yk-empty-search').hidden = modules.some(module => !module.hidden);
  });
  const designs = [document.getElementById('yk-daily'), document.getElementById('yk-compact')];
  designs.forEach(product => {
    const settings = {spacing: 24, accent: 'Yakult red'};
    function applyDesign() {
      product.style.setProperty('--yk-gap', settings.spacing + 'px');
      product.style.setProperty('--yk-red', settings.accent === 'Yakult red' ? 'light-dark(#b92335,#ff8995)' : 'light-dark(#254b7c,#9fc6ff)');
      product.style.setProperty('--yk-tint', settings.accent === 'Yakult red' ? 'light-dark(#fcf0f1,#39262d)' : 'light-dark(#edf3fb,#202f46)');
    }
    applyDesign();
    if (globalThis.Tweak) {
      const tweak = new Tweak({container: product, onChange: applyDesign});
      tweak.addSelect(settings, 'accent', {label: 'Brand accent', options: ['Yakult red', 'Current navy']});
      if (product.id === 'yk-daily') tweak.addSlider(settings, 'spacing', {label: 'Content spacing', min: 16, max: 32, step: 4, unit: 'px'});
    }
  });
})();

