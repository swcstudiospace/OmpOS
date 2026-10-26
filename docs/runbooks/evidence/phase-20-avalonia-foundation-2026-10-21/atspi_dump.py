#!/usr/bin/env python3
"""AT-SPI2 query for the OmpOS native head (OmpOS.Native, Avalonia 12.1.4).

Waits up to --timeout seconds for an application named OmpOS (or an application with a
top-level element named OmpOS) on the AT-SPI2 desktop, then prints plain text:
  1. every application on the desktop;
  2. the controls of Phase 20 criterion 4: the prompt input, the send control and the
     transcript (required), and the extra controls the runbook asks to record. Every node
     whose accessible name matches exactly is printed with its role and tree path;
  3. the complete tree of the OmpOS application, one node per line, two spaces per level.
If no OmpOS application appears, every application's tree is printed instead and the
script exits 1. It exits 0 only when each required control resolves to exactly one node.
"""

import argparse
import sys
import time

import gi

gi.require_version('Atspi', '2.0')
from gi.repository import Atspi  # noqa: E402

APP_NAME = 'OmpOS'
MAX_DEPTH = 40

# (label, exact accessible name, required)
CONTROLS = [
    ('prompt input', 'Message', True),
    ('send control', 'Send message', True),
    ('transcript', 'Session transcript', True),
    ('conversations', 'Conversations', False),
    ('stop response', 'Stop response', False),
    ('session status', 'Session status', False),
]
WANTED_NAMES = {name for _, name, _ in CONTROLS}


def name_of(node):
    try:
        return node.get_name() or ''
    except Exception as error:  # report, do not hide
        return '<error ' + str(error) + '>'


def role_of(node):
    try:
        return node.get_role_name() or ''
    except Exception as error:  # report, do not hide
        return '<error ' + str(error) + '>'


def children_of(node):
    try:
        count = node.get_child_count()
    except Exception:
        return []
    children = []
    for index in range(count):
        try:
            child = node.get_child_at_index(index)
        except Exception:
            child = None
        if child is not None:
            children.append(child)
    return children


def walk(node, depth, path, lines, matches):
    if depth > MAX_DEPTH:
        lines.append('  ' * depth + '<depth limit ' + str(MAX_DEPTH) + '>')
        return
    role = role_of(node)
    name = name_of(node)
    lines.append('  ' * depth + role + ' | ' + name)
    if name in WANTED_NAMES:
        matches.setdefault(name, []).append((role, path))
    try:
        count = node.get_child_count()
    except Exception as error:  # report, do not hide
        lines.append('  ' * (depth + 1) + '<error ' + str(error) + '>')
        return
    for index in range(count):
        try:
            child = node.get_child_at_index(index)
        except Exception as error:  # report, do not hide
            lines.append('  ' * (depth + 1) + '<error child ' + str(index) + ' ' + str(error) + '>')
            continue
        if child is not None:
            walk(child, depth + 1, path + '.' + str(index), lines, matches)


def desktop_apps():
    desktop = Atspi.get_desktop(0)
    apps = []
    for index in range(desktop.get_child_count()):
        app = desktop.get_child_at_index(index)
        if app is not None:
            apps.append((index, app))
    return apps


def is_ompos(app):
    if APP_NAME in name_of(app):
        return True
    return any(name_of(child) == APP_NAME for child in children_of(app))


def wait_for_ompos(timeout):
    deadline = time.monotonic() + timeout
    while True:
        found = [(index, app) for index, app in desktop_apps() if is_ompos(app)]
        if found or time.monotonic() >= deadline:
            return found
        time.sleep(1.0)


def main():
    sys.stdout.reconfigure(encoding='utf-8')
    parser = argparse.ArgumentParser(description='Query the AT-SPI2 tree of the OmpOS native head.')
    parser.add_argument('--timeout', type=float, default=90.0,
                        help='seconds to wait for the OmpOS application to appear')
    args = parser.parse_args()

    found = wait_for_ompos(args.timeout)
    apps = desktop_apps()
    lines = ['desktop applications: ' + str(len(apps))]
    lines += ['APP ' + name_of(app) for _, app in apps]

    if not found:
        lines.append("application 'OmpOS' not found on the AT-SPI2 desktop after "
                     + str(args.timeout) + ' s; observed trees follow')
        for index, app in apps:
            tree = []
            walk(app, 1, str(index), tree, {})
            lines.append('== tree: application ' + repr(name_of(app)) + ' (desktop index ' + str(index) + ') ==')
            lines += tree
        print('\n'.join(lines))
        return 1

    matches = {}
    trees = []
    for index, app in found:
        tree = []
        walk(app, 1, str(index), tree, matches)
        trees.append('== tree: application ' + repr(name_of(app)) + ' (desktop index ' + str(index) + ') ==')
        trees += tree

    ok = True
    lines.append('== controls: exact accessible name, role and tree path ==')
    for label, wanted, required in CONTROLS:
        hits = matches.get(wanted, [])
        if len(hits) == 1:
            role, path = hits[0]
            lines.append(label + ': name=' + repr(wanted) + ' role=' + repr(role) + ' path=' + path)
            continue
        status = 'NOT FOUND' if not hits else 'AMBIGUOUS count=' + str(len(hits))
        lines.append(label + ': name=' + repr(wanted) + ' ' + status)
        for role, path in hits:
            lines.append('  candidate role=' + repr(role) + ' path=' + path)
        if required:
            ok = False
    print('\n'.join(lines + trees))
    return 0 if ok else 1


if __name__ == '__main__':
    sys.exit(main())
