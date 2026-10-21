<%@ Page Language="C#" %>

<!DOCTYPE html>
<html lang="en">
<head runat="server">
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1" />
    <title>Yakult Inventory API</title>
    <link rel="preconnect" href="https://fonts.googleapis.com">
    <link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>
    <link href="https://fonts.googleapis.com/css2?family=Space+Grotesk:wght@400;500;600&display=swap" rel="stylesheet">
    <style type="text/css">
        :root {
            --bg: #05060a;
            --bg-accent: radial-gradient(circle at top, rgba(82, 166, 255, .25), transparent 55%);
            --card-bg: rgba(15, 18, 26, 0.8);
            --text-primary: #f4f6fb;
            --text-muted: #96a0ba;
            --accent: #4dd7c6;
            --border: rgba(255, 255, 255, 0.08);
            --shadow: 0 20px 45px rgba(0, 0, 0, 0.4);
        }

        * {
            box-sizing: border-box;
        }

        body {
            margin: 0;
            min-height: 100vh;
            background: var(--bg);
            color: var(--text-primary);
            font-family: "Space Grotesk", system-ui, -apple-system, BlinkMacSystemFont, "Segoe UI", sans-serif;
            display: flex;
            align-items: center;
            justify-content: center;
            padding: 40px 20px;
            position: relative;
        }

        body::before {
            content: "";
            position: absolute;
            inset: 0;
            background: var(--bg-accent);
            z-index: 0;
        }

        main {
            width: min(720px, 100%);
            background: var(--card-bg);
            border-radius: 28px;
            padding: 48px;
            border: 1px solid var(--border);
            box-shadow: var(--shadow);
            position: relative;
            z-index: 1;
        }

        h1 {
            font-size: clamp(2.2rem, 3vw, 2.8rem);
            margin: 0 0 12px;
            letter-spacing: -0.5px;
        }

        .muted {
            color: var(--text-muted);
            margin-bottom: 32px;
            line-height: 1.5;
        }

        .status-card {
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(220px, 1fr));
            gap: 24px;
            margin-bottom: 32px;
        }

        .pill {
            padding: 8px 14px;
            border-radius: 999px;
            background: rgba(77, 215, 198, 0.15);
            display: inline-flex;
            align-items: center;
            gap: 6px;
            font-weight: 500;
            color: var(--accent);
        }

        .timestamp,
        .runtime {
            border-radius: 18px;
            padding: 20px;
            background: rgba(255, 255, 255, 0.02);
            border: 1px solid var(--border);
        }

        .timestamp h3,
        .runtime h3 {
            margin: 0;
            font-size: 0.95rem;
            color: var(--text-muted);
            letter-spacing: 0.1em;
            text-transform: uppercase;
        }

        .timestamp p,
        .runtime p {
            margin: 10px 0 0;
            font-size: 1.3rem;
            font-weight: 600;
        }

        .endpoint-grid {
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(240px, 1fr));
            gap: 18px;
        }

        .endpoint {
            padding: 18px;
            border-radius: 16px;
            border: 1px solid var(--border);
            background: rgba(255, 255, 255, 0.01);
        }

        .endpoint code {
            display: block;
            background: rgba(255, 255, 255, 0.04);
            padding: 10px 12px;
            border-radius: 10px;
            margin-bottom: 10px;
            font-size: 0.95rem;
        }

        .endpoint span {
            color: var(--text-muted);
            font-size: 0.9rem;
        }

        footer {
            margin-top: 40px;
            border-top: 1px solid var(--border);
            padding-top: 16px;
            color: var(--text-muted);
            font-size: 0.9rem;
        }

        @media (max-width: 600px) {
            main {
                padding: 32px 24px;
            }
        }
    </style>
</head>
<body>
    <main>
        <div class="pill">
            <span>●</span>
            API Online
        </div>
        <h1>Yakult Inventory API</h1>
        <p class="muted">
            REST services for Yakult Inventory App + Scanner. If you can see this page, the API site is running properly.
        </p>

        <section class="status-card">
            <div class="timestamp">
                <h3>Server UTC Time</h3>
                <p><%= DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") %></p>
            </div>
            <div class="runtime">
                <h3>Environment</h3>
                <p>ASP.NET Web API · IIS</p>
            </div>
        </section>

        <section>
            <h2>Starter Endpoints</h2>
            <div class="endpoint-grid">
                <div class="endpoint">
                    <code>POST /api/auth/login</code>
                    <span>Authenticate and receive a JWT for clients.</span>
                </div>
                <div class="endpoint">
                    <code>POST /api/auth/register</code>
                    <span>Create a new mobile/scanner user.</span>
                </div>
                <div class="endpoint">
                    <code>GET /api/sets/by-token/{token}</code>
                    <span>Retrieve dispatch set details from a QR token.</span>
                </div>
                <div class="endpoint">
                    <code>GET /api/health</code>
                    <span>Simple heartbeat used by the desktop app.</span>
                </div>
            </div>
        </section>

        <footer>
            Need help? Check the API logs under <code>/App_Data/api-error-log.txt</code>.
        </footer>
    </main>
</body>
</html>
