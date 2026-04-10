// Azure deployment environment — URLs are replaced by CI/CD pipeline
export const environment = {
  production: true,
  apiBaseUrl: "https://CONTAINER_APP_FQDN/api",
  hubBaseUrl: "https://CONTAINER_APP_FQDN/hubs/marketdata",
  appVersion: "0.1.0"
};
