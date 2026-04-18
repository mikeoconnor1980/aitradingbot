import { TestBed } from "@angular/core/testing";
import { Router } from "@angular/router";
import { adminRoleGuard } from "./admin-role.guard";
import { AuthService } from "../services/auth.service";

describe("adminRoleGuard", () => {
  it("allows admin users", () => {
    TestBed.configureTestingModule({
      providers: [
        { provide: AuthService, useValue: { currentUser: { isAdmin: true } } },
        { provide: Router, useValue: { createUrlTree: jasmine.createSpy("createUrlTree") } }
      ]
    });

    const result = TestBed.runInInjectionContext(() => adminRoleGuard({} as never, {} as never));

    expect(result).toBeTrue();
  });

  it("redirects non-admin users", () => {
    const urlTree = { redirectedTo: "/dashboard" } as unknown as ReturnType<Router["createUrlTree"]>;
    const createUrlTree = jasmine.createSpy("createUrlTree").and.returnValue(urlTree);

    TestBed.configureTestingModule({
      providers: [
        { provide: AuthService, useValue: { currentUser: { isAdmin: false } } },
        { provide: Router, useValue: { createUrlTree } }
      ]
    });

    const result = TestBed.runInInjectionContext(() => adminRoleGuard({} as never, {} as never));

    expect(createUrlTree).toHaveBeenCalledWith(["/dashboard"]);
    expect(result as unknown).toBe(urlTree);
  });
});