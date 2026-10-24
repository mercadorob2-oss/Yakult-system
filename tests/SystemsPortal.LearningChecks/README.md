# Learning checks

Run from the repository root:

```powershell
dotnet run --project tests/SystemsPortal.LearningChecks
```

The checks validate the versioned course document, media URL rules, quiz answers, legacy Markdown compatibility, and user-specific browser progress keys. They do not register a database provider or load the portal's startup code.

For a local UI fixture, run:

```powershell
dotnet run --project tests/SystemsPortal.LearningChecks -- --serve
```

Open `http://127.0.0.1:5187/Public/Learning` or `/Admin/LearningContent`. This loopback-only host uses sample records and in-memory draft saves. Its authentication is a fixture identity; do not deploy this test host.

Optional automated browser checks:

```powershell
npm install --prefix (Join-Path $env:TEMP 'yakult-learning-qa') playwright --ignore-scripts --no-audit --no-fund
node tests/SystemsPortal.LearningChecks/browser-checks.cjs
```

The script uses the existing cached Chromium revision when present, otherwise the browser installed for Playwright. Screenshots go into the task's temporary QA directory.

## Content compatibility

New structured courses use `yakult-course:v1` followed by JSON in the existing content body. No schema migration is needed. Existing Markdown articles and single-video courses retain their reader and video tracking. Structured course progress is local to a user and browser, versioned by the course body; changing the body creates a fresh progress record. Practice answers and local lesson completion do not create official completion statistics or certificates. Existing Draft and Publish permissions still apply.

The catalog now uses a parameterized count and paged query against existing tables. SQL execution must be verified in an authorized database environment; the local checks deliberately never connect to one.

## Existing demo view

The portal's existing **View demo data** toggle now presents two references in the public Learning area: **IT Policy & Employee Guidelines** and **Meet IT & Our Business Systems**. The policy contains **Workplace Guidelines**, **Profile Account**, **Internet Access**, and **Flowcharts**. Profile Account preserves all seven requirements and the three-part Active Domain Account Process, including the source's password requirements, IT setup, resignation, and account-deletion timing. These are source statements, not a newly approved policy revision.

The three policy flowcharts use the actual source diagrams rather than reconstructed arrows. The Hardware Deployment and Internet Access charts are vector renders of the selected document regions. File Sharing uses its original embedded diagram, including the User and Administrator lanes and backup/deletion path. Text descriptions preserve their branches and connectors. A source note calls out the different deletion/validation timings in the file-sharing diagram and Data Security prose without resolving them. The original Profile Account section is also available for comparison.

The selected source images live outside `wwwroot`, in `App_Data/PolicyPreview`, and are served only through the allowlisted, demo-gated `/media/learning/demo/policy/{file}` route with `Cache-Control: no-store`. The full DOCX/PPTX, network diagrams, procurement/contract-renewal workflows, and unfinished content remain excluded. No original full documents are distributed. Source-section labels do not imply policy approval dates.

The public reference catalog, detail pages, and search use code-only data in demo mode, with no progress or completion tracking. Leaving demo view restores the managed catalog and makes reference-preview URLs return 404. Production demo activation still uses the existing authorized session toggle; the preview does not change authentication rules or publish the content for anonymous production access.

Learning Content retains its separate 15 structured-course authoring samples for compatibility. Authors can inspect and rearrange these previews; Save, Publish, Import, and Upload cannot write demo records or files. Their images, silent video clip, and checklist remain under the demo-gated `/media/learning/demo/` route. Demo browser progress remains separate from real progress.

The fixture host accepts the `learning-fixture-demo=true` cookie to test the real demo controller paths with a repository that throws on every call. Add `learning-fixture-anonymous=true` to test rendering with no fixture identity. These cookies exist only in the isolated test host. Run `node tests/SystemsPortal.LearningChecks/demo-browser-checks.cjs` while the fixture host is running to verify reference search, selected public content, anonymous reading, mobile layouts, course/editor compatibility, and mode isolation.
