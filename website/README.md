# AvaScope showcase

Local, English-language design preview for the AvaScope capability website. This
first version covers a selection of capabilities. The browser demonstrations are
labelled illustrations, not a live AvaScope connection or recorded product evidence.

Open `index.html` directly in a browser, or serve this directory locally:

```sh
python -m http.server 4173 --bind 127.0.0.1 --directory website
```

The command above runs from the repository root. Open `http://127.0.0.1:4173`.
HTML, CSS, JavaScript, branding and the Manrope font are self-contained. No build,
package installation, external font request or backend is required. Documentation
and repository links require an internet connection.

The site can later be published as static files under a GitHub Pages project path;
all asset URLs are relative. No deployment workflow is configured. Publication is
pending explicit approval of the design.

## Local validation

Check desktop and narrow mobile layouts, keyboard focus, the element inspector,
theme selector, workflow replay, comparison slider and feature disclosures. Check
JavaScript syntax with `node --check website/script.js` from the repository root.
Product .NET builds are not applicable to this standalone static website.

Manrope is bundled under the SIL Open Font License; see `assets/OFL-Manrope.txt`.
