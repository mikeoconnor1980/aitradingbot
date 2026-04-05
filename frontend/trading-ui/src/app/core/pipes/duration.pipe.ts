import { Pipe, PipeTransform } from "@angular/core";

@Pipe({ name: "duration", standalone: true })
export class DurationPipe implements PipeTransform {
  transform(minutes: number | null | undefined): string {
    if (minutes == null || minutes <= 0) {
      return "—";
    }

    const days = Math.floor(minutes / 1440);
    const hours = Math.floor((minutes % 1440) / 60);
    const mins = Math.round(minutes % 60);

    if (days > 0) {
      return hours > 0 ? `${days}d ${hours}h` : `${days}d`;
    }

    if (hours > 0) {
      return mins > 0 ? `${hours}h ${mins}m` : `${hours}h`;
    }

    return `${mins}m`;
  }
}
