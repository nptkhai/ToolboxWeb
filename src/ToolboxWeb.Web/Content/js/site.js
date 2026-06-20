(function () {
  const shell = document.querySelector('.app-shell');
  const savedSidebar = localStorage.getItem('toolbox.sidebar');
  if (shell && savedSidebar === 'collapsed') {
    shell.classList.add('sidebar-collapsed');
  }

  document.querySelector('[data-sidebar-toggle]')?.addEventListener('click', function () {
    shell?.classList.toggle('sidebar-collapsed');
    localStorage.setItem('toolbox.sidebar', shell?.classList.contains('sidebar-collapsed') ? 'collapsed' : 'expanded');
  });

  document.querySelector('[data-theme-toggle]')?.addEventListener('click', function () {
    const next = document.documentElement.dataset.theme === 'dark' ? 'light' : 'dark';
    document.documentElement.dataset.theme = next;
    localStorage.setItem('toolbox.theme', next);
  });

  document.querySelectorAll('[data-copy-target]').forEach(function (button) {
    button.addEventListener('click', async function () {
      const target = document.querySelector(button.getAttribute('data-copy-target'));
      if (!target) return;
      await navigator.clipboard.writeText(target.textContent || '');
      const original = button.dataset.defaultText || button.textContent || '';
      const copiedText = button.dataset.copiedText || 'Copied';
      button.textContent = copiedText;
      setTimeout(function () { button.textContent = original || 'Copy'; }, 1200);
    });
  });

  const timer = document.querySelector('[data-focus-timer]');
  if (!timer) return;

  let remaining = Number(timer.dataset.minutes || '25') * 60;
  let intervalId = null;
  const display = timer.querySelector('[data-timer-display]');
  const minutesInput = document.querySelector('[name="DurationMinutes"]');

  function render() {
    const minutes = Math.floor(remaining / 60).toString().padStart(2, '0');
    const seconds = (remaining % 60).toString().padStart(2, '0');
    if (display) display.textContent = `${minutes}:${seconds}`;
  }

  function resetFromInput() {
    remaining = Math.max(1, Number(minutesInput?.value || '25')) * 60;
    render();
  }

  timer.querySelector('[data-timer-start]')?.addEventListener('click', function () {
    if (intervalId) return;
    intervalId = window.setInterval(function () {
      remaining -= 1;
      render();
      if (remaining <= 0) {
        window.clearInterval(intervalId);
        intervalId = null;
      }
    }, 1000);
  });

  timer.querySelector('[data-timer-pause]')?.addEventListener('click', function () {
    window.clearInterval(intervalId);
    intervalId = null;
  });

  timer.querySelector('[data-timer-reset]')?.addEventListener('click', resetFromInput);
  minutesInput?.addEventListener('change', resetFromInput);
  render();
})();
