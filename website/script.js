const featureMenu = document.querySelector('.feature-menu');
const menuToggle = featureMenu.querySelector('summary');

featureMenu.addEventListener('click', (event) => {
  const link = event.target.closest('a[href^="#"]');
  if (!link) return;

  featureMenu.open = false;
  const section = document.querySelector(link.hash);
  if (section) {
    section.tabIndex = -1;
    section.focus({ preventScroll: true });
    section.addEventListener('blur', () => section.removeAttribute('tabindex'), { once: true });
  }
});

featureMenu.addEventListener('focusout', (event) => {
  if (!featureMenu.contains(event.relatedTarget)) featureMenu.open = false;
});

document.addEventListener('click', (event) => {
  if (featureMenu.open && !featureMenu.contains(event.target)) {
    featureMenu.open = false;
  }
});

document.addEventListener('keydown', (event) => {
  if (event.key === 'Escape' && featureMenu.open) {
    featureMenu.open = false;
    menuToggle.focus();
  }
});
