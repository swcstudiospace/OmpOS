---
description: "Step-by-step keyboard-only test script for the critical flows of the Autonogrammer web head: connect, new conversation, send, approve a tool, review changes, settings, kanban and sign out."
---

# Manual keyboard test script

Status: Written, not yet carried out by a person. The result table at the end is empty.

Last verified: 2026-10-29

Purpose: confirm by hand what the scripted checks (`scripts/web-smoke/a11y`) can only approximate, with the mouse unplugged or untouched. Allow about 45 minutes. Record every deviation from an expected result, including the ones that look small.

## Setup

- A current desktop browser (the script was written for Chrome; repeat in Firefox and Safari when available). Do not use the mouse after step 1 of a flow unless the step says so.
- The web head against a running API (owner-token deployment for flows 1 and 8) or the Demo (flows 2 to 7; flows 4 and 5 need the Demo's "approval" and "completed with changes" sessions, found in the Agents list).
- Browser zoom 100 percent, window at least 1280 px wide. Repeat flows 2, 3, 6 and 7 at 320 px wide (or 400 percent zoom) to cover the drawer.
- Keys: Tab and Shift+Tab move between controls; Enter and Space activate buttons; Enter activates links; arrow keys move inside a tab list, a list or a board; Escape closes a dialog, a drawer or a palette. The app's shortcuts are Ctrl+Shift+A (Agents), Ctrl+Shift+K (Kanban) and Ctrl+Comma (Settings); on macOS read Ctrl as Command.

## Rules for every flow

1. Focus is always visible, and never hidden behind a bar or a dialog.
2. Focus never gets stuck: Tab and Shift+Tab always lead somewhere, and Escape or a named button leaves any popup.
3. After an action the focus is somewhere sensible (the new content, the opener, the next control), never lost to the top of the page.
4. Every control has a name you could say aloud, and that name contains the text you see on it.
5. Nothing needs a mouse, a drag or a hover.

## Flow 1: connect (owner-token deployment)

1. Open the address of the deployment in a new tab with no token stored.
2. Expected: the address becomes `/connect`; the tab title is "Connect - Autonogrammer"; focus is on the heading "Connect to Autonogrammer".
3. Press Tab. Expected: focus goes to the "Access token" field (a password field with a visible label and a help sentence), then Tab again to the "Connect" button; Shift+Tab goes back.
4. Focus the field and press Enter with the field empty. Expected: the message "Enter the access token." appears; the field is marked invalid; focus stays in the field.
5. Type a wrong token and press Enter. Expected: "Checking the token…" while it waits, then "That token was not accepted. Check it and enter it again."; the field is empty again and has focus.
6. Type the real token and press Enter. Expected: you arrive at the page you asked for (or the Agents view); the tab title changes; focus is on that page's heading.

## Flow 2: new conversation

1. On the Agents view press Tab from the top of the page. Expected: the first stop is the skip link "Skip to main content" (or "Skip to conversation" while a conversation is open), not the heading. If the first stop is anything else, record what it was.
2. Press Enter on the skip link. Expected: focus moves to the main content or the conversation.
3. Tab to the button "New conversation" (in the agent list) and press Enter. Expected: a new conversation opens; the tab title and address change; focus moves to the conversation, and one Tab leads to the "Message" box.
4. Tab to the "Filter agents" field and type a letter. Expected: the list narrows. Record whether the change in the number of agents is announced, and what Escape does in the field.
5. Use the arrow keys in the agent list. Expected: one Tab stop for the whole list; Up and Down move between agents; Enter opens one.

## Flow 3: send a message

1. In "Message" type a line, press Shift+Enter, type a second line. Expected: a new line, nothing sent.
2. Press Enter. Expected: the message is sent; focus stays in the box; while the answer streams a status such as "Response complete." appears at the end (visible and in a live region).
3. Press Escape while an answer streams. Expected: the response stops.
4. Type `/`. Expected: a list of commands opens under the box; Up and Down move through it; Enter chooses; Escape closes it and leaves focus in the box. Record what Tab does while the list is open (known issue K2).
5. Tab to "Send message" or "Stop response" and activate it with Enter and with Space. Expected: both work.

## Flow 4: approve a tool

1. Open the Demo agent whose status says it needs approval. Expected: a group named "Tool approval: bash" with two buttons, "Allow once: bash" and "Deny: bash"; the arrival of the request is announced.
2. Tab to "Deny: bash" and press Enter. Expected: the group's status text changes ("Denied" or similar); the buttons are disabled or gone; focus is not lost to the top of the page.
3. Repeat with "Allow once: bash" on a fresh approval. Expected: the same, with the allowed outcome.

## Flow 5: review changes

1. Open the Demo agent that completed with changes. Expected: a "Changes" section with "Keep all" and "Undo all" and one row per file with Keep, Undo and Review buttons, each named with the file.
2. Tab to a file's "Review" button and press Enter. Expected: a review panel opens, named by its heading; each hunk has "Accept hunk n" and "Reject hunk n" toggle buttons that report pressed or not pressed.
3. Toggle a hunk and activate the apply button. Expected: the result is announced and shown; focus stays inside the panel or returns to the row.
4. Close the panel with "Close review". Expected: focus returns to the "Review" button that opened it. Record whether Escape also closes it.

## Flow 6: settings

1. Press Ctrl+Comma (or Tab to "Settings" in the agent list and press Enter). Expected: a dialog named "Settings" opens; the address becomes `/settings`; the title changes; focus is inside the dialog.
2. Press Tab many times, then Shift+Tab many times. Expected: focus cycles inside the dialog only (section buttons, the page's controls, the close button); the page behind is not reachable.
3. Move to another section with Enter on its button. Expected: the content changes; the chosen section is marked as current; focus does not jump out.
4. Change a value, then press Escape. Expected: a prompt asks whether to save, discard or keep editing, with focus inside it; "Keep editing" returns to the page, "Discard" closes.
5. Close the dialog (Escape with nothing changed). Expected: it closes; focus returns to the control that opened it.

## Flow 7: kanban

1. Press Ctrl+Shift+K or Tab to the "Kanban" tab and press Enter. Expected: the board shows; the tab title is "Kanban - Autonogrammer"; the Kanban tab stays focused (arrow keys move between the Agents and Kanban tabs).
2. Tab into the board. Expected: one Tab stop for the board; the arrow keys move between cards; Enter opens the card's agent, the address becomes `/sessions/...`, and focus moves to the conversation.
3. On a card, reach its Steer, Move to backlog and Archive buttons. Expected: each has a name that includes the card's title and Enter activates it. Record where focus goes when the steering box opens and whether Escape closes it.
4. Press Ctrl+Shift+A. Expected: back on the Agents view with the Agents tab focused.

## Flow 8: sign out (owner-token deployment)

1. Tab to "Sign out" in the top bar and press Enter. Expected: the connect screen opens with its heading focused. Record whether the "Your session ended" message shows.
2. Press the browser's Back button. Expected: the workbench does not show; you are sent back to connect.
3. Look at the token field. Expected: empty. The token is not kept anywhere you can recover it by reload.

## Result record

Fill one row per flow and run. Leave rows empty until a person has done the flow.

| Date | Tester | Browser and version | OS | Flow | Pass, Fail or Partial | Deviations and notes |
| --- | --- | --- | --- | --- | --- | --- |
| | | | | | | |

## How verified

- Every control name, key and message in this script was copied from the Razor and C# sources on 2026-10-29: `AuthCopy.cs` (connect messages), `AppShell.razor` and `WorkbenchView.razor` (skip link, headings), `ViewTabs.razor`, `AgentRail.razor`, `Composer.razor`, `ToolApprovalRow.razor`, `ChangesBar.razor`, `ChangeReviewPanel.razor`, `SettingsDialog.razor`, `KanbanCardView.razor`, `autonogrammer-keys.js` (shortcuts).
- The script itself has not been run by anyone. Where the source does not settle the expected result (Escape in the review panel, the keys on a Kanban card, the exact status text after a decision) the step says "record" instead of stating an outcome.
