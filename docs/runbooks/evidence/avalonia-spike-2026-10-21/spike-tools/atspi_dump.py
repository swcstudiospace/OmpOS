import gi
gi.require_version('Atspi', '2.0')
from gi.repository import Atspi


def walk(node, depth, out):
    try:
        name = node.get_name() or ''
        role = node.get_role_name() or ''
        out.append('  ' * depth + role + ' | ' + name)
        for i in range(node.get_child_count()):
            child = node.get_child_at_index(i)
            if child is not None:
                walk(child, depth + 1, out)
    except Exception as error:  # report, do not hide
        out.append('  ' * depth + '<error ' + str(error) + '>')


desktop = Atspi.get_desktop(0)
out = ['desktop children: ' + str(desktop.get_child_count())]
for i in range(desktop.get_child_count()):
    app = desktop.get_child_at_index(i)
    if app is None:
        continue
    app_name = app.get_name() or ''
    out.append('APP ' + app_name)
    if 'Spike' in app_name or 'OmpOS' in app_name or 'Avalonia' in app_name:
        walk(app, 1, out)
print('\n'.join(out))
