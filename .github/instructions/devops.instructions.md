---
applyTo: "**/pipelines/**,**/charts/**,**/Dockerfile,**/docker-compose.yml,**/.yaml,**/.yml"
---

# DevOps Guidelines

CI/CD pipelines, Docker, Helm charts, and deployment patterns.

## Azure DevOps Pipeline Structure

<!-- DOT-CUSTOM-START:devops-pipeline-structure -->
```
pipelines/
├── azure-pipelines.yml        # Main pipeline
├── deploy/
│   └── deployment.yml         # Deployment template
├── infrastructure/
│   └── azuredeploy.bicep      # IaC
└── ui-tests/
    └── run-tests.yml          # Acceptance tests
```
<!-- DOT-CUSTOM-END:devops-pipeline-structure -->

## Pipeline Stages

The main pipeline follows this flow:

1. **Build** - Build, test, scan, and create Docker images
2. **Deploy_Local** - Feature branch infrastructure (from `features/*`)
3. **Deploy_Dev** - Automatic from `main`
4. **Acceptance_Tests** - Run after Dev deployment
5. **Deploy_QA** - Manual approval
6. **Deploy_Staging** - Manual approval
7. **Deploy_Production** - Manual approval

## Branch Strategy

<!-- DOT-CUSTOM-START:branch-strategy -->
```yaml
trigger:
  branches:
    include:
    - main
    - hotfix/*
    - release/*
```

- `main` - Deploys through all environments
- `features/*` - Feature branches for trunk-based development. Used to deploy changes to local Key Vault.
- `hotfix/*` - Fast-track to Staging and Production
- `release/*` - Controlled releases
<!-- DOT-CUSTOM-END:branch-strategy -->

## Docker Best Practices

### Multi-Stage Builds

```dockerfile
# Build stage
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/DTS.UKCT.Efiling.Api/DTS.UKCT.Efiling.Api.csproj", "src/DTS.UKCT.Efiling.Api/"]
RUN dotnet restore "src/DTS.UKCT.Efiling.Api/DTS.UKCT.Efiling.Api.csproj"
COPY . .
RUN dotnet build "src/DTS.UKCT.Efiling.Api/DTS.UKCT.Efiling.Api.csproj" -c Release -o /app/build

# Publish stage
FROM build AS publish
RUN dotnet publish "src/DTS.UKCT.Efiling.Api/DTS.UKCT.Efiling.Api.csproj" -c Release -o /app/publish

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS final
WORKDIR /app
COPY --from=publish /app/publish .
EXPOSE 80
ENTRYPOINT ["dotnet", "DTS.UKCT.Efiling.Api.dll"]
```

### Docker Compose Profiles

```yaml
# docker-compose.yml
services:
  sqlserver:
    image: mcr.microsoft.com/mssql/server:2019-latest
    profiles: ["dev"]
    environment:
      SA_PASSWORD: "Admin1234!"
      ACCEPT_EULA: "Y"
    ports:
      - "1433:1433"

  al-dbup:
    image: dtsuksouthdevacr.azurecr.io/al-dbup:latest
    profiles: ["al"]
    depends_on:
      - sqlserver
```

Usage:
```bash
# Start dev services
docker-compose --profile dev up -d

# Start with AL migration
docker-compose --profile al up

# Stop and remove volumes
docker-compose --profile dev down -v
```

## Helm Chart Structure

```
charts/
├── api/
│   ├── Chart.yaml
│   ├── values.yaml
│   └── templates/
│       ├── deployment.yaml
│       ├── service.yaml
│       └── ingress.yaml
```

### Integration Chart Configuration

