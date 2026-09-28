'use strict';

// These bounded illustrations run entirely in the browser; they do not connect to an app.
const nodeExamples = {
  save: { type: 'Button', name: 'SaveButton', size: '144 × 40', label: 'Action', value: 'Invoke' },
  name: {
    type: 'TextBox',
    name: 'WorkspaceName',
    size: '280 × 34',
    label: 'Text',
    value: 'Northstar',
  },
  notifications: {
    type: 'ToggleSwitch',
    name: 'EmailNotifications',
    size: '280 × 36',
    label: 'Checked',
    value: 'True',
  },
};

document.querySelectorAll('[data-node]').forEach((button) => {
  button.addEventListener('click', () => {
    const key = button.dataset.node;
    const node = nodeExamples[key];
    document.querySelectorAll('[data-node]').forEach((item) => {
      const selected = item.dataset.node === key;
      item.classList.toggle('selected', selected);
      item.setAttribute('aria-pressed', String(selected));
    });
    document.querySelector('#node-type').textContent = node.type;
    document.querySelector('#node-name').textContent = node.name;
    document.querySelector('#node-size').textContent = node.size;
    document.querySelector('#node-extra-label').textContent = node.label;
    document.querySelector('#node-extra').textContent = node.value;
    document.querySelector('.selection-dimension').style.visibility =
      key === 'save' ? 'visible' : 'hidden';
  });
});

document.querySelectorAll('[data-theme]').forEach((button) => {
  button.addEventListener('click', () => {
    const theme = button.dataset.theme;
    document.querySelector('#preview-card').classList.toggle('dark', theme === 'dark');
    document.querySelectorAll('[data-theme]').forEach((item) => {
      const selected = item.dataset.theme === theme;
      item.classList.toggle('active', selected);
      item.setAttribute('aria-pressed', String(selected));
    });
  });
});

const comparisonRange = document.querySelector('#comparison-range');
comparisonRange.addEventListener('input', () => {
  document.querySelector('#comparison').style.setProperty('--split', `${comparisonRange.value}%`);
  comparisonRange.setAttribute('aria-valuetext', `${comparisonRange.value}% reference visible`);
});

const runButton = document.querySelector('#run-workflow');
const workflowStatus = document.querySelector('#workflow-status');
const steps = [...document.querySelectorAll('[data-step]')];
const reducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)');

runButton.addEventListener('click', async () => {
  if (runButton.disabled) return;
  runButton.disabled = true;
  runButton.textContent = 'Running…';
  workflowStatus.textContent = 'Playing the example…';
  steps.forEach((step, index) => {
    step.classList.remove('complete', 'running');
    step.querySelector('.step-marker').textContent = String(index + 1);
    step.querySelector('.step-status').textContent = 'Ready';
  });
  for (const step of steps) {
    step.classList.add('running');
    step.querySelector('.step-status').textContent = 'Running';
    if (!reducedMotion.matches) await new Promise((resolve) => setTimeout(resolve, 650));
    step.classList.replace('running', 'complete');
    step.querySelector('.step-marker').textContent = '✓';
    step.querySelector('.step-status').textContent = 'Done';
  }
  workflowStatus.textContent = 'Example complete. Saved name verified.';
  runButton.textContent = '↻  Replay example';
  runButton.disabled = false;
});
