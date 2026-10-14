"""Migrate compiled Inventory UI decoration to the shared Yakult palette."""
from pathlib import Path
import re
import json
import sys
import xml.etree.ElementTree as ET

ROOT = Path(__file__).resolve().parents[2]
APP = ROOT / 'Yakult.Inventory.App'
PROJECT = APP / 'Yakult.Inventory.App.csproj'
PALETTE = '/Yakult.Inventory.App;component/Wpf/Shared/Styles/YakultPalette.xaml'
GROUPS = {
    'Primary': '4E9AFC 3A8EF6 3498DB 2563EB 2F6FED 2D83EA 3B82F6 2E86DE 2D8DF0 2F80ED 2778DF 4A90E2',
    'PrimaryHover': '1A6DD0 2980B9 3A86E8 2E7EE8 2272D9 5FA0F2 6FAEFB 2A5BC8 1664AA 1D4ED8 185ABC 2474CC',
    'Navigation': '007ACC',
    'NavigationMuted': 'BDD9FF CFE3FF AADDFF',
    'PageBackground': 'F0F2F6 F5F7FA F3F5F8 F0F3F7 F5F6FA',
    'SurfaceMuted': 'FAFBFC F8F9FB E8F0F8',
    'AccentTint': 'F0F6FF EBF5FF EFF6FF EAF3FF E8F3FF EEF5FF EEF3FF EEF4FF EFF5FF F4F8FF F2F7FF',
    'AccentBorder': 'BDD0F8 AFC9ED',
    'Selection': 'C8DFFE B8CEED CDD8EE',
    'SelectionHover': 'B8D4FC',
    'Border': 'DDE3EC EEF1F4 E4E8EF CDD5DE',
    'InputBorder': 'C2CCDA',
    'TextDark': '1A2333 3A4A5A 4A5A6A 374151 2A3A4A',
    'TextMuted': '6B7A8D 5A6A7A 5A6A7E 7A8A9A 8A9AAA 5B6A7D 64748B 5A6C7D 6A7A8A',
}
MAP = {color: key for key, values in GROUPS.items() for color in values.split()}
HEX = dict(Primary='B92335', PrimaryHover='9E1E2E', Navigation='182235',
           NavigationMuted='B8C3D4', AccentBorder='E7B8C1',
           PageBackground='F6F5F2', SurfaceMuted='F5F6F8', AccentTint='FCF0F1',
           Selection='F8DDE2', SelectionHover='F2CCD4', Border='E5E6E8',
           InputBorder='CDD0D6', TextDark='202635', TextMuted='677081')
EXCLUDED = {'callmonitoring', 'repairportal', 'requestportal', 'request-portal',
            'borrowitems', 'portal',
            'toast', 'converters', 'viewmodels', 'models', 'walkthrough'}
SEMANTIC = re.compile(r'status|severity|warning|success|error|danger|approved|pending|rejected|completed|fulfilled|overdue|legend|chart|series|stocklevel', re.I)

def in_scope(path):
    parts = {p.lower() for p in path.parts}
    if path.as_posix().lower() == 'wpf/requestportal/requestportalstyles.xaml':
        return True  # Also used by Inventory forms, license reviews, and invoice dialogs.
    if parts & EXCLUDED:
        return False
    if path.parts[0].lower() == 'forms' and path.parts[1].lower() not in {'dashboard', 'reports', 'notifications'}:
        return False
    if path.parts[0].lower() not in {'pages', 'wpf', 'forms'}:
        return False
    return path.name not in {'LoginPage.cs', 'LoginPage.Designer.cs', 'MyAccountPage.cs', 'ChangePasswordDialog.cs', 'RegisterPage.cs', 'RegisterPage.Designer.cs', 'YakultPalette.xaml'}