```yaml
# charts/integration/values.yaml
deploy:
  acrServer: "dtsnortheuropedevacr.azurecr.io"
  imageTag: "20200430.4"
  vaultName: "dts-p2-dev-kv"

# Event handlers with auto-scaling
events:
  - name: upload-events
    type: UploadEventsService
    scaleSubscription: UploadCreatedIntegrationEvent
    minReplicas: 2
    requests:
      memory: "512Mi"
      cpu: "500m"
    limits:
      memory: "3Gi"
      cpu: "1"
    COMPlus_GCHeapHardLimit:
      value: "3145728000" #3GB - 3000*1024*1024 - Should match with Memory limit

# Scheduled jobs
jobs:
  daily-digest-job:
    type: DailyDigestJob
    schedule: "0 7 * * *"  # Daily at 7am UTC
```

### Keda ScaledObject

Events with `scaleSubscription` get auto-scaling based on Service Bus queue depth:

```yaml
events:
  - name: my-events
    type: MyEventsService
    scaleSubscription: MyIntegrationEvent  # Creates Keda ScaledObject
    messageCount: 3                        # Messages to trigger scale-out
    minReplicas: 1                         # Can scale to 0 if no messages
    maxReplicas: 5
```

## Variable Groups

Configure in Azure DevOps Library:

| Group | Variables |
|-------|-----------|
| DTS.UKCT.Efiling.NonProd | DatabaseAdminPassword, PlatformsClientSecret |
| DTS.UKCT.Efiling.Development | AbstractionLayerEmeaServiceBusConnectionString |
| DTS.UKCT.Efiling.QA | AbstractionLayerEmeaServiceBusConnectionString |
| DTS.UKCT.Efiling.Production | DatabaseAdminPassword, PlatformsClientSecret |
| AutomationUsers | User1Username, UserPassword |
| PAT.Code | PAT |

## Image Promotion

Images are built once and promoted through environments:

```yaml
# Build in Dev
- template: containers/build_images.yml@templates
  parameters:
    PROFILE: server

# Promote to QA
- parameters:
    promoteFromAcrServer: "dtsnortheuropedevacr.azurecr.io"
    promoteFromAcrServerConnection: "dtsnortheuropedevacr"
```

## Deployment Parameters

```yaml
- template: ./deploy/deployment.yml
  parameters:
    environmentName: "Development"
    environmentTag: "dev"
    azureServiceConnection: "DTS.UKCT.Efiling Non-Prod"
    azureResourceGroup: "dtsk8s-northeurope-dev-rg"
    kubernetesCluster: "dtsk8s-northeurope-dev-aks"
    acrServerConnection: "dtsnortheuropedevacr"
    defaults:
      imageTag: "$(Build.BuildNumber)"
      acrServer: "dtsnortheuropedevacr.azurecr.io"
      namespace: 'p2'
```

## Quality Gates

### SonarQube

```yaml
- template: dotnet/dotnet_test.yml@templates
  parameters:
    PROJECT_NAME: 'UK CT'
    PROJECT_KEY: 'UK CT'
    SONAR_EXCLUSIONS: '**/Migrations/**'
    SONAR_COVERAGE_EXCLUSIONS: '**/Program.cs,**/ClientApp/**'
```

### Snyk Security Scan

```yaml
- template: snyk/snyk_code.yml@templates
  parameters:
    DOTNET_SOLUTION: DTS.UKCT.Efiling.sln
    ANGULAR_DIRECTORY: ./src/DTS.UKCT.Efiling.Web/ClientApp
```

### Linting

```yaml
# Dockerfile linting
- template: containers/docker_lint.yml@templates

# Helm linting
- template: containers/helm_lint.yml@templates

# Code style
- script: 'dotnet format DTS.UKCT.Efiling.sln --verify-no-changes'
```

## Local Development Commands

```bash
# Start local services
docker-compose --profile dev up -d

# Stop services
docker-compose --profile dev down

# Stop and delete data
docker-compose --profile dev down -v

# Run Abstraction Layer migration
docker-compose --profile al up
```

## Kubernetes Access

```bash
# Login to cluster
az aks get-credentials --resource-group dtsk8s-northeurope-dev-rg --name dtsk8s-northeurope-dev-aks

# Check pods
kubectl get pods -n p2

# View logs
kubectl logs -n p2 deployment/api

# Port forward
kubectl port-forward -n p2 svc/api 5000:80
```