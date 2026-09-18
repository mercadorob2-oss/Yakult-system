# Yakult Scanner Manual Generator

This folder generates scanner manual deliverables from a single PowerShell content source.

Outputs:

- `docs/scanner-manual/out/Yakult_Scanner_IT_Support_Manual_YYYY-MM-DD.doc`
- `docs/scanner-manual/out/Yakult_Scanner_IT_Support_Manual_YYYY-MM-DD.pdf`

## Generate / Regenerate

From the repo root:

```powershell
powershell -ExecutionPolicy Bypass -File docs/scanner-manual/generate.ps1
```

To override the screenshot source folder:

```powershell
powershell -ExecutionPolicy Bypass -File docs/scanner-manual/generate.ps1 -ImageSource "C:\Path\To\Images"
```

## Screenshot Source

Default screenshot source:

`C:\Users\ITD\Desktop\Yakult-inventory-monitoring-system-User-manual-main\YS_NEW IMAGES`

The generator copies available images into:

- `docs/scanner-manual/assets/`

If the assets already exist locally, the generator reuses them.
