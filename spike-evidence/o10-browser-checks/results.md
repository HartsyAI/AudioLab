# O10 proof page: keyboard, focus and axe-core results

Machine: Windows_NT 10.0.26200, Intel(R) Core(TM) i7-9700K CPU @ 3.60GHz, 8 cores, 40 GB. Node v26.13.0, Playwright 1.65.1, axe-core 4.14.1.
Browsers: chromium 159.0.8154.6, webkit 27.2, firefox 159.0.

| Check | chromium | webkit | firefox |
|---|---|---|---|
| native dialog: opened with the keyboard, opens modal and moves focus inside | pass | pass | pass |
| native dialog: Tab and Shift+Tab never reach the page behind it (10 presses each way) | pass | pass | pass |
| native dialog: Escape closes it and focus returns to the opener | pass | pass | pass |
| native dialog: opened with a mouse click, Escape returns focus to the opener | pass | FAIL | pass |
| native dialog: confirm runs the action and closes | pass | pass | pass |
| library dialog: opens, role=dialog/alertdialog, focus inside | pass | pass | pass |
| library dialog: Tab stays inside (10 presses) | pass | pass | pass |
| library dialog: Escape closes it and focus returns to the opener | pass | pass | pass |
| menu: Enter opens, role=menu, focus on the first item | pass | pass | pass |
| menu: ArrowDown/ArrowUp move the active item, Home/End jump | pass | pass | pass |
| menu: typeahead moves to a matching item | pass | pass | pass |
| menu: Escape closes and focus returns to the button | pass | pass | pass |
| menu: Enter on an item runs it and closes | pass | pass | pass |
| context menu: right-click opens, ArrowDown + Enter runs an item | pass | pass | pass |
| context menu: Shift+F10 on the focused area opens it (synthetic key, informational) | pass | FAIL | FAIL |
| select: Enter opens a listbox, ArrowDown + Enter selects, value shown | pass | pass | pass |
| select: Escape closes the list and focus returns to the button | pass | pass | pass |
| combobox: typing filters, ArrowDown + Enter selects | pass | pass | pass |
| combobox: Escape closes the list | pass | pass | pass |
| popover: Enter opens, Escape closes, focus returns to the button | pass | pass | pass |
| popover: click outside closes it | pass | pass | pass |
| popover (library): the panel opens below its button, start-aligned | pass | pass | pass |
| popover (native, CSS anchor positioning): placed below its button, start-aligned | pass | pass | pass |
| tooltip (trigger is the button): keyboard focus shows it, Escape hides it | pass | pass | pass |
| tooltip (trigger is the button): hover shows it | pass | pass | pass |
| tooltip (span wrapped around a button): keyboard focus shows it | FAIL | FAIL | FAIL |
| tooltip (span wrapped around a button): hover shows it | pass | pass | pass |
| toast: appears, announced (role status/alert or live region), F8 reaches it, Undo runs | pass | pass | pass |
| modal: select inside a native dialog opens, picks, and Escape closes the list before the dialog | pass | pass | pass |
| modal: combobox inside a native dialog, Escape closes the list before the dialog | pass | pass | pass |
| modal: popover inside a native dialog, Escape closes it before the dialog | pass | pass | pass |
| modal: axe state - native form dialog with a select list expanded | pass | pass | pass |
| modal: a toast raised from inside a dialog (observation: is it clickable?) | pass | pass | pass |
| tabs: ArrowRight moves to the next tab and shows its panel | pass | pass | pass |

## Failures

- chromium: tooltip (span wrapped around a button): keyboard focus shows it: focus on button.al-button "Play"; tooltip not shown
- webkit: native dialog: opened with a mouse click, Escape returns focus to the opener: focus is on body "Primitives proof "
- webkit: context menu: Shift+F10 on the focused area opens it (synthetic key, informational): did not open (synthetic key presses do not raise contextmenu in every engine)
- webkit: tooltip (span wrapped around a button): keyboard focus shows it: focus on button.al-button "Play"; tooltip not shown
- firefox: context menu: Shift+F10 on the focused area opens it (synthetic key, informational): did not open (synthetic key presses do not raise contextmenu in every engine)
- firefox: tooltip (span wrapped around a button): keyboard focus shows it: focus on button.al-button "Play"; tooltip not shown

## axe-core (WCAG 2.0 to 2.2 A and AA)

| State | chromium | webkit | firefox |
|---|---|---|---|
| page at rest | 0 violation(s), 0 incomplete | 0 violation(s), 0 incomplete | 0 violation(s), 0 incomplete |
| native dialog open | 0 violation(s), 0 incomplete | 0 violation(s), 0 incomplete | 0 violation(s), 0 incomplete |
| library dialog open | 0 violation(s), 0 incomplete | 0 violation(s), 0 incomplete | 0 violation(s), 0 incomplete |
| menu open | 1 violation(s) (color-contrast x1), 0 incomplete | 1 violation(s) (color-contrast x1), 0 incomplete | 1 violation(s) (color-contrast x1), 0 incomplete |
| select open | 0 violation(s), 0 incomplete | 0 violation(s), 0 incomplete | 0 violation(s), 0 incomplete |
| combobox open | 0 violation(s), 0 incomplete | 0 violation(s), 0 incomplete | 0 violation(s), 0 incomplete |
| popover open | 0 violation(s), 0 incomplete | 0 violation(s), 0 incomplete | 0 violation(s), 0 incomplete |
| tooltip visible | 0 violation(s), 0 incomplete | 0 violation(s), 0 incomplete | 0 violation(s), 0 incomplete |
| toast visible | 0 violation(s), 1 incomplete | 0 violation(s), 1 incomplete | 0 violation(s), 1 incomplete |
| native form dialog with select expanded | 0 violation(s), 1 incomplete | 0 violation(s), 1 incomplete | 0 violation(s), 1 incomplete |

## Observations

- chromium: native dialog: focus passed through the document body (the browser UI) 6 time(s) in 20 presses; ends in
- chromium: library popover offset from its button (px): {"dTop":0,"dLeft":0}
- chromium: anchor positioning: supports {"anchorName":true,"positionArea":true}; native popover offset from its anchor (px): {"open":true,"dTop":0,"dLeft":0}
- chromium: toast raised from inside a modal dialog: shown=true, receives pointer hits=false (topmost element at its centre: dialog.al-native-dialog)
- webkit: native dialog: focus passed through the document body (the browser UI) 5 time(s) in 20 presses; ends in
- webkit: library popover offset from its button (px): {"dTop":0,"dLeft":0}
- webkit: anchor positioning: supports {"anchorName":true,"positionArea":true}; native popover offset from its anchor (px): {"open":true,"dTop":0,"dLeft":0}
- webkit: toast raised from inside a modal dialog: shown=true, receives pointer hits=false (topmost element at its centre: dialog.al-native-dialog)
- firefox: native dialog: focus passed through the document body (the browser UI) 4 time(s) in 20 presses; ends in
- firefox: library popover offset from its button (px): {"dTop":0,"dLeft":0}
- firefox: anchor positioning: supports {"anchorName":true,"positionArea":true}; native popover offset from its anchor (px): {"open":true,"dTop":0,"dLeft":0}
- firefox: toast raised from inside a modal dialog: shown=true, receives pointer hits=false (topmost element at its centre: dialog.al-native-dialog)
