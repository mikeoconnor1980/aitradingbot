import { Component } from "@angular/core";
import { ComponentFixture, TestBed } from "@angular/core/testing";
import { RelativeTimePipe } from "./relative-time.pipe";

@Component({
  standalone: true,
  imports: [RelativeTimePipe],
  template: "<span class=\"value\">{{ timestamp | relativeTime:refreshToken }}</span>"
})
class TestHostComponent {
  public refreshToken = 0;
  public timestamp = "2026-04-19T12:00:00.000Z";
}

describe("RelativeTimePipe", () => {
  let fixture: ComponentFixture<TestHostComponent>;
  let now = Date.parse("2026-04-19T12:00:30.000Z");

  beforeEach(async () => {
    spyOn(Date, "now").and.callFake(() => now);

    await TestBed.configureTestingModule({
      imports: [TestHostComponent]
    }).compileComponents();

    fixture = TestBed.createComponent(TestHostComponent);
  });

  it("should refresh rendered output when the refresh token changes", () => {
    fixture.detectChanges();
    expect(getRenderedValue()).toBe("just now");

    now = Date.parse("2026-04-19T12:01:05.000Z");
    fixture.componentInstance.refreshToken += 1;
    fixture.detectChanges();

    expect(getRenderedValue()).toBe("1m ago");
  });

  function getRenderedValue(): string {
    return (fixture.nativeElement as HTMLElement).querySelector(".value")?.textContent?.trim() ?? "";
  }
});