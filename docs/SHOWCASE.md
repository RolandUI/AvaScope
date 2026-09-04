# AvaScope Showcase

The public feature showcase is hosted through GitHub Pages at:

https://rolandui.github.io/AvaScope/

Its static source lives under `website/`. The page uses repository-owned brand assets and concrete output from the AvaScope getting-started sample. It is intentionally presentation-only: it does not run AvaScope, load user projects, or expose a runtime bridge in the browser.

## Release-Aligned Publishing

`.github/workflows/pages.yml` deploys the site only for:

- a published GitHub Release; or
- an explicit `workflow_dispatch` used for the initial publication or deployment recovery.

Ordinary pushes and pull requests never update the public site. A release-triggered run checks out the released revision and writes `release.json` from the published release event before deploying the static artifact. A manual run uses the latest public GitHub Release metadata.

## Local Preview

Serve the static directory through any local HTTP server. For example:

```powershell
python -m http.server 4173 --directory website
```

Then open `http://127.0.0.1:4173/`. Opening `index.html` directly is not the supported preview path because browsers may block the `release.json` request for local files.

## Updating Showcase Content

Update `website/` alongside the product feature it describes. The committed source can advance on `master` without changing the live site; publication remains aligned to the next GitHub Release. Keep feature statements grounded in the capability catalog and refresh committed sample images when their represented UI or output contract changes.

The site must remain static, accessible, responsive, and free of external runtime dependencies. Do not add analytics, remote execution, credentials, or network-backed interactive demos without an explicit product and privacy decision.
