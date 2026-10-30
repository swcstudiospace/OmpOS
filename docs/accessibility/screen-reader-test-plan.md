---
description: "Screen reader test plan for the Autonogrammer web head: browser and assistive technology matrix, flows, expected announcements and an empty result table."
---

# Screen reader test plan

Status: Plan only. NOT YET RUN: no screen reader has been used on Autonogrammer, and the result table below is empty on purpose.

Last verified: 2026-10-29

Until the table has results, every statement about what a screen reader announces is an inference from the markup (roles, names, live regions) and must not be repeated as a tested claim.

## Matrix

| # | Screen reader | Browser | Platform | Priority |
| --- | --- | --- | --- | --- |
| 1 | NVDA (current release) | Firefox (current) | Windows 11 | High |
| 2 | NVDA | Chrome | Windows 11 | High |
| 3 | JAWS (current) | Chrome | Windows 11 | High |
| 4 | VoiceOver | Safari | macOS (current) | High |
| 5 | VoiceOver | Safari | iOS (current), Demo only | Medium |
| 6 | TalkBack | Chrome | Android (current), Demo only | Medium |
| 7 | Orca | Firefox | Linux (GNOME) | Medium |

Run the Demo bundle for all rows; run flow A on an owner-token deployment for rows 1 to 4. Record the exact versions of the screen reader, the browser and the OS in the result table. Use the screen reader's default settings first (browse or reading mode on), then repeat flows C and E in focus or forms mode.

## Flows and expected announcements

The expected announcements are what the markup supports. The wording will differ between screen readers; check the information, not the words.

### A. Connect (owner-token deployment)

- On load the page title is announced as "Connect - Autonogrammer" and the heading "Connect to Autonogrammer" (level 1) is read, because focus is placed on it.
- The field is announced as "Access token", edit, protected or password, required, with the help text as its description.
- An empty submit announces the error ("Enter the access token."), and the field is announced as invalid when focus is back in it.
- A rejected token announces the error; the field is empty.
- A successful connection announces the new page's title.

### B. Page and region structure

- Landmarks: banner, navigation "Agents", main, and content information (footer). The skip link is the first control and is announced as a link.
- The Agents and Kanban tabs are announced as a tab list named "Views", with the selected tab and its position ("1 of 2").
- Heading navigation reaches one level-1 heading per view (Agents, Kanban, About, the session-not-found heading).
- Moving between views by link, tab, Back or Forward announces the new title (a polite status message) and places focus on the view heading or the conversation.

### C. Conversation

- The conversation region is announced as "Session".
- A new message and the end of an answer are announced politely ("Response complete."); the composer "Message" is a multi-line edit field with its hint as description; the send and stop buttons have names.
- The slash command list announces its options and the count while it is open.
- A tool approval request is announced; the group "Tool approval: <tool>" has "Allow once: <tool>" and "Deny: <tool>" buttons; the decision result is announced.
- The Changes bar, the per-file buttons, the review panel with "Accept hunk n" and "Reject hunk n" (pressed or not pressed) and the apply result are announced.

### D. Agent list and Kanban

- The list is announced with its group headings and each agent's name, time and status as one phrase; the selected agent is marked as current.
- The Kanban board announces its columns; each card announces its spoken name and position; Steer, Move to backlog and Archive buttons name the card.

### E. Settings dialog

- Opening announces a dialog named "Settings" (modal); focus is inside; the section list is a navigation named with the settings navigation name and the current section is announced as the current page.
- Escape with unsaved changes announces the alert dialog with its title and body; closing returns focus to the opener, announced as the opener's name.

### F. Errors and status

- The error banner ("An unhandled error has occurred") is announced as an alert and can be dismissed from the keyboard.
- Loading states are announced once (status), not repeatedly.

## What to record for each flow

1. Whether every control was reachable and had a name that matches what is on screen.
2. Whether each expected announcement was made, was missing, was duplicated, or was too late or too early.
3. Whether focus went where the flow says it should.
4. Anything announced that is wrong, confusing or noisy (for example the same text read twice by a live region and by focus).

## Result table (NOT YET RUN)

| Date | Tester | Screen reader and version | Browser and version | OS | Flow | Result | Defects and notes |
| --- | --- | --- | --- | --- | --- | --- | --- |
| | | | | | | | |

## How verified

- The flows and the expected announcements were derived on 2026-10-29 from the sources: `AppShell.razor`, `ViewTabs.razor` (tablist named "Views"), `RouteAnnouncerRegion.razor`, `ConnectView.razor`, `AuthCopy.cs`, `Composer.razor`, `ThreadView.razor` (live regions), `ToolApprovalRow.razor`, `ChangesBar.razor`, `ChangeReviewPanel.razor`, `AgentRail.razor`, `KanbanCardView.razor`, `SettingsDialog.razor`.
- No screen reader, browser or device was used to write this plan. The matrix names the current release of each product at the time of the test, not a version that was verified.
