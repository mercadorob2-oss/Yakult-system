# Borrow Items Manual Generator

This folder generates borrow-system manual deliverables from a single PowerShell content source.

Outputs:

- `docs/borrow-manual/out/Borrow_Items_IT_Support_Manual_YYYY-MM-DD.doc`
- `docs/borrow-manual/out/Borrow_Items_IT_Support_Manual_YYYY-MM-DD.pdf`

## Generate / Regenerate

From the repo root:

```powershell
powershell -ExecutionPolicy Bypass -File docs/borrow-manual/generate.ps1
```

To override the screenshot source folder:

```powershell
powershell -ExecutionPolicy Bypass -File docs/borrow-manual/generate.ps1 -ImageSource "C:\Path\To\Images"
```

## Screenshot Source

Default screenshot source:

`C:\Users\ITD\Desktop\Yakult-inventory-monitoring-system-User-manual-main\borrow_images`

The generator copies available images into:

- `docs/borrow-manual/assets/`

If the assets already exist locally, the generator reuses them.
