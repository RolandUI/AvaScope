'use strict';

// Scripted, local illustrations. No AvaScope connection or user data is involved.
const scenes = {
  inspect: {
    title: 'A button to you.\nA clear target to your agent.',
    description:
      'AvaScope returns the interface as a tree of elements, together with a screenshot. Your agent can identify a control and see where it belongs.',
    prompt: 'Find the Save changes button.',
    intent: 'I’ll read the interface and capture the current view.',
    tools: [
      [
        'observe',
        'Read the UI tree and capture a screenshot',
        'Target: the settings window. Requested evidence: element tree, interaction states and screenshot.',
      ],
    ],
    evidence: `<div class="inspection-evidence"><div class="tree-evidence"><span class="artifact-title">UI tree</span><ul class="element-tree"><li>Window <span>Northstar</span><ul><li>Panel <span>Settings</span><ul><li>TextBox</li><li>ToggleSwitch</li><li class="tree-selected">Button <strong>SaveButton</strong></li></ul></li></ul></li></ul><div class="element-state">Visible <span>✓</span> Enabled <span>✓</span></div></div><figure class="image-evidence"><figcaption>Screenshot</figcaption><img src="./assets/northstar-light.png" alt="Screenshot of the same illustrated Northstar settings window" width="520" height="362" /></figure></div>`,
    answer: 'Found it: SaveButton. It’s visible, enabled and ready to use.',
    observation: 'The highlighted button matches the element in the agent’s tree.',
    docs: 'USER_GUIDE.md#runtime-inspection',
    docLabel: 'Read about inspection',
  },
  preview: {
    title: 'Try another theme.\nSee the actual layout.',
    description:
      'Your agent can ask Avalonia to render a view in a different theme or size. The returned image shows the result before you change the running app.',
    prompt: 'Show me this settings view in dark mode.',
    intent: 'I’ll render the same view with Avalonia’s dark theme.',
    tools: [
      [
        'preview_axaml',
        'Render the view with the dark theme',
        'View: Settings. Theme: dark. The preview is rendered in an isolated Avalonia process.',
      ],
    ],
    evidence: `<div class="preview-evidence"><figure class="image-evidence"><figcaption>Light reference</figcaption><img src="./assets/northstar-light.png" alt="Illustrated settings view in the light theme" width="520" height="362" /></figure><figure class="image-evidence"><figcaption>Dark preview</figcaption><img src="./assets/northstar-dark.png" alt="The same illustrated settings view in the dark theme" width="520" height="362" /></figure></div>`,
    answer: 'Here’s the dark preview of the same settings view.',
    observation: 'The rendered preview is also returned to the agent as an image.',
    docs: 'USER_GUIDE.md#preview-sessions',
    docLabel: 'Read about previews',
  },
  automate: {
    title: 'Ask for a change.\nFollow it through to the result.',
    description:
      'Your agent can find a form, fill its fields and save with an explicit action. AvaScope provides the state it needs to check what happened.',
    prompt: 'Rename this workspace to Northstar and save it.',
    intent: 'I’ll find the name field, update it, then save and check the result.',
    tools: [
      [
        'inspect_form',
        'Find the workspace name field',
        'Read the supported fields in the settings form.',
      ],
      [
        'fill_form',
        'Set the name to Northstar',
        'Set the desired value of WorkspaceName. Filling the form does not submit it.',
      ],
      [
        'input',
        'Press Save changes',
        'Invoke the identified save button as a separate, explicit action.',
      ],
      [
        'observe',
        'Check the saved state',
        'Read the resulting name and the application’s save confirmation.',
      ],
    ],
    evidence: `<div class="verified-evidence"><div><span>Workspace name</span><strong>Northstar</strong></div><div><span>Save confirmation</span><strong>Changes saved <span aria-hidden="true">✓</span></strong></div></div>`,
    answer: 'Saved. The workspace is now Northstar, and the app confirmed the change.',
    observation: 'The field changed and the app confirmed the save.',
    docs: 'FORM_WORKFLOWS.md',
    docLabel: 'Read about form workflows',
  },
  compare: {
    title: 'Something moved.\nYour agent can see where.',
    description:
      'Compare the current interface with a saved image. AvaScope points out visual differences so your agent can decide what needs a closer look.',
    prompt: 'Has anything changed on the settings screen?',
    intent: 'I’ll compare the current screenshot with the saved reference.',
    tools: [
      [
        'semantic_diff',
        'Compare the two screenshots',
        'Compare a current screenshot with a saved reference. Return changed regions and visual-difference findings.',
      ],
    ],
    evidence: `<div class="preview-evidence"><figure class="image-evidence"><figcaption>Saved reference</figcaption><img src="./assets/northstar-light.png" alt="Reference settings window with the original button position" width="520" height="362" /></figure><figure class="image-evidence"><figcaption>Current screenshot</figcaption><img src="./assets/northstar-changed.png" alt="Changed settings window with the save button shifted downward" width="520" height="362" /></figure></div><div class="difference-finding"><span aria-hidden="true">↧</span><span>Save button moved down <strong>18 px</strong></span></div>`,
    answer: 'The Save changes button moved down. The extra gap is highlighted in the app view.',
    observation: 'The outline marks the changed area. The dashed line shows its original position.',
    docs: 'VISUAL_REGRESSION_CI.md',
    docLabel: 'Read about visual checks',
  },
};

