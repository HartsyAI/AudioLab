# O10 browser checks (throwaway evidence)

Evidence for D14 in DESIGN.md, 7.5. Not part of the solution: no project, package manifest or dependency lives here, and the
conventions tests do not scan this folder.

- `run.js`: Playwright script. Per browser it runs the keyboard and focus checks and axe-core on ten states of the proof page.
- `report.js`: turns the three JSON files into `results.md`.
- `anchor.html`: a native popover placed with CSS anchor positioning, copied next to the published proof page.
- `results.md`: the per-check pass/fail table for Chromium, WebKit and Firefox, the axe table and the observations.
- `chromium.json`, `webkit.json`, `firefox.json`: raw results, including every axe violation with its element and message.

Run, from an empty scratch folder (nothing here is installed by the repository):

1. `npm i playwright@1.65.1 axe-core@4.14.1`, then `npx playwright install chromium firefox webkit`.
2. `dotnet publish src/AudioLab.Web -c Release -o <out>`, copy `anchor.html` into `<out>/wwwroot`, serve `<out>/wwwroot` on
   `http://127.0.0.1:8765/` (`python -m http.server 8765 --bind 127.0.0.1`).
3. Copy `run.js` and `report.js` next to `node_modules`, then `ONLY=chromium OUT=chromium.json node run.js` (likewise `webkit`,
   `firefox`), then `node report.js`.

Firefox note: on the machine used, Playwright's Firefox 159 would not start from the default browser folder under the user
profile (Windows side-by-side error for `mozglue`). Copied to `C:\pwtest` and run with `PLAYWRIGHT_BROWSERS_PATH=C:\pwtest` it
started and all checks ran.
