import { TestBed } from "@angular/core/testing";
import { ConditionFactoryService } from "./condition-factory.service";

describe("ConditionFactoryService", () => {
  let service: ConditionFactoryService;

  beforeEach(() => {
    TestBed.configureTestingModule({});
    service = TestBed.inject(ConditionFactoryService);
  });

  describe("createRsiCondition", () => {
    it("should create a form group with RSI defaults", () => {
      const group = service.createRsiCondition();

      expect(group.get("type")?.value).toBe("rsi");
      expect(group.get("period")?.value).toBe(14);
      expect(group.get("operator")?.value).toBe("lt");
      expect(group.get("value")?.value).toBe(40);
      expect(group.get("enabled")?.value).toBeTrue();
      expect(group.get("label")?.value).toBe("");
    });

    it("should apply overrides", () => {
      const group = service.createRsiCondition({
        period: 7,
        operator: "gte",
        value: 70,
        label: "Overbought",
      });

      expect(group.get("period")?.value).toBe(7);
      expect(group.get("operator")?.value).toBe("gte");
      expect(group.get("value")?.value).toBe(70);
      expect(group.get("label")?.value).toBe("Overbought");
    });

    it("should generate unique IDs", () => {
      const groupOne = service.createRsiCondition();
      const groupTwo = service.createRsiCondition();

      expect(groupOne.get("id")?.value).not.toBe(groupTwo.get("id")?.value);
    });

    it("should invalidate period values less than 1", () => {
      const group = service.createRsiCondition({ period: 0 });

      expect(group.get("period")?.valid).toBeFalse();
    });

    it("should invalidate values greater than 100", () => {
      const group = service.createRsiCondition({ value: 101 });

      expect(group.get("value")?.valid).toBeFalse();
    });

    it("should invalidate values less than 0", () => {
      const group = service.createRsiCondition({ value: -1 });

      expect(group.get("value")?.valid).toBeFalse();
    });

    it("should accept a value of exactly 0", () => {
      const group = service.createRsiCondition({ value: 0 });

      expect(group.get("value")?.valid).toBeTrue();
    });

    it("should accept a value of exactly 100", () => {
      const group = service.createRsiCondition({ value: 100 });

      expect(group.get("value")?.valid).toBeTrue();
    });
  });
});