(function () {
  const root = document.querySelector('.auth-login-page');
  if (!root) {
    return;
  }

  const buttons = Array.from(root.querySelectorAll('[data-login-switch]'));
  if (!buttons.length) {
    return;
  }

  function applyMode(mode) {
    const nextMode = mode === 'jira' ? 'jira' : 'local';
    root.dataset.loginMode = nextMode;

    buttons.forEach((button) => {
      const active = button.dataset.loginSwitch === nextMode;
      button.classList.toggle('is-active', active);
      button.setAttribute('aria-selected', active ? 'true' : 'false');
    });
  }

  buttons.forEach((button) => {
    button.addEventListener('click', function () {
      applyMode(button.dataset.loginSwitch);
    });
  });

  applyMode(root.dataset.loginMode);
})();
