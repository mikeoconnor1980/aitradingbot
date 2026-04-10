using 'main.bicep'

param environmentName = 'prod'
param location = 'uksouth'
param appName = 'tradepilot'
param containerImage = 'ghcr.io/OWNER/tradepilot-api:latest'
param sqlAdminLogin = 'tradepilotadmin'
param sqlAdminPassword = readEnvironmentVariable('SQL_ADMIN_PASSWORD', '')
param jwtSecretKey = readEnvironmentVariable('JWT_SECRET_KEY', '')
param llmApiKey = readEnvironmentVariable('LLM_API_KEY', '')
param corsAllowedOrigin = ''
