using 'main.bicep'

param environmentName = 'prod'
param location = 'uksouth'
param appName = 'tradepilot'
param containerImage = 'ghcr.io/OWNER/tradepilot-api:latest'
param sqlAdminLogin = 'tradepilotadmin'
param sqlAdminPassword = readEnvironmentVariable('SQL_ADMIN_PASSWORD', '')
param corsAllowedOrigin = ''
