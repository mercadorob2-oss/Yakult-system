<%@ Page Language="C#" %>
<%@ Import Namespace="System.Configuration" %>
<%@ Import Namespace="System.Data.SqlClient" %>

<!DOCTYPE html>
<html lang="en">
<head runat="server">
    <meta charset="utf-8" />
    <meta name="viewport" content="width=device-width, initial-scale=1" />
    <title>Yakult Inventory API</title>
    <link rel="preconnect" href="https://fonts.googleapis.com">
    <link rel="preconnect" href="https://fonts.gstatic.com" crossorigin>
    <link href="https://fonts.googleapis.com/css2?family=Inter:wght@400;500;600;700&family=JetBrains+Mono:wght@400;500&display=swap" rel="stylesheet">
    <style type="text/css">
        :root {
            --bg: #0a0f1a;
            --bg-gradient: radial-gradient(ellipse at 20% 0%, rgba(59, 130, 246, 0.15) 0%, transparent 50%),
                           radial-gradient(ellipse at 80% 100%, rgba(16, 185, 129, 0.1) 0%, transparent 50%);
            --surface: rgba(15, 23, 42, 0.6);
            --surface-hover: rgba(30, 41, 59, 0.8);
            --text-primary: #f1f5f9;
            --text-secondary: #94a3b8;
            --text-muted: #64748b;
            --accent: #3b82f6;
            --accent-success: #10b981;
            --accent-warning: #f59e0b;
            --accent-danger: #ef4444;
            --border: rgba(148, 163, 184, 0.1);
            --shadow: 0 25px 50px -12px rgba(0, 0, 0, 0.5);
            --font-sans: "Inter", system-ui, -apple-system, sans-serif;
            --font-mono: "JetBrains Mono", "SF Mono", monospace;
        }

        * { box-sizing: border-box; margin: 0; padding: 0; }

        body {
            font-family: var(--font-sans);
            background: var(--bg);
            background-image: var(--bg-gradient);
            color: var(--text-primary);
            min-height: 100vh;
            line-height: 1.6;
        }

        .container {
            max-width: 1200px;
            margin: 0 auto;
            padding: 40px 24px;
        }

        /* Header */
        .header {
            display: flex;
            align-items: flex-start;
            justify-content: space-between;
            gap: 24px;
            margin-bottom: 32px;
            flex-wrap: wrap;
        }

        .brand {
            display: flex;
            align-items: center;
            gap: 16px;
        }

        .logo {
            width: 56px;
            height: 56px;
            background: linear-gradient(135deg, var(--accent) 0%, #8b5cf6 100%);
            border-radius: 16px;
            display: flex;
            align-items: center;
            justify-content: center;
            font-size: 28px;
            box-shadow: 0 10px 30px rgba(59, 130, 246, 0.3);
        }

        .brand-text h1 {
            font-size: 1.75rem;
            font-weight: 700;
            letter-spacing: -0.02em;
            margin-bottom: 4px;
        }

        .brand-text p {
            color: var(--text-secondary);
            font-size: 0.95rem;
        }

        .env-badge {
            display: inline-flex;
            align-items: center;
            gap: 8px;
            padding: 8px 16px;
            background: var(--surface);
            border: 1px solid var(--border);
            border-radius: 999px;
            font-size: 0.875rem;
            font-weight: 500;
        }

        .env-badge.dev { color: var(--accent-warning); }
        .env-badge.prod { color: var(--accent-success); }

        .status-dot {
            width: 8px;
            height: 8px;
            border-radius: 50%;
            background: currentColor;
            animation: pulse 2s infinite;
        }

        @keyframes pulse {
            0%, 100% { opacity: 1; }
            50% { opacity: 0.5; }
        }

        /* Cards Grid */
        .cards-grid {
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(260px, 1fr));
            gap: 16px;
            margin-bottom: 32px;
        }

        .card {
            background: var(--surface);
            border: 1px solid var(--border);
            border-radius: 16px;
            padding: 20px;
            backdrop-filter: blur(12px);
        }

        .card-header {
            display: flex;
            align-items: center;
            gap: 12px;
            margin-bottom: 12px;
        }

        .card-icon {
            width: 40px;
            height: 40px;
            border-radius: 12px;
            display: flex;
            align-items: center;
            justify-content: center;
            font-size: 20px;
        }

        .card-icon.blue { background: rgba(59, 130, 246, 0.15); }
        .card-icon.green { background: rgba(16, 185, 129, 0.15); }
        .card-icon.orange { background: rgba(245, 158, 11, 0.15); }
        .card-icon.purple { background: rgba(139, 92, 246, 0.15); }

        .card-label {
            font-size: 0.75rem;
            text-transform: uppercase;
            letter-spacing: 0.1em;
            color: var(--text-muted);
            font-weight: 600;
        }

        .card-value {
            font-size: 1.25rem;
            font-weight: 700;
            font-family: var(--font-mono);
            color: var(--text-primary);
            word-break: break-word;
        }

        .card-value.small {
            font-size: 1rem;
        }

        .db-status {
            display: inline-flex;
            align-items: center;
            gap: 6px;
        }

        .db-status.connected { color: var(--accent-success); }
        .db-status.error { color: var(--accent-danger); }

        /* Endpoints Section */
        .section {
            background: var(--surface);
            border: 1px solid var(--border);
            border-radius: 20px;
            padding: 24px;
            margin-bottom: 24px;
            backdrop-filter: blur(12px);
        }

        .section-header {
            display: flex;
            align-items: center;
            gap: 12px;
            margin-bottom: 20px;
            padding-bottom: 16px;
            border-bottom: 1px solid var(--border);
        }

        .section-icon {
            width: 36px;
            height: 36px;
            border-radius: 10px;
            display: flex;
            align-items: center;
            justify-content: center;
            font-size: 18px;
        }

        .section-title {
            font-size: 1.125rem;
            font-weight: 600;
        }

        .section-desc {
            color: var(--text-secondary);
            font-size: 0.875rem;
            margin-left: auto;
        }

        /* Endpoint Items */
        .endpoint-list {
            display: flex;
            flex-direction: column;
            gap: 12px;
        }

        .endpoint {
            display: flex;
            align-items: center;
            gap: 16px;
            padding: 16px;
            background: rgba(255, 255, 255, 0.03);
            border: 1px solid var(--border);
            border-radius: 12px;
            transition: all 0.2s ease;
        }

        .endpoint:hover {
            background: var(--surface-hover);
            border-color: rgba(148, 163, 184, 0.2);
        }

        .method {
            font-family: var(--font-mono);
            font-size: 0.75rem;
            font-weight: 700;
            padding: 4px 10px;
            border-radius: 6px;
            text-transform: uppercase;
            flex-shrink: 0;
        }

        .method.get { background: rgba(59, 130, 246, 0.2); color: #60a5fa; }
        .method.post { background: rgba(16, 185, 129, 0.2); color: #34d399; }
        .method.put { background: rgba(245, 158, 11, 0.2); color: #fbbf24; }
        .method.delete { background: rgba(239, 68, 68, 0.2); color: #f87171; }

        .endpoint-path {
            font-family: var(--font-mono);
            font-size: 0.9rem;
            color: var(--text-primary);
            flex: 1;
        }

        .endpoint-desc {
            color: var(--text-secondary);
            font-size: 0.875rem;
            max-width: 300px;
            text-align: right;
        }

        .copy-btn {
            padding: 6px 12px;
            background: transparent;
            border: 1px solid var(--border);
            border-radius: 6px;
            color: var(--text-secondary);
            font-size: 0.75rem;
            cursor: pointer;
            transition: all 0.2s ease;
            flex-shrink: 0;
        }

        .copy-btn:hover {
            background: rgba(255, 255, 255, 0.05);
            color: var(--text-primary);
        }

        .copy-btn.copied {
            background: rgba(16, 185, 129, 0.2);
            color: var(--accent-success);
            border-color: var(--accent-success);
        }

        /* Legacy Section */
        .legacy-grid {
            display: grid;
            grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
            gap: 12px;
        }

        .legacy-item {
            padding: 14px 16px;
            background: rgba(255, 255, 255, 0.03);
            border: 1px solid var(--border);
            border-radius: 10px;
            font-family: var(--font-mono);
            font-size: 0.85rem;
            color: var(--text-secondary);
        }

        /* Footer */
        .footer {
            display: flex;
            align-items: center;
            justify-content: space-between;
            gap: 16px;
            padding-top: 24px;
            border-top: 1px solid var(--border);
            color: var(--text-muted);
            font-size: 0.875rem;
            flex-wrap: wrap;
        }

        .footer-links {
            display: flex;
            gap: 20px;
        }

        .footer-links a {
            color: var(--text-secondary);
            text-decoration: none;
            transition: color 0.2s ease;
        }

        .footer-links a:hover {
            color: var(--accent);
        }

        /* Toast */
        .toast {
            position: fixed;
            bottom: 24px;
            right: 24px;
            padding: 12px 20px;
            background: var(--surface);
            border: 1px solid var(--border);
            border-radius: 12px;
            color: var(--text-primary);
            font-size: 0.875rem;
            box-shadow: var(--shadow);
            opacity: 0;
            transform: translateY(20px);
            transition: all 0.3s ease;
            pointer-events: none;
            z-index: 100;
        }

        .toast.show {
            opacity: 1;
            transform: translateY(0);
        }

        /* Responsive */
        @media (max-width: 768px) {
            .header {
                flex-direction: column;
            }
            
            .endpoint {
                flex-wrap: wrap;
            }
            
            .endpoint-desc {
                text-align: left;
                width: 100%;
                max-width: none;
                margin-top: 8px;
                padding-left: 52px;
            }
            
            .copy-btn {
                margin-left: auto;
            }
            
            .section-desc {
                display: none;
            }
        }
    </style>
</head>
<body>
    <% 
        string dbName = "Unknown";
        string dbStatus = "error";
        string connString = "";
        var cs = ConfigurationManager.ConnectionStrings["Yakult_Inventory_System"];
        if (cs != null) { connString = cs.ConnectionString; }
        try {
            using (var con = new SqlConnection(connString)) {
                con.Open();
                using (var cmd = new SqlCommand("SELECT DB_NAME()", con)) {
                    dbName = (string)cmd.ExecuteScalar();
                    dbStatus = "connected";
                }
            }
        } catch { dbStatus = "error"; }
        
        bool isDev = dbName.Contains("DEV") || dbName.Contains("Test") || dbName.Contains("Staging");
    %>

    <div class="container">
        <!-- Header -->
        <header class="header">
            <div class="brand">
                <div class="logo">&#128241;</div>
                <div class="brand-text">
                    <h1>Yakult Inventory API</h1>
                    <p>REST services for Inventory App & Mobile Scanner</p>
                </div>
            </div>
            <div class="env-badge <%= isDev ? "dev" : "prod" %>">
                <span class="status-dot"></span>
                <%= isDev ? "Development" : "Production" %>
            </div>
        </header>

        <!-- Status Cards -->
        <div class="cards-grid">
            <div class="card">
                <div class="card-header">
                    <div class="card-icon blue">&#128340;</div>
                    <span class="card-label">Server Time (UTC)</span>
                </div>
                <div class="card-value small"><%= DateTime.UtcNow.ToString("yyyy-MM-dd HH:mm:ss") %></div>
            </div>
            
            <div class="card">
                <div class="card-header">
                    <div class="card-icon green">&#128187;</div>
                    <span class="card-label">Runtime</span>
                </div>
                <div class="card-value small">ASP.NET 4.7.2</div>
            </div>
            
            <div class="card">
                <div class="card-header">
                    <div class="card-icon <%= dbStatus == "connected" ? "green" : "orange" %>">&#128267;</div>
                    <span class="card-label">Database</span>
                </div>
                <div class="card-value small">
                    <span class="db-status <%= dbStatus %>">
                        <%= dbStatus == "connected" ? "&#9679;" : "&#9679;" %>
                        <%= dbName %>
                    </span>
                </div>
            </div>
            
            <div class="card">
                <div class="card-header">
                    <div class="card-icon purple">&#128161;</div>
                    <span class="card-label">API Version</span>
                </div>
                <div class="card-value small">v2.1.0</div>
            </div>
        </div>

        <!-- Public Endpoints -->
        <div class="section">
            <div class="section-header">
                <div class="section-icon" style="background: rgba(16, 185, 129, 0.15);">&#128275;</div>
                <span class="section-title">Public Endpoints</span>
                <span class="section-desc">No authentication required</span>
            </div>
            <div class="endpoint-list">
                <div class="endpoint">
                    <span class="method get">GET</span>
                    <code class="endpoint-path">/api/health</code>
                    <span class="endpoint-desc">Health check with DB status</span>
                    <button class="copy-btn" onclick="copyPath(this, '/api/health')">Copy</button>
                </div>
                <div class="endpoint">
                    <span class="method post">POST</span>
                    <code class="endpoint-path">/api/auth/login</code>
                    <span class="endpoint-desc">Authenticate & receive JWT</span>
                    <button class="copy-btn" onclick="copyPath(this, '/api/auth/login')">Copy</button>
                </div>
                <div class="endpoint">
                    <span class="method post">POST</span>
                    <code class="endpoint-path">/api/auth/register</code>
                    <span class="endpoint-desc">Register new user</span>
                    <button class="copy-btn" onclick="copyPath(this, '/api/auth/register')">Copy</button>
                </div>
            </div>
        </div>

        <!-- Dispatch & Sets -->
        <div class="section">
            <div class="section-header">
                <div class="section-icon" style="background: rgba(139, 92, 246, 0.15);">&#128230;</div>
                <span class="section-title">Dispatch & Sets</span>
                <span class="section-desc">JWT required</span>
            </div>
            <div class="endpoint-list">
                <div class="endpoint">
                    <span class="method get">GET</span>
                    <code class="endpoint-path">/api/sets/by-token/{token}</code>
                    <span class="endpoint-desc">Get set by QR token</span>
                    <button class="copy-btn" onclick="copyPath(this, '/api/sets/by-token/{token}')">Copy</button>
                </div>
                <div class="endpoint">
                    <span class="method put">PUT</span>
                    <code class="endpoint-path">/api/dispatch/{setId}</code>
                    <span class="endpoint-desc">Deploy/dispatch a set</span>
                    <button class="copy-btn" onclick="copyPath(this, '/api/dispatch/{setId}')">Copy</button>
                </div>
                <div class="endpoint">
                    <span class="method get">GET</span>
                    <code class="endpoint-path">/api/dispatch/resolve-token/{token}</code>
                    <span class="endpoint-desc">Resolve token to set ID</span>
                    <button class="copy-btn" onclick="copyPath(this, '/api/dispatch/resolve-token/{token}')">Copy</button>
                </div>
                <div class="endpoint">
                    <span class="method get">GET</span>
                    <code class="endpoint-path">/api/dispatch/status?setCodes=A,B,C</code>
                    <span class="endpoint-desc">Batch status check</span>
                    <button class="copy-btn" onclick="copyPath(this, '/api/dispatch/status')">Copy</button>
                </div>
            </div>
        </div>

        <!-- Items -->
        <div class="section">
            <div class="section-header">
                <div class="section-icon" style="background: rgba(59, 130, 246, 0.15);">&#128295;</div>
                <span class="section-title">Items</span>
                <span class="section-desc">JWT required</span>
            </div>
            <div class="endpoint-list">
                <div class="endpoint">
                    <span class="method post">POST</span>
                    <code class="endpoint-path">/api/Items/CreateBatch</code>
                    <span class="endpoint-desc">Create multiple items</span>
                    <button class="copy-btn" onclick="copyPath(this, '/api/Items/CreateBatch')">Copy</button>
                </div>
                <div class="endpoint">
                    <span class="method post">POST</span>
                    <code class="endpoint-path">/api/Items/ReceiveSerialFromMobile</code>
                    <span class="endpoint-desc">Queue serial from scanner</span>
                    <button class="copy-btn" onclick="copyPath(this, '/api/Items/ReceiveSerialFromMobile')">Copy</button>
                </div>
                <div class="endpoint">
                    <span class="method get">GET</span>
                    <code class="endpoint-path">/api/Items/Categories</code>
                    <span class="endpoint-desc">List item categories</span>
                    <button class="copy-btn" onclick="copyPath(this, '/api/Items/Categories')">Copy</button>
                </div>
                <div class="endpoint">
                    <span class="method get">GET</span>
                    <code class="endpoint-path">/api/Items/Conditions</code>
                    <span class="endpoint-desc">List item conditions</span>
                    <button class="copy-btn" onclick="copyPath(this, '/api/Items/Conditions')">Copy</button>
                </div>
                <div class="endpoint">
                    <span class="method get">GET</span>
                    <code class="endpoint-path">/api/Items/Vendors</code>
                    <span class="endpoint-desc">List vendors</span>
                    <button class="copy-btn" onclick="copyPath(this, '/api/Items/Vendors')">Copy</button>
                </div>
            </div>
        </div>

        <!-- Set Updates -->
        <div class="section">
            <div class="section-header">
                <div class="section-icon" style="background: rgba(245, 158, 11, 0.15);">&#9997;</div>
                <span class="section-title">Set Updates</span>
                <span class="section-desc">JWT required</span>
            </div>
            <div class="endpoint-list">
                <div class="endpoint">
                    <span class="method get">GET</span>
                    <code class="endpoint-path">/api/SetUpdates?setCode=XXX</code>
                    <span class="endpoint-desc">List updates by set code</span>
                    <button class="copy-btn" onclick="copyPath(this, '/api/SetUpdates')">Copy</button>
                </div>
                <div class="endpoint">
                    <span class="method get">GET</span>
                    <code class="endpoint-path">/api/SetUpdates/Pending</code>
                    <span class="endpoint-desc">List pending updates</span>
                    <button class="copy-btn" onclick="copyPath(this, '/api/SetUpdates/Pending')">Copy</button>
                </div>
                <div class="endpoint">
                    <span class="method get">GET</span>
                    <code class="endpoint-path">/api/SetUpdates/PendingCount</code>
                    <span class="endpoint-desc">Count of pending items</span>
                    <button class="copy-btn" onclick="copyPath(this, '/api/SetUpdates/PendingCount')">Copy</button>
                </div>
                <div class="endpoint">
                    <span class="method post">POST</span>
                    <code class="endpoint-path">/api/SetUpdates/Upload</code>
                    <span class="endpoint-desc">Upload set item updates</span>
                    <button class="copy-btn" onclick="copyPath(this, '/api/SetUpdates/Upload')">Copy</button>
                </div>
            </div>
        </div>

        <!-- Borrow System -->
        <div class="section">
            <div class="section-header">
                <div class="section-icon" style="background: rgba(236, 72, 153, 0.15);">&#128179;</div>
                <span class="section-title">Borrow System</span>
                <span class="section-desc">JWT required</span>
            </div>
            <div class="endpoint-list">
                <div class="endpoint">
                    <span class="method get">GET</span>
                    <code class="endpoint-path">/api/Borrow/Resolve?serial=XXX</code>
                    <span class="endpoint-desc">Resolve item by serial</span>
                    <button class="copy-btn" onclick="copyPath(this, '/api/Borrow/Resolve')">Copy</button>
                </div>
                <div class="endpoint">
                    <span class="method get">GET</span>
                    <code class="endpoint-path">/api/Borrow/Employees?q=search</code>
                    <span class="endpoint-desc">Search employees</span>
                    <button class="copy-btn" onclick="copyPath(this, '/api/Borrow/Employees')">Copy</button>
                </div>
                <div class="endpoint">
                    <span class="method post">POST</span>
                    <code class="endpoint-path">/api/Borrow/Employees</code>
                    <span class="endpoint-desc">Create new employee</span>
                    <button class="copy-btn" onclick="copyPath(this, '/api/Borrow/Employees')">Copy</button>
                </div>
                <div class="endpoint">
                    <span class="method get">GET</span>
                    <code class="endpoint-path">/api/Borrow/Companies</code>
                    <span class="endpoint-desc">List companies</span>
                    <button class="copy-btn" onclick="copyPath(this, '/api/Borrow/Companies')">Copy</button>
                </div>
                <div class="endpoint">
                    <span class="method get">GET</span>
                    <code class="endpoint-path">/api/Borrow/Branches?companyId=1</code>
                    <span class="endpoint-desc">List branches</span>
                    <button class="copy-btn" onclick="copyPath(this, '/api/Borrow/Branches')">Copy</button>
                </div>
                <div class="endpoint">
                    <span class="method get">GET</span>
                    <code class="endpoint-path">/api/Borrow/Departments?companyId=1</code>
                    <span class="endpoint-desc">List departments</span>
                    <button class="copy-btn" onclick="copyPath(this, '/api/Borrow/Departments')">Copy</button>
                </div>
                <div class="endpoint">
                    <span class="method post">POST</span>
                    <code class="endpoint-path">/api/Borrow</code>
                    <span class="endpoint-desc">Create borrow record</span>
                    <button class="copy-btn" onclick="copyPath(this, '/api/Borrow')">Copy</button>
                </div>
                <div class="endpoint">
                    <span class="method post">POST</span>
                    <code class="endpoint-path">/api/Borrow/Return</code>
                    <span class="endpoint-desc">Return borrowed item</span>
                    <button class="copy-btn" onclick="copyPath(this, '/api/Borrow/Return')">Copy</button>
                </div>
                <div class="endpoint">
                    <span class="method post">POST</span>
                    <code class="endpoint-path">/api/Borrow/Delete</code>
                    <span class="endpoint-desc">Delete borrow record</span>
                    <button class="copy-btn" onclick="copyPath(this, '/api/Borrow/Delete')">Copy</button>
                </div>
                <div class="endpoint">
                    <span class="method get">GET</span>
                    <code class="endpoint-path">/api/Borrow/Open</code>
                    <span class="endpoint-desc">List open borrows</span>
                    <button class="copy-btn" onclick="copyPath(this, '/api/Borrow/Open')">Copy</button>
                </div>
                <div class="endpoint">
                    <span class="method get">GET</span>
                    <code class="endpoint-path">/api/Borrow/Home</code>
                    <span class="endpoint-desc">Dashboard summary stats</span>
                    <button class="copy-btn" onclick="copyPath(this, '/api/Borrow/Home')">Copy</button>
                </div>
                <div class="endpoint">
                    <span class="method get">GET</span>
                    <code class="endpoint-path">/api/Borrow/History</code>
                    <span class="endpoint-desc">Full borrow history</span>
                    <button class="copy-btn" onclick="copyPath(this, '/api/Borrow/History')">Copy</button>
                </div>
            </div>
        </div>

        <!-- IT Call Monitoring -->
        <div class="section">
            <div class="section-header">
                <div class="section-icon" style="background: rgba(239, 68, 68, 0.15);">&#128222;</div>
                <span class="section-title">IT Call Monitoring</span>
                <span class="section-desc">Mobile ticket management</span>
            </div>
            <div class="endpoint-list">
                <div class="endpoint">
                    <span class="method get">GET</span>
                    <code class="endpoint-path">call-tickets.ashx?status=pending&amp;page=1&amp;pageSize=25</code>
                    <span class="endpoint-desc">List tickets with filters &amp; pagination</span>
                    <button class="copy-btn" onclick="copyPath(this, 'call-tickets.ashx')">Copy</button>
                </div>
                <div class="endpoint">
                    <span class="method post">POST</span>
                    <code class="endpoint-path">call-tickets.ashx</code>
                    <span class="endpoint-desc">Create new ticket</span>
                    <button class="copy-btn" onclick="copyPath(this, 'call-tickets.ashx')">Copy</button>
                </div>
                <div class="endpoint">
                    <span class="method get">GET</span>
                    <code class="endpoint-path">call-ticket-detail.ashx?id={ticketId}</code>
                    <span class="endpoint-desc">Get ticket detail, notes &amp; history</span>
                    <button class="copy-btn" onclick="copyPath(this, 'call-ticket-detail.ashx')">Copy</button>
                </div>
                <div class="endpoint">
                    <span class="method post">POST</span>
                    <code class="endpoint-path">call-ticket-action.ashx</code>
                    <span class="endpoint-desc">Update status / priority / add note</span>
                    <button class="copy-btn" onclick="copyPath(this, 'call-ticket-action.ashx')">Copy</button>
                </div>
                <div class="endpoint">
                    <span class="method get">GET</span>
                    <code class="endpoint-path">call-companies.ashx</code>
                    <span class="endpoint-desc">Lookup — active companies</span>
                    <button class="copy-btn" onclick="copyPath(this, 'call-companies.ashx')">Copy</button>
                </div>
                <div class="endpoint">
                    <span class="method get">GET</span>
                    <code class="endpoint-path">call-departments.ashx?comId={id}</code>
                    <span class="endpoint-desc">Lookup — departments (filtered by company)</span>
                    <button class="copy-btn" onclick="copyPath(this, 'call-departments.ashx')">Copy</button>
                </div>
                <div class="endpoint">
                    <span class="method get">GET</span>
                    <code class="endpoint-path">call-branches.ashx?comId={id}&amp;deptId={id}</code>
                    <span class="endpoint-desc">Lookup — branches (filtered by company)</span>
                    <button class="copy-btn" onclick="copyPath(this, 'call-branches.ashx')">Copy</button>
                </div>
                <div class="endpoint">
                    <span class="method get">GET</span>
                    <code class="endpoint-path">call-employees.ashx?deptId={id}&amp;comId={id}&amp;branchId={id}</code>
                    <span class="endpoint-desc">Lookup — employees by dept/branch (caller candidates)</span>
                    <button class="copy-btn" onclick="copyPath(this, 'call-employees.ashx')">Copy</button>
                </div>
                <div class="endpoint">
                    <span class="method get">GET</span>
                    <code class="endpoint-path">call-it-employees.ashx</code>
                    <span class="endpoint-desc">Lookup — IT staff (ticket assignment candidates)</span>
                    <button class="copy-btn" onclick="copyPath(this, 'call-it-employees.ashx')">Copy</button>
                </div>
            </div>
        </div>

        <!-- Legacy Endpoints -->
        <div class="section">
            <div class="section-header">
                <div class="section-icon" style="background: rgba(100, 116, 139, 0.15);">&#128220;</div>
                <span class="section-title">Legacy Handlers</span>
                <span class="section-desc">Direct .ashx access</span>
            </div>
            <div class="legacy-grid">
                <div class="legacy-item">dbinfo.ashx</div>
                <div class="legacy-item">get-image.ashx?id={id}</div>
                <div class="legacy-item">mobile-claim.ashx</div>
                <div class="legacy-item">mobile-confirm.ashx</div>
                <div class="legacy-item">mobile-cancel.ashx</div>
                <div class="legacy-item">set-images.ashx</div>
                <div class="legacy-item">set-image-upload.ashx</div>
                <div class="legacy-item">call-tickets.ashx</div>
                <div class="legacy-item">call-ticket-detail.ashx</div>
                <div class="legacy-item">call-ticket-action.ashx</div>
                <div class="legacy-item">call-companies.ashx</div>
                <div class="legacy-item">call-departments.ashx</div>
                <div class="legacy-item">call-branches.ashx</div>
                <div class="legacy-item">call-employees.ashx</div>
                <div class="legacy-item">call-it-employees.ashx</div>
            </div>
        </div>

        <!-- Footer -->
        <footer class="footer">
            <div class="footer-links">
                <a href="/api/health" target="_blank">Health Check &#8599;</a>
                <a href="/App_Data/api-error-log.txt" target="_blank">Error Logs &#8599;</a>
            </div>
            <span>&#169; <%= DateTime.Now.Year %> Yakult Inventory System</span>
        </footer>
    </div>

    <!-- Toast Notification -->
    <div class="toast" id="toast">Copied to clipboard!</div>

    <script type="text/javascript">
        function copyPath(btn, path) {
            navigator.clipboard.writeText(path).then(function() {
                btn.classList.add('copied');
                btn.textContent = 'Copied!';
                
                var toast = document.getElementById('toast');
                toast.classList.add('show');
                
                setTimeout(function() {
                    btn.classList.remove('copied');
                    btn.textContent = 'Copy';
                    toast.classList.remove('show');
                }, 2000);
            });
        }
    </script>
</body>
</html>
