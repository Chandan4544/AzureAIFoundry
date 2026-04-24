# Group 3: Implementation Status and Azure Setup

## 1) What We Have Completed So Far

### Implemented code components
- Core data models for orders, returns, session context, request/response, safety assessment, and audit records.
- Simulated backend store with sample order and return fixtures.
- Identity verification service that validates customer_email against order ownership.
- Content safety service with:
  - high-risk detection -> transaction suspension + escalation response
  - frustration detection -> empathetic tone adjustment
- Audit logger that captures structured tool call records and exports JSON.
- Tool schema generator that exports JSON schema definitions for all 4 tools.
- Four custom transactional tools implemented in C#:
  - OrderStatusTool
  - ReturnInitiationTool
  - DeliveryReschedulingTool
  - RefundStatusTool
- Agent orchestrator with policy enforcement:
  - identity verification required before transactional tool calls
  - customer context matching checks
  - content safety check before tool execution
  - structured logging for success/rejection/suspension
- System instruction document authored with authorization and safety policies.

### Generated artifacts
- tool-schemas.json (tool contracts and return formats)
- audit-log-sample.json (sample run/tool invocation audit records)
- SYSTEM_INSTRUCTIONS.md (policy/instruction baseline)

### Build/run verification completed
- Solution build succeeded.
- Console run succeeded.
- Scripted run confirmed:
  - identity verification path
  - successful transactional calls
  - frustration tone adjustment behavior
  - high-risk content suspension and escalation message

## 2) Azure Portal Services to Create (Required)

Create these in Azure Portal for production-like assignment execution with Azure AI Foundry + C#:

1. Azure AI Foundry Project
- Purpose: central workspace for model deployment, agent, tools, evaluation.
- Notes: use one shared project/resource group for team collaboration.

2. Azure OpenAI / Model Deployment (inside Foundry)
- Purpose: host GPT model for the agent runtime.
- Notes: deploy the model version required by your lab/instructor guidance.

3. Azure AI Content Safety resource
- Purpose: text safety scoring and threshold-driven block/escalation decisions.
- Notes: map thresholds in app config for blocked vs tone-adjust-only behavior.

4. Application Insights
- Purpose: telemetry, request traces, exceptions, custom events for tool calls.
- Notes: log tool_name, outcome, latency, thread_id, run_id.

5. Log Analytics Workspace (recommended with App Insights)
- Purpose: centralized querying, dashboards, and investigation.
- Notes: connect Application Insights to this workspace.

6. Azure Storage Account (Blob)
- Purpose: store audit logs, test transcripts, and exported artifacts.
- Notes: keep a container for 10-session evidence and presentation assets.

7. Azure Key Vault
- Purpose: secure secrets (API keys, connection strings, endpoints).
- Notes: do not hardcode secrets in source code or appsettings.

8. Azure App Service (or Azure Container Apps)
- Purpose: host the C# agent API/console wrapper for team demo.
- Notes: choose one hosting option only; App Service is simplest.

9. Azure Monitor Alerts (recommended)
- Purpose: notify on failures, high suspension rate, or exception spikes.
- Notes: add basic alerts for failed requests and high error counts.

## 3) Optional but Useful Services

1. Azure AI Search (if you add knowledge retrieval later)
- Not required for current transactional scope.

2. Azure SQL Database or Cosmos DB
- Use only if moving from simulated fixtures to persisted order/return state.

3. Microsoft Entra ID (App Registration)
- For managed identity/auth if exposing APIs beyond local demo scope.

## 4) Minimal Environment Configuration Needed

Set these settings in Azure/App configuration (or local dev secrets):
- FOUNDRY_PROJECT_ENDPOINT
- FOUNDRY_API_KEY (or managed identity)
- MODEL_DEPLOYMENT_NAME
- CONTENT_SAFETY_ENDPOINT
- CONTENT_SAFETY_KEY
- APPINSIGHTS_CONNECTION_STRING
- STORAGE_CONNECTION_STRING

## 5) Service Ownership Suggestion (7 Members)

- Foundry + model deployment: 1 member
- Content Safety policy and thresholds: 1 member
- App Insights + Monitor + KQL dashboard: 1 member
- Storage + artifact pipeline: 1 member
- Key Vault + config/security: 1 member
- Hosting (App Service/Container Apps): 1 member
- Validation/testing and evidence packaging: 1 member

## 6) Monday-Ready Provisioning Order (Fastest)

1. Resource Group
2. Foundry project + model deployment
3. Content Safety
4. Application Insights (+ Log Analytics)
5. Storage Account
6. Key Vault
7. Host app (App Service/Container Apps)
8. Configure app settings/secrets
9. Smoke test and capture logs/screenshots for submission proof

## 7) Evidence to Capture for Submission

- Portal screenshots of created resources.
- Model deployment screenshot and endpoint details (mask keys).
- Content Safety threshold configuration used.
- App Insights traces showing tool calls and outcomes.
- Sample audit log files from 10 test conversations.
- Brief architecture slide mapping each service to assignment criteria.
