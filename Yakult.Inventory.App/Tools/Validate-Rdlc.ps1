param([Parameter(Mandatory)][string]$Path)

function Validate-File {
    param([string]$filePath)
    $name = Split-Path $filePath -Leaf
    Write-Host ""
    Write-Host "======================================================"
    Write-Host "  $name"
    Write-Host "======================================================"

    try {
        [xml]$xml = Get-Content $filePath -Encoding UTF8 -ErrorAction Stop
    } catch {
        Write-Host "  [FAIL] Cannot parse XML: $_"
        return
    }

    $nsMgr = New-Object System.Xml.XmlNamespaceManager($xml.NameTable)
    $nsMgr.AddNamespace("r", "http://schemas.microsoft.com/sqlserver/reporting/2016/01/reportdefinition")
    $ok = $true

    $paramNodes = $xml.SelectNodes("//r:ReportParameter", $nsMgr)
    $paramCount = $paramNodes.Count
    Write-Host ""
    Write-Host "  PARAMETERS ($paramCount defined):"
    foreach ($p in $paramNodes) { Write-Host "    . $($p.GetAttribute('Name'))" }

    $layoutNodes = $xml.SelectNodes("//r:ReportParametersLayout", $nsMgr)
    if ($layoutNodes.Count -gt 0) {
        $cellCount = $xml.SelectNodes("//r:ReportParametersLayout//r:CellDefinition", $nsMgr).Count
        if ($cellCount -ne $paramCount) {
            Write-Host "  [FAIL] ReportParametersLayout: $cellCount cells but $paramCount params - remove the block"
            $ok = $false
        } else {
            Write-Host "  [OK]   ReportParametersLayout: $cellCount cells = $paramCount params"
        }
    } else {
        Write-Host "  [OK]   No ReportParametersLayout block (safe)"
    }

    $tablixes = $xml.SelectNodes("//r:Tablix", $nsMgr)
    foreach ($tablix in $tablixes) {
        $tName = $tablix.GetAttribute("Name")
        Write-Host ""
        Write-Host "  TABLIX: $tName"

        $colCount = $tablix.SelectNodes("r:TablixBody/r:TablixColumns/r:TablixColumn", $nsMgr).Count
        Write-Host "    TablixColumns defined: $colCount"

        $allColM = $tablix.SelectNodes("r:TablixColumnHierarchy//r:TablixMember", $nsMgr)
        $leafColCount = 0
        foreach ($m in $allColM) {
            if ($m.SelectNodes("r:TablixMembers/r:TablixMember", $nsMgr).Count -eq 0) { $leafColCount++ }
        }
        Write-Host "    Column hierarchy leaf members: $leafColCount"

        if ($colCount -ne $leafColCount) {
            Write-Host "    [FAIL] Column count ($colCount) != column leaf members ($leafColCount)"
            $ok = $false
        } else {
            Write-Host "    [OK]   Column counts match: $colCount"
        }

        $rowNodes = $tablix.SelectNodes("r:TablixBody/r:TablixRows/r:TablixRow", $nsMgr)
        $rowCount  = $rowNodes.Count
        Write-Host "    TablixRows defined: $rowCount"

        $allRowM = $tablix.SelectNodes("r:TablixRowHierarchy//r:TablixMember", $nsMgr)
        $leafRowCount = 0
        foreach ($m in $allRowM) {
            if ($m.SelectNodes("r:TablixMembers/r:TablixMember", $nsMgr).Count -eq 0) { $leafRowCount++ }
        }
        Write-Host "    Row hierarchy leaf members: $leafRowCount"

        if ($rowCount -ne $leafRowCount) {
            Write-Host "    [FAIL] Row count ($rowCount) != row leaf members ($leafRowCount)"
            $ok = $false
        } else {
            Write-Host "    [OK]   Row counts match: $rowCount"
        }

        $ri = 0
        foreach ($row in $rowNodes) {
            $ri++
            $cells = $row.SelectNodes("r:TablixCells/r:TablixCell", $nsMgr).Count
            if ($cells -ne $colCount) {
                Write-Host "    [FAIL] Row ${ri} has $cells cells - expected $colCount"
                $ok = $false
            } else {
                Write-Host "    [OK]   Row ${ri}: $cells cells"
            }
        }

        $boxNames = $tablix.SelectNodes(".//r:Textbox", $nsMgr) | ForEach-Object { $_.GetAttribute("Name") }
        $dupes = $boxNames | Group-Object | Where-Object { $_.Count -gt 1 }
        if ($dupes) {
            foreach ($d in $dupes) {
                Write-Host "    [FAIL] Duplicate Textbox name '$($d.Name)' appears $($d.Count) times"
                $ok = $false
            }
        } else {
            Write-Host "    [OK]   No duplicate Textbox names"
        }
    }

    Write-Host ""
    if ($ok) { Write-Host "  RESULT: PASS" } else { Write-Host "  RESULT: FAIL - see [FAIL] lines above, fix then rebuild" }
}

if (Test-Path $Path -PathType Container) {
    Get-ChildItem $Path -Include "*.rdlc","*.rdl" -Recurse | ForEach-Object { Validate-File $_.FullName }
} elseif (Test-Path $Path -PathType Leaf) {
    Validate-File $Path
} else {
    Write-Host "Path not found: $Path"
}
