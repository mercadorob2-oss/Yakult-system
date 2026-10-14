"""Apply the inventory Yakult palette to IT Call Monitoring chrome and actions."""
from pathlib import Path
import re

root = Path(__file__).resolve().parents[2]
folder = root / 'Yakult.Inventory.App/Wpf/CallMonitoring'
neutral = {
    (240, 242, 245): 'PageBackground',
    (241, 244, 247): 'PageBackground',
    (245, 247, 250): 'SurfaceMuted',
    (248, 250, 252): 'SurfaceMuted',
    (223, 229, 236): 'Border',
    (210, 219, 230): 'Border',
    (15, 23, 42): 'TextDark',
    (100, 116, 139): 'TextMuted',
}
brand = {
    (0, 150, 136): 'Primary',
    (15, 118, 110): 'PrimaryHover',
    (13, 148, 136): 'Primary',
}
blue = {(37, 99, 235), (59, 130, 246)}
action = re.compile(
    r'Create(?:Primary|Rounded|Hero|Footer|InlineAction|CompactAction|Action)Button|'
    r'_btn(?:Refresh|OpenDashboard|Tab[12])|_save(?:Smtp|Rules)Button|'
    r'_applyFiltersButton|_refreshButton|_profileOpenButton|'
    r'_scheduleBtn|selectedTrigger|'
    r'(?:Background|Foreground|BorderBrush)\s*=', re.I)
semantic = re.compile(
    r'"(?:Open|Pending|Medium|In Progress|Status|Critical|Filtered Rows|Today.s Volume)"|'
    r'\b(?:AddMetric|MetricCard|MetricPill|Chart|Series|StatusColor)\b', re.I)
color_call = re.compile(r'\b(Color\.FromRgb|BrushFromRgb)\(\s*(\d+)\s*,\s*(\d+)\s*,\s*(\d+)\s*\)')

changed_files = []
for path in folder.glob('*.cs'):
    if path.name in {'WpfThemeResources.cs', 'WpfCallMonitoringShell.cs'}:
        continue
    raw = path.read_bytes()
    bom = raw.startswith(b'\xef\xbb\xbf')
    content = raw.decode('utf-8-sig')
    lines = content.splitlines(keepends=True)
    updated = []
    for line in lines:
        code = line.split('//', 1)[0]
        if not code.strip() or code.lstrip().startswith('//'):
            updated.append(line)
            continue
        def replacement(match):
            rgb = tuple(map(int, match.groups()[1:]))
            key = neutral.get(rgb) or brand.get(rgb)
            if rgb in blue and action.search(code) and not semantic.search(code):
                key = 'Primary'
            if not key:
                return match.group(0)
            return 'WpfThemeResources.' + key + ('Brush' if match.group(1) == 'BrushFromRgb' else '')
        updated.append(color_call.sub(replacement, line))
    result = ''.join(updated)
    if result == content:
        continue
    newline = '\r\n' if '\r\n' in content else '\n'
    result = result.replace('\r\n', '\n').replace('\n', newline)
    path.write_bytes((b'\xef\xbb\xbf' if bom else b'') + result.encode('utf-8'))
    changed_files.append(path.relative_to(root).as_posix())
print(f'Updated {len(changed_files)} WPF ITCM files.')
print('\n'.join(changed_files))
