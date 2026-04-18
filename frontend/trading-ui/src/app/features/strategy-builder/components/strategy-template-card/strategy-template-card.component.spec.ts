import { ComponentFixture, TestBed } from "@angular/core/testing";
import { StrategyTemplateCardComponent } from "./strategy-template-card.component";
import { StrategyTemplateDto } from "../../models/strategy.model";

describe("StrategyTemplateCardComponent", () => {
  let fixture: ComponentFixture<StrategyTemplateCardComponent>;
  let component: StrategyTemplateCardComponent;

  const template: StrategyTemplateDto = {
    id: "template-1",
    slug: "range-reversion-alpha",
    name: "Range Reversion Alpha",
    description: "A removable shared template.",
    strategyMode: "signal",
    direction: "long",
    market: "BTCUSDT",
    tags: ["range"],
    config: {
      strategyName: "Range Reversion Alpha",
      strategyMode: "signal",
      market: "BTCUSDT",
      direction: "long",
    },
    sortOrder: 1,
    isSystemTemplate: false,
    createdAtUtc: Date.now(),
    updatedAtUtc: Date.now(),
  } as StrategyTemplateDto;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [StrategyTemplateCardComponent],
    }).compileComponents();

    fixture = TestBed.createComponent(StrategyTemplateCardComponent);
    component = fixture.componentInstance;
    component.template = template;
    fixture.detectChanges();
  });

  it("should emit clone when the card is selected", () => {
    spyOn(component.clone, "emit");

    component.onClone();

    expect(component.clone.emit).toHaveBeenCalledWith(template);
  });
});