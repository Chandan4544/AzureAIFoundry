# Azure Portal Creation Steps (Monday Fast-Track)

## 1. Create Resource Group
1. Open Azure Portal.
2. Go to Resource groups -> Create.
3. Name: rg-shopaxis-group3.
4. Region: choose same region for all services.
5. Create.

## 2. Create Azure AI Foundry Project
1. Open Azure AI Foundry.
2. Create/Select hub and create a project.
3. Project name: shopaxis-agent-prj.
4. Link it to rg-shopaxis-group3.
5. Confirm project endpoint is available.

## 3. Deploy Model in Foundry
1. In the Foundry project, open Models/Deployments.
2. Deploy the required model (per instructor policy).
3. Set deployment name: gpt-agent-deployment.
4. Save deployment name and endpoint for app config.

## 4. Create Azure AI Content Safety
1. In Portal, create resource: Azure AI Content Safety.
2. Name: cs-shopaxis-group3.
3. Region: same as Foundry.
4. After deploy, copy endpoint and key.

## 5. Create Application Insights
1. Create resource: Application Insights.
2. Name: ai-shopaxis-group3.
3. Region: same as app hosting.
4. Workspace-based mode: enabled.
5. Copy connection string.

## 6. Create Log Analytics Workspace
1. Create resource: Log Analytics Workspace.
2. Name: law-shopaxis-group3.
3. Region: same region.
4. Link Application Insights to this workspace.

## 7. Create Storage Account (Audit Artifacts)
1. Create resource: Storage account.
2. Name: stshopaxisgroup3.
3. Performance: Standard, Redundancy: LRS (for assignment).
4. Create container: audit-logs.
5. Store 10-session logs and evidence exports.

## 8. Create Key Vault (Secrets)
1. Create resource: Key Vault.
2. Name: kv-shopaxis-group3.
3. Add secrets:
- FOUNDRY_API_KEY
- CONTENT_SAFETY_KEY
- APPINSIGHTS_CONNECTION_STRING (optional as secret)
- STORAGE_CONNECTION_STRING
4. Grant team access policy (or RBAC) for project members.

## 9. Create Hosting (choose one)
1. Option A: App Service (fastest)
- Create App Service: app-shopaxis-group3
- Runtime: .NET
2. Option B: Container Apps
- Use only if your team already uses containers.

## 10. Configure App Settings
Set these values in App Service/host configuration:
- FOUNDRY_PROJECT_ENDPOINT
- FOUNDRY_API_KEY (or managed identity)
- MODEL_DEPLOYMENT_NAME
- CONTENT_SAFETY_ENDPOINT
- CONTENT_SAFETY_KEY
- APPINSIGHTS_CONNECTION_STRING
- STORAGE_CONNECTION_STRING

## 11. Smoke Test Checklist
1. App starts without config errors.
2. Identity verification works before tool calls.
3. One successful transaction per tool is logged.
4. One abusive input triggers suspension/escalation.
5. Audit file is written and visible in storage/local export.
6. App Insights shows request + custom tool telemetry.

## 12. Evidence to Capture (for Submission)
1. Screenshot: Foundry project and model deployment.
2. Screenshot: Content Safety resource.
3. Screenshot: App Insights traces for tool calls.
4. Screenshot: Storage container with audit files.
5. Screenshot: Key Vault secrets list (names only, no values).
6. Attach tool-schemas.json and audit-log-sample.json.