def merge_palette(text, root):
    if PALETTE in text:
        return text
    entry = '                <ResourceDictionary Source="' + PALETTE + '"/>'
    if root == 'ResourceDictionary':
        if '<ResourceDictionary.MergedDictionaries>' in text:
            return text.replace('<ResourceDictionary.MergedDictionaries>', '<ResourceDictionary.MergedDictionaries>\n' + entry, 1)
        end = text.index('>') + 1
        return text[:end] + '\n    <ResourceDictionary.MergedDictionaries>\n' + entry + '\n    </ResourceDictionary.MergedDictionaries>\n' + text[end:]
    tag = root + '.Resources'
    pattern = r'<' + re.escape(tag) + r'>([\s\S]*?)</' + re.escape(tag) + r'>'
    found = re.search(pattern, text)
    if found:
        inside = found.group(1)
        source_only = re.fullmatch(r'\s*(<ResourceDictionary\s+Source="[^"]+"\s*/>)\s*', inside)
        if source_only:
            inside = '\n        <ResourceDictionary>\n            <ResourceDictionary.MergedDictionaries>\n' + entry + '\n                ' + source_only.group(1) + '\n            </ResourceDictionary.MergedDictionaries>\n        </ResourceDictionary>\n    '
        elif '<ResourceDictionary.MergedDictionaries>' in inside:
            inside = inside.replace('<ResourceDictionary.MergedDictionaries>', '<ResourceDictionary.MergedDictionaries>\n' + entry, 1)
        elif re.search(r'<ResourceDictionary\s*>', inside):
            inside = re.sub(r'<ResourceDictionary\s*>', '<ResourceDictionary>\n            <ResourceDictionary.MergedDictionaries>\n' + entry + '\n            </ResourceDictionary.MergedDictionaries>', inside, count=1)
        else:
            inside = '\n        <ResourceDictionary>\n            <ResourceDictionary.MergedDictionaries>\n' + entry + '\n            </ResourceDictionary.MergedDictionaries>\n' + inside + '\n        </ResourceDictionary>\n    '
        return text[:found.start(1)] + inside + text[found.end(1):]
    end = text.index('>') + 1
    resources = '\n    <' + tag + '>\n        <ResourceDictionary>\n            <ResourceDictionary.MergedDictionaries>\n' + entry + '\n            </ResourceDictionary.MergedDictionaries>\n        </ResourceDictionary>\n    </' + tag + '>\n'
    return text[:end] + resources + text[end:]

def xaml(text):
    tree = ET.fromstring(text)
    root = tree.tag.rsplit('}', 1)[-1]
    if root not in {'UserControl', 'Window', 'ResourceDictionary'}:
        return text
    # Retain status/data triggers and semantic styles verbatim.
    protected = []
    def protect(m):
        protected.append(m.group(0))
        return 'YAKULT_PROTECTED_' + str(len(protected)-1) + '_END'
    text = re.sub(r'<(?:DataTrigger|MultiDataTrigger)\b[\s\S]*?</(?:DataTrigger|MultiDataTrigger)>', protect, text)
    text = re.sub(r'<Style\b[^>]*x:Key="[^"]*(?:Status|Success|Warning|Danger|Error|Badge)[^"]*"[\s\S]*?</Style>', protect, text, flags=re.I)
    # Replace only color attributes, leaving bindings, labels, and business data intact.
    def color(m):
        attr, old = m.group(1), m.group(2).upper()
        if old not in MAP:
            return m.group(0)
        key = MAP[old]
        suffix = 'Color' if attr == 'Color' else 'Brush'
        return attr + '="{StaticResource Yakult' + key + suffix + '}"'
    changed = re.sub(r'\b(Color|Background|Foreground|BorderBrush|Fill|Stroke|CaretBrush|HorizontalGridLinesBrush|VerticalGridLinesBrush|Value)="#([0-9A-Fa-f]{6})"', color, text)
    if changed == text:
        for index, value in reversed(list(enumerate(protected))):
            changed = changed.replace('YAKULT_PROTECTED_' + str(index) + '_END', value)
        return changed
    # Color setters need Color resources, not Brush resources.
    changed = re.sub(r'(<Setter\b[^>]*Property="Color"[^>]*Value="\{StaticResource Yakult\w+)Brush(\}")', r'\1Color\2', changed)
    # Root properties resolve after the root resource dictionary is available.
    end = changed.index('>')
    changed = changed[:end].replace('{StaticResource Yakult', '{DynamicResource Yakult') + changed[end:]
    for index, value in reversed(list(enumerate(protected))):
        changed = changed.replace('YAKULT_PROTECTED_' + str(index) + '_END', value)
    changed = merge_palette(changed, root)
    assert 'YAKULT_PROTECTED_' not in changed
    ET.fromstring(changed)
    return changed

