NITTE Cloud Workshop - Day 1 sample code (TESTED VERSION, Oct 2026)
webapp/index.html             Web App page (edit name, version, FUNCTION_BASE_URL)
students.sql                  Run in Azure SQL Query editor
functions/hello/              HTTP function   -> paste index.js; check function.json authLevel = anonymous
functions/heartbeat/          Timer function  -> paste index.js (template already has the schedule)
functions/students/           HTTP + SQL      -> replace BOTH index.js and function.json
functions/_reference/host.json  Compare with Function App > App files > host.json (bundle must be [4.*, 5.0.0))
DEPLOY_COMMANDS.txt           PowerShell / Cloud Shell deploy + curl test commands

Function App must be: Consumption plan, Windows, Node.js (portal code editing).
App setting required: SqlConnectionString (App settings tab). Optional: GREETING.
CORS: https://portal.azure.com  +  your Web App URL (no trailing slash).
