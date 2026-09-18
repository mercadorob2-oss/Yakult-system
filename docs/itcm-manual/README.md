# IT Call Monitoring (ITCM) Manual (Encoders)

This folder generates two deliverables from a single source:

- `docs/itcm-manual/out/ITCM_User_Manual_Encoders_2026-03-09.doc` (HTML-in-`.doc`, opens in Microsoft Word)
- `docs/itcm-manual/out/ITCM_User_Manual_Encoders_2026-03-09.pdf`

## Generate / Regenerate

From the repo root:

```powershell
powershell -ExecutionPolicy Bypass -File docs/itcm-manual/generate.ps1
```

To generate the IT support manual instead of the encoder guide:

```powershell
powershell -ExecutionPolicy Bypass -File docs/itcm-manual/generate.ps1 -ContentFile itcm-support-manual.content.ps1 -OutputPrefix ITCM_IT_Support_Manual
```

### Images

The generator tries to copy ITCM screenshots from:

`C:\Users\ITD\Desktop\ITCM-IMAGES`

…into `docs/itcm-manual/assets/`.

If you already have the images in `docs/itcm-manual/assets/`, the script will reuse them.
