(function () {
  const form = document.querySelector('[data-jira-search-form]');
  if (!form) {
    return;
  }

  const advancedToggle = form.querySelector('[data-jira-advanced-toggle]');
  const advancedPanel = form.querySelector('[data-jira-advanced-panel]');
  const filterSelect = form.querySelector('[data-jira-filter-select]');
  const advancedJql = form.querySelector('textarea[name="Search.AdvancedJql"]');

  function syncAdvancedPanel() {
    if (!advancedToggle || !advancedPanel) {
      return;
    }

    advancedPanel.classList.toggle('d-none', !advancedToggle.checked);
  }

  advancedToggle?.addEventListener('change', syncAdvancedPanel);

  filterSelect?.addEventListener('change', function () {
    const selected = filterSelect.options[filterSelect.selectedIndex];
    const jql = selected?.dataset?.jql || '';
    if (!jql || !advancedJql || !advancedToggle) {
      return;
    }

    advancedJql.value = jql;
    advancedToggle.checked = true;
    syncAdvancedPanel();
  });

  syncAdvancedPanel();
})();
