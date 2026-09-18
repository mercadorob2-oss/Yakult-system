$Manual = [ordered]@{
    Title = 'Yakult Scanner - IT Support and Operations Manual'
    Subtitle = 'Android Mobile Scanner Administration, Operations, and Troubleshooting Guide'
    Version = 'v1.0'
    Date = '2026-03-11'
    Notes = @(
        'This manual is intended for IT support, IT technical staff, and application support personnel responsible for operating and troubleshooting the Yakult Scanner Android application.'
        'The scanner application is a mobile companion to the Yakult inventory ecosystem. It depends on the backend API for login, dispatch lookup, update upload, batch item creation, and serial handoff.'
    )
    Sections = @(
        @{
            Title = '1) Purpose and Scope'
            Paragraphs = @(
                'Yakult Scanner is the Android mobile subsystem used to scan dispatch QR codes, send serial numbers from mobile to desktop-related workflows, create batch inventory items, review mobile-originated updates, and capture support diagnostics from the field.',
                'This manual covers connection setup, login, dashboard usage, dispatch scanning, serial workflows, pending and processed update monitoring, diagnostics, and support handling. It is written for technical and support personnel rather than casual end users.'
            )
            Bullets = @(
                'System location: Latest_sys/YakultScanner.'
                'Documented environments inferred from the project: DEV and PROD.'
                'Primary mobile integration points: auth, set lookup, SetUpdates, batch item creation, and serial handoff APIs.'
            )
        }
        @{
            Title = '2) System Overview and Main Modules'
            Paragraphs = @(
                'After login, the user lands on the main dashboard where the application shows a quick operational summary and provides entry points into the scanner and serial workflows.',
                'The main dashboard is the support team''s first checkpoint because it immediately shows whether the app is alive, whether the API appears reachable, and whether the device can move into either operational workflow.'
            )
            Bullets = @(
                'Root Home shows processed and pending counts.'
                'Yakult Scanner module opens dispatch scanning and recent sets.'
                'Serial Dashboard opens batch serial entry and direct serial handoff workflows.'
                'The bottom navigation exposes Search, Explore, Home, Quick Scan, and Profile/Diagnostics entry points.'
            )
            Images = @('home_screen.png')
        }
        @{
            Title = '3) Login and Connection Configuration'
            Paragraphs = @(
                'The login screen is the first operational screen. Users must authenticate against the backend API before the app can load protected data. The same screen also exposes an application settings dialog used to change or test the configured backend connection.',
                'Support personnel should always verify the target host and port before deeper troubleshooting. A successful health check confirms that the device can reach the backend host but does not, by itself, guarantee every business endpoint is healthy.'
            )
            Bullets = @(
                'The login form requires username and password.'
                'Registration is available in the app, so support should know whether self-registration is allowed in the target environment.'
                'The settings dialog allows Server IP and Port changes.'
                'Test Connection uses the API health endpoint before saving the new configuration.'
            )
            Images = @('login_screen.png', 'connection.png')
        }
        @{
            Title = '4) Dispatch Scanner Home'
            Paragraphs = @(
                'The Yakult Scanner module is the entry point for dispatch-related scanning. It lets the operator choose between hardware scanning and camera scanning, then provides quick access to scan history and pending issue monitoring.',
                'This screen is where support should begin when validating whether the dispatch side of the mobile app is healthy. If this screen loads but scanning fails, the failure is likely in scanner capture, token resolution, or downstream dispatch retrieval.'
            )
            Bullets = @(
                'New Device Scan is intended for built-in hardware scanner use.'
                'Use Phone Camera opens the camera-based QR workflow.'
                'Scan History provides local recent-scan visibility on the current device.'
                'Issues opens the grouped list of pending issue uploads by set code.'
            )
            Images = @('yakult_scanner_homescreen.png')
        }
        @{
            Title = '5) QR Capture and Dispatch Retrieval'
            Paragraphs = @(
                'The scanner screen is used to capture dispatch QR codes. In camera mode, the app presents a live camera preview with a scan frame. In hardware mode, the app waits for device-generated scan input. The same overall workflow supports token-based QR and raw JSON QR payloads.',
                'When the scanned payload is token-based, the mobile app asks the backend to resolve the token into a dispatch set. This is the preferred route because it gives the app a server-authoritative record and allows deploy actions later in the process.'
            )
            Bullets = @(
                'The scanner supports camera capture, hardware wedge input, and Honeywell broadcast input where device support exists.'
                'Gallery-based QR extraction is available in camera mode.'
                'Token QR format is expected as yakult:set:v1:GUID.'
                'A successful scan should lead to dispatch details and local history entry creation.'
            )
            Images = @('scan_qr_interface.png')
            Callouts = @(
                @{ Kind = 'tip'; Text = 'If camera mode works but hardware mode does not, check device-specific scanner configuration before blaming the API.' }
            )
        }
        @{
            Title = '6) Pending Issues and Update Lifecycle'
            Paragraphs = @(
                'The mobile app separates the user''s field action from desktop-side processing. A mobile upload creates a pending record first. Later, the desktop-side workflow or office-side staff processes that record and the update becomes part of the processed view.',
                'Support must understand this separation. A user may successfully upload an issue from the phone and still require desktop-side follow-up before the main inventory state is fully updated.'
            )
            Bullets = @(
                'Pending Issues groups sets with unprocessed issue uploads.'
                'Pending Updates shows individual unprocessed update records coming from mobile.'
                'Processed Updates shows updates already handled by the desktop-side process.'
                'The office-side mobile updates page is a downstream checkpoint outside the Android app itself.'
            )
            Images = @('pending_issues.png', 'pending_updates_page.png', 'pending_updates.png', 'desktop_update_page.png')
        }
        @{
            Title = '7) Serial Scan Dashboard'
            Paragraphs = @(
                'The serial dashboard is the second major module in the application. It separates quick serial handoff from structured batch item creation so the operator can choose the workflow that matches the operational need.',
                'Support should use this page to confirm that the serial subsystem is reachable and that both serial-related entry points load without crash.'
            )
            Bullets = @(
                'Add Items by Serial is the structured batch entry workflow.'
                'Direct Serial Scan is the fast handoff workflow for serial values.'
                'Both workflows rely on serial normalization so duplicate formatting does not create duplicate entries.'
            )
            Images = @('serial_scan_dashboard_page.png')
        }
        @{
            Title = '8) Direct Serial Scan Workflow'
            Paragraphs = @(
                'Direct Serial Scan is used when the operator needs to capture one or more serials and send them to the Windows-side flow as quickly as possible. Serials can be typed manually, scanned with the camera route, or supplied by hardware scanner input depending on device setup.',
                'The app sends the serials sequentially rather than in parallel. This makes results easier to interpret during support incidents because each serial has an ordered attempt rather than a concurrent submission burst.'
            )
            Bullets = @(
                'Operator can add multiple serials before sending.'
                'The send button posts serials to the mobile-to-Windows endpoint.'
                'The list is cleared only on full success.'
                'If the phone reports success but desktop behavior is absent, the next checkpoint is the server or Windows consumer path.'
            )
            Images = @('direct_serial_scan_page.png')
        }
        @{
            Title = '9) Batch Item Entry Workflow'
            Paragraphs = @(
                'Batch Entry is the structured mobile workflow for creating multiple items at once. The screen combines item details, category and condition selection, purchase details, and a serial list before submitting a single batch request.',
                'This workflow is more data-rich than Direct Serial Scan. It depends on supporting API lookups for categories, conditions, and vendors. If those lookups fail, the form may open but remain operationally incomplete.'
            )
            Bullets = @(
                'Required fields include item name, model number, category, condition, and at least one serial.'
                'The form changes by selected type: Hardware, Software/License, or Services.'
                'Purchase details include vendor, warranty, date purchased, and remarks.'
                'Serials are added into a list before final save.'
            )
            Images = @('batch_entry_page.png', 'batch_entry_page_extension.png')
        }
        @{
            Title = '10) Scan History and Local Device Evidence'
            Paragraphs = @(
                'Scan History is a local convenience view, not the enterprise source of truth. It helps the current device user reopen recent scans and quickly return to recently used set records.',
                'Because this history is device-local, support should not assume another device will show the same entries. Clearing cache also removes this history.'
            )
            Bullets = @(
                'History is stored locally on the device.'
                'The current implementation limits history to 50 entries.'
                'Pinned sets and scan history are maintenance data, not official audit data.'
            )
            Images = @('scan_history.png')
        }
        @{
            Title = '11) App Info and Diagnostics'
            Paragraphs = @(
                'The diagnostics screen is the required evidence page for support incidents. It shows the current app version, package name, device model, Android version, Android ID, and the configured API base URL.',
                'Support staff should capture this screen whenever escalating an issue because it provides the minimum environment fingerprint needed to separate device issues from application or backend issues.'
            )
            Bullets = @(
                'Use diagnostics to confirm app version and package.'
                'Use diagnostics to confirm the exact base URL in use.'
                'Clear Cache removes local scan history and pinned sets only.'
            )
            Images = @('appinfo_diagnostic_page.png')
            Callouts = @(
                @{ Kind = 'warn'; Text = 'Diagnostics proves the current device configuration, but it does not prove that every business endpoint is healthy. Always combine it with a real workflow test.' }
            )
        }
        @{
            Title = '12) Support Checklist and Escalation Rules'
            Paragraphs = @(
                'When supporting the scanner application, always verify the simplest things first: target host, login, scanner entry screen, and whether the issue is local to one device or repeated across multiple devices. Then move outward into API, desktop, or backend ownership depending on where the workflow stops.',
                'A working mobile upload does not mean the entire business process is complete. The update may still be waiting in the desktop-side or office-side process. Support should identify the failed step rather than describing the entire system as down.'
            )
            Bullets = @(
                'Check connection settings and health test first.'
                'Confirm whether issue affects login, scanning, upload, or desktop-side processing.'
                'Capture app version, device, base URL, set code or serial, and screenshots before escalation.'
                'Recommended escalation path: IT Support -> Supervisor -> Developer/API owner.'
            )
        }
        @{
            Title = '13) Known Technical Gaps'
            Paragraphs = @(
                'The current codebase has several known constraints that support should understand before writing policy around the application. These are not necessarily production blockers, but they do affect how incidents should be described and triaged.',
                'The most important constraints are cleartext HTTP, hardcoded default endpoints, free-text issue status entry, lack of offline queueing, and missing release-signing configuration in Gradle.'
            )
            Bullets = @(
                'HTTP cleartext is enabled.'
                'Default endpoints use a fixed internal IP.'
                'Item status entry is free-text rather than fully controlled.'
                'No offline transaction queue exists.'
                'Release signing configuration is not defined in source.'
            )
        }
        @{
            Title = '14) Quick Readiness Checklist'
            Bullets = @(
                'Can the app open to login without crashing?'
                'Does Test Connection succeed for the intended host and port?'
                'Can a valid account sign in?'
                'Does Root Home display dashboard cards?'
                'Does Yakult Scanner open and scan successfully?'
                'Does Direct Serial Scan send serials successfully?'
                'Do Pending and Processed Updates load?'
                'Has diagnostics evidence been captured before escalation?'
            )
        }
    )
}
