import { provideHttpClient, withInterceptors } from "@angular/common/http";
import { ApplicationConfig, provideZoneChangeDetection } from "@angular/core";
import { DateAdapter, MAT_DATE_FORMATS, MAT_DATE_LOCALE, NativeDateAdapter } from "@angular/material/core";
import { provideAnimationsAsync } from "@angular/platform-browser/animations/async";
import { provideRouter } from "@angular/router";
import { authInterceptor } from "./core/interceptors/auth.interceptor";
import { errorInterceptor } from "./core/interceptors/error.interceptor";
import { exchangeInterceptor } from "./core/interceptors/exchange.interceptor";
import { routes } from "./app.routes";

const APP_DATE_FORMATS = {
  parse: {
    dateInput: { day: "numeric", month: "short", year: "numeric" }
  },
  display: {
    dateInput: { day: "numeric", month: "short", year: "numeric" },
    monthYearLabel: { month: "short", year: "numeric" },
    dateA11yLabel: { day: "numeric", month: "long", year: "numeric" },
    monthYearA11yLabel: { month: "long", year: "numeric" }
  }
};

export const appConfig: ApplicationConfig = {
  providers: [
    provideZoneChangeDetection({ eventCoalescing: true }),
    provideHttpClient(withInterceptors([authInterceptor, exchangeInterceptor, errorInterceptor])),
    provideAnimationsAsync(),
    { provide: MAT_DATE_LOCALE, useValue: "en-GB" },
    { provide: DateAdapter, useClass: NativeDateAdapter },
    { provide: MAT_DATE_FORMATS, useValue: APP_DATE_FORMATS },
    provideRouter(routes)
  ]
};
