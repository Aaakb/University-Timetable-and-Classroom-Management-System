(() => {
  const key = 'university-timetable-theme';
  const root = document.documentElement;
  let theme = 'light';
  try {
    if (localStorage.getItem(key) === 'dark') theme = 'dark';
  } catch { /* The toggle still works when storage is unavailable. */ }

  const sun = '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" aria-hidden="true"><circle cx="12" cy="12" r="4"/><path d="M12 2v2m0 16v2M4.93 4.93l1.42 1.42m11.3 11.3 1.42 1.42M2 12h2m16 0h2M4.93 19.07l1.42-1.42m11.3-11.3 1.42-1.42"/></svg>';
  const moon = '<svg viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.8" stroke-linecap="round" stroke-linejoin="round" aria-hidden="true"><path d="M20.5 14A8.5 8.5 0 0 1 10 3.5 8.5 8.5 0 1 0 20.5 14Z"/></svg>';

  function applyTheme() {
    root.dataset.theme = theme;
    const meta = document.querySelector('meta[name="theme-color"]');
    if (meta) meta.content = theme === 'light' ? '#faf8f2' : '#090f29';
    document.querySelectorAll('.theme-toggle').forEach(button => {
      const next = theme === 'dark' ? 'الوضع الفاتح' : 'الوضع الداكن';
      button.setAttribute('aria-label', `تفعيل ${next}`);
      button.title = `تفعيل ${next}`;
      button.innerHTML = `${theme === 'dark' ? sun : moon}<span class="theme-toggle__label">${next}</span>`;
    });
  }

  function makeButton() {
    const button = document.createElement('button');
    button.type = 'button';
    button.className = 'theme-toggle';
    button.addEventListener('click', () => {
      theme = theme === 'dark' ? 'light' : 'dark';
      try { localStorage.setItem(key, theme); } catch { /* Ignore unavailable storage. */ }
      applyTheme();
    });
    return button;
  }

  // Apply before the page is painted, then add controls when its layout exists.
  applyTheme();
  document.addEventListener('DOMContentLoaded', () => {
    document.querySelector('.app-layout .account')?.prepend(makeButton());
    const authPanel = document.querySelector('.auth-panel');
    if (authPanel) {
      const button = makeButton();
      button.classList.add('theme-toggle--auth');
      authPanel.prepend(button);
    }
    const standalone = document.querySelector('.preferences-page');
    if (standalone) {
      const toolbar = document.createElement('div');
      toolbar.className = 'theme-toolbar';
      toolbar.append(makeButton());
      standalone.prepend(toolbar);
    }
    applyTheme();
  });
  window.addEventListener('storage', event => {
    if (event.key === key || event.key === null) {
      theme = event.newValue === 'dark' ? 'dark' : 'light';
      applyTheme();
    }
  });
})();