def csharp(text):
    output = []
    used = False
    for line in text.splitlines(keepends=True):
        code = line.split('//', 1)[0]
        if code.lstrip().startswith('//') or SEMANTIC.search(code) or re.search(r'\b(return|case)\b', code):
            output.append(line)
            continue
        def rgb(m):
            nonlocal used
            old = ''.join(f'{int(v):02X}' for v in m.groups()[1:])
            key = MAP.get(old)
            if not key or key in {'SelectionHover', 'Navigation', 'NavigationMuted', 'AccentBorder'}:
                return m.group(0)
            media = m.group(1) == 'FromRgb'
            if key == 'Selection':
                key = 'Selection' if media else 'PrimaryHover'
            cs_key = {'PageBackground': 'HeaderBack', 'AccentTint': 'OutlineHoverBack', 'SurfaceMuted': 'GridAltRowBack'}.get(key, key)
            if media:
                cs_key = {'HeaderBack': 'PageBackground', 'OutlineHoverBack': 'AccentTint', 'GridAltRowBack': 'SurfaceMuted'}.get(cs_key, cs_key)
            if not media and 'ColumnHeadersDefaultCellStyle' in code and 'BackColor' in code:
                cs_key = 'GridHeaderBack'
            used = True
            return 'InventoryTheme.' + ('WpfColors.' if media else 'Colors.') + cs_key
        line = re.sub(r'(?<![\w.])Color\.(FromArgb|FromRgb)\(\s*(\d{1,3})\s*,\s*(\d{1,3})\s*,\s*(\d{1,3})\s*\)', rgb, line)
        # Explicitly named brand greens are action colors; semantic greens remain intact.
        if re.search(r'\bthemeGreen(?:Hover)?\s*=', line):
            key = 'PrimaryHover' if 'themeGreenHover' in line else 'Primary'
            line = re.sub(r'Color\.FromArgb\([^)]*\)', 'InventoryTheme.Colors.' + key, line)
            if 'InventoryTheme.' in line:
                used = True
        # Embedded XAML and ColorConverter calls continue to receive hex strings.
        line = re.sub(r'#[0-9A-Fa-f]{6}\b', lambda m: '#' + HEX[MAP[m.group(0)[1:].upper()]] if m.group(0)[1:].upper() in MAP else m.group(0), line)
        output.append(line)
    result = ''.join(output)
    if used and 'using InventoryTheme =' not in result:
        result = 'using InventoryTheme = Yakult.Inventory.App.Helpers.UiTheme;\n' + result
    return result

items = sorted(set(re.findall(r'<(?:Compile|Page)\s+Include="([^"]+)"', PROJECT.read_text(encoding='utf-8-sig'))))
report = []
for item in items:
    rel = Path(item.replace('\\', '/'))
    path = APP / rel
    if not in_scope(rel) or not path.is_file() or path.suffix not in {'.cs', '.xaml'}:
        continue
    raw = path.read_bytes()
    bom = raw.startswith(b'\xef\xbb\xbf')
    original = raw.decode('utf-8-sig')
    newline = '\r\n' if '\r\n' in original else '\n'
    normalized = original.replace('\r\n', '\n')
    result = xaml(normalized) if path.suffix == '.xaml' else csharp(normalized)
    if result == normalized:
        continue
    report.append(str(rel))
    if '--apply' in sys.argv:
        data = result.replace('\n', newline).encode('utf-8')
        path.write_bytes((b'\xef\xbb\xbf' if bom else b'') + data)
(ROOT / 'work/yakult-theme/changed-files.json').write_text(json.dumps(report, indent=2), encoding='utf-8')
print(('Applied' if '--apply' in sys.argv else 'Would change') + ' palette migration to ' + str(len(report)) + ' compiled UI files.')
print('\n'.join(report))
