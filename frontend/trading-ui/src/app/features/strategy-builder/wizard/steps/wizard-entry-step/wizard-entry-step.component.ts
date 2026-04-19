import { Component, Input } from "@angular/core";
import { AbstractControl, FormArray, FormGroup } from "@angular/forms";
import { MatIconModule } from "@angular/material/icon";
import { DcaConfigCardComponent } from "../../../components/dca-config-card/dca-config-card.component";
import { GridConfigCardComponent } from "../../../components/grid-config-card/grid-config-card.component";
import { EntryConditionsCardComponent } from "../../../components/entry-conditions-card/entry-conditions-card.component";

@Component({
  selector: "app-wizard-entry-step",
  standalone: true,
  imports: [MatIconModule, DcaConfigCardComponent, GridConfigCardComponent, EntryConditionsCardComponent],
  templateUrl: "./wizard-entry-step.component.html",
  styleUrl: "./wizard-entry-step.component.scss"
})
export class WizardEntryStepComponent {
  @Input({ required: true }) public isSignalMode = false;
  @Input({ required: true }) public isDcaMode = false;
  @Input({ required: true }) public gridGroup!: FormGroup;
  @Input({ required: true }) public dcaGroup!: FormGroup;
  @Input({ required: true }) public conditionsArray!: FormArray;
  @Input({ required: true }) public entryLogicControl!: AbstractControl;
}
