import { TestBed } from "@angular/core/testing";
import { firstValueFrom, of, throwError } from "rxjs";
import { Router } from "@angular/router";
import { adminRoleGuard } from "./admin-role.guard";
import { AuthService } from "../services/auth.service";

describe("adminRoleGuard", () => {
  it("allows admin users", () => {
    TestBed.configureTestingModule({
      providers: [
        { provide: AuthService, useValue: { currentUser: { isAdmin: true }, isAuthenticated: true, syncCurrentUser: () => of({ isAdmin: true }) } },
        { provide: Router, useValue: { createUrlTree: jasmine.createSpy("createUrlTree") } }
      ]
    });

    const result = TestBed.runInInjectionContext(() => adminRoleGuard({} as never, {} as never));

    expect(result).toBeTrue();
  });

  it("allows users who become admin after auth refresh", async () => {
    TestBed.configureTestingModule({
      providers: [
        { provide: AuthService, useValue: { currentUser: { isAdmin: false }, isAuthenticated: true, syncCurrentUser: () => of({ isAdmin: true }) } },
        { provide: Router, useValue: { createUrlTree: jasmine.createSpy("createUrlTree") } }
      ]
    });

    const result = TestBed.runInInjectionContext(() => adminRoleGuard({} as never, {} as never));

    expect(await firstValueFrom(result as ReturnType<typeof of>)).toBeTrue();
  });

  it("redirects non-admin users", async () => {
    const urlTree = { redirectedTo: "/dashboard" } as unknown as ReturnType<Router["createUrlTree"]>;
    const createUrlTree = jasmine.createSpy("createUrlTree").and.returnValue(urlTree);

    TestBed.configureTestingModule({
      providers: [
        { provide: AuthService, useValue: { currentUser: { isAdmin: false }, isAuthenticated: true, syncCurrentUser: () => throwError(() => new Error("forbidden")) } },
        { provide: Router, useValue: { createUrlTree } }
      ]
    });

    const result = TestBed.runInInjectionContext(() => adminRoleGuard({} as never, {} as never));

    expect(await firstValueFrom(result as ReturnType<typeof of>)).toBe(urlTree);
  });
});