let currentScene = 'inspect';
let currentPhase = 2;
let playTimer;
const reducedMotion = window.matchMedia('(prefers-reduced-motion: reduce)');
const playButton = document.querySelector('#play-story');
const pair = document.querySelector('.demo-pair');
const appWindow = document.querySelector('.app-window');

function renderScene() {
  const scene = scenes[currentScene];
  document
    .querySelectorAll('button[data-scene]')
    .forEach((button) =>
      button.setAttribute('aria-pressed', String(button.dataset.scene === currentScene)),
    );
  document
    .querySelectorAll('button[data-phase]')
    .forEach((button) =>
      button.setAttribute('aria-pressed', String(Number(button.dataset.phase) === currentPhase)),
    );
  pair.dataset.scene = currentScene;
  pair.dataset.phase = String(currentPhase);
  document.querySelector('#scene-title').textContent = scene.title;
  document.querySelector('#scene-description').textContent = scene.description;
  document.querySelector('#user-prompt').textContent = scene.prompt;
  document.querySelector('#agent-intent').textContent = scene.intent;
  document.querySelector('#agent-work').hidden = currentPhase === 0;
  document.querySelector('#returned-evidence').hidden = currentPhase < 2;
  document.querySelector('#agent-answer').hidden = currentPhase < 2;
  document.querySelector('#conversation-wait').hidden = currentPhase === 2;
  document.querySelector('#conversation-wait').textContent =
    currentPhase === 0
      ? 'Next: the agent calls AvaScope.'
      : 'Next: see the information returned to the agent.';
  document.querySelector('#tool-calls').innerHTML = scene.tools
    .map(
      ([name, label, detail]) =>
        `<details class="tool-call"><summary><span class="tool-icon" aria-hidden="true">◎</span><span><span class="tool-identity">AvaScope <code>${name}</code></span><span class="tool-description">${label}</span></span><span class="tool-check" aria-hidden="true">${currentPhase === 2 ? '✓' : '↗'}</span></summary><p>${detail}</p></details>`,
    )
    .join('');
  document.querySelector('#evidence-content').innerHTML = scene.evidence;
  document.querySelector('#answer-text').textContent = scene.answer;
  document.querySelector('#app-observation').textContent =
    currentPhase === 2 ? scene.observation : 'The example app, before the agent has the result.';
  document.querySelector('#app-name').textContent =
    currentScene === 'automate' && currentPhase < 2 ? 'Untitled workspace' : 'Northstar';
  document.querySelector('#app-saved').hidden = !(
    currentScene === 'automate' && currentPhase === 2
  );
  const previewIsDark = currentScene === 'preview' && currentPhase === 2;
  appWindow.classList.toggle('dark-preview', previewIsDark);
  document.querySelector('#app-context').textContent = previewIsDark
    ? 'Dark preview · illustration'
    : 'Avalonia app · illustration';
  document.querySelector('#app-pane-title').textContent =
    currentScene === 'preview' ? 'Your view' : 'Your app';
  document.querySelector('#app-pane-subtitle').textContent =
    currentScene === 'preview'
      ? 'The same view, rendered in another theme'
      : 'What you see on screen';
  appWindow.setAttribute(
    'aria-label',
    `Illustrated Northstar settings window. ${currentPhase === 2 ? scene.observation : 'Workspace name: ' + document.querySelector('#app-name').textContent + '.'}`,
  );
  const docsLink = document.querySelector('#scene-docs');
  docsLink.href = `https://github.com/RolandUI/AvaScope/blob/master/docs/${scene.docs}`;
  docsLink.textContent = `${scene.docLabel} ↗`;
  document.querySelector('#story-status').textContent =
    `${scene.prompt} Step ${currentPhase + 1} of 3: ${['the request', 'AvaScope tool calls', 'the result'][currentPhase]}.`;
}

function stopPlayback() {
  clearTimeout(playTimer);
  playTimer = undefined;
  playButton.innerHTML = '<span aria-hidden="true">↻</span> Replay example';
}

document.querySelectorAll('button[data-scene]').forEach((button) => {
  button.addEventListener('click', () => {
    stopPlayback();
    currentScene = button.dataset.scene;
    currentPhase = 2;
    renderScene();
  });
});

document.querySelectorAll('button[data-phase]').forEach((button) => {
  button.addEventListener('click', () => {
    stopPlayback();
    currentPhase = Number(button.dataset.phase);
    renderScene();
  });
});

playButton.addEventListener('click', () => {
  if (playTimer !== undefined) {
    stopPlayback();
    return;
  }
  if (reducedMotion.matches) {
    currentPhase = 2;
    renderScene();
    return;
  }
  currentPhase = 0;
  renderScene();
  playButton.innerHTML = '<span aria-hidden="true">Ⅱ</span> Pause example';
  const advance = () => {
    currentPhase += 1;
    renderScene();
    if (currentPhase < 2) playTimer = setTimeout(advance, 1800);
    else stopPlayback();
  };
  playTimer = setTimeout(advance, 1400);
});

renderScene();
