import { HttpInterceptorFn } from "@angular/common/http";
import { inject } from "@angular/core";
import { environment } from "../../../environments/environment";
import { ExchangeContextService } from "../services/exchange-context.service";

export const exchangeInterceptor: HttpInterceptorFn = (req, next) => {
  const exchangeContext = inject(ExchangeContextService);

  if (!req.url.startsWith(environment.apiBaseUrl)) {
    return next(req);
  }

  return next(req.clone({
    setHeaders: {
      "X-Exchange": exchangeContext.exchange
    }
  }));
};