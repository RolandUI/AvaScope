# AvaScope showcase

English-language AvaScope capability website, published on
[GitHub Pages](https://rolandui.github.io/AvaScope/).
This version presents twelve main capabilities in separate sections on one scrolling
page, followed by twenty-one smaller illustrated capabilities in a two-column layout.
Seeing and controlling the app come first, followed by complete checked workflows.
Each section explains what AvaScope enables the agent to do. A compact navigation menu
links directly to each section and can accommodate more features without changing
the page structure.
The hero lists Windows, macOS and Linux with small monochrome icons.
MCP, CLI and .NET API access is presented in the integration example below.

The default theme is dark. Light, rounded illustration surfaces distinguish the
examples from the website. Each example explains one idea with a static app
fragment, element tree, theme pair or comparison; there are no playback controls
or hidden feature panels. Smaller examples keep each illustration, title and
description together, with more space between separate capabilities.
All feature content is available without JavaScript.
JavaScript closes the navigation menu on selection, outside click or Escape and
places keyboard focus at the selected section.

The fictional Northstar UI is a simplified HTML/CSS illustration, not a live
AvaScope connection or recorded product evidence. The retained `northstar-*.png`
assets from the earlier design are captures of a fictional HTML application,
not Avalonia renders or results from an actual agent run; this version does not
use those images.

## Content coverage

Copy is checked against the registered tools and their implementations, not only
the usage documentation. The `data-capabilities` attributes map sections to all
84 IDs currently declared in the protocol. Related capabilities share an example
instead of repeating a card for each tool or technical contract.

Use these sources when updating content:

- [Capability IDs](../src/AvaScope.Protocol/AvaScopeCapabilityIds.cs) and
  [catalog](../src/AvaScope.Protocol/AvaScopeCapabilityCatalog.cs).
- [MCP tools](../src/AvaScope.Mcp/AvaScopeMcpTools.cs),
  [runtime bridge](../src/AvaScope.Bridge) and [core workflows](../src/AvaScope.Core).
- [Preview host](../src/AvaScope.PreviewHost) and [contract tests](../tests).

Keep platform and host-integration requirements explicit. Source coverage does
not certify that every capability has been exercised in a live application.

## Local preview

Serve the directory locally from the repository root:

```sh
python -m http.server 4173 --bind 127.0.0.1 --directory website
```

Open `http://127.0.0.1:4173`. HTML, CSS, JavaScript, branding and the Manrope font
are self-contained. No build, package installation, external font request or
backend is required. Documentation and repository links require internet access.

## Deployment

[The Pages workflow](../.github/workflows/pages.yml) publishes the `website` directory
when its files or the workflow change on `master`. It also supports manual dispatch
from `master`. The repository's Pages source is **GitHub Actions**.

All asset URLs are relative, so the site works under the `/AvaScope/` project path.
The deployment uses the `github-pages` environment and does not build or release
the AvaScope product.

## Local validation

Check desktop and narrow mobile layouts, anchor navigation, menu dismissal,
keyboard focus, local assets and reduced-motion behavior. Check JavaScript syntax
with `node --check website/script.js`, then run `git diff --check` from the
repository root. Product .NET builds are not applicable to this static website.

Manrope is bundled under the SIL Open Font License; see `assets/OFL-Manrope.txt`.
The inline Apple and Linux icons come from [Simple Icons 16.3.0](https://github.com/simple-icons/simple-icons/tree/16.3.0),
distributed under [CC0](https://github.com/simple-icons/simple-icons/blob/16.3.0/LICENSE.md).
