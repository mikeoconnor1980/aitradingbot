import { Pipe, PipeTransform } from "@angular/core";

@Pipe({
  name: "relativeTime",
  standalone: true
})
export class RelativeTimePipe implements PipeTransform {
  public transform(value: string | null | undefined, _refreshToken?: number): string {
    if (!value) {
      return "";
    }

    const date = new Date(value);
    const timestamp = date.getTime();

    if (Number.isNaN(timestamp)) {
      return "";
    }

    const now = Date.now();
    const diffMs = now - timestamp;

    if (diffMs < 0) {
      return "just now";
    }

    const seconds = Math.floor(diffMs / 1000);
    if (seconds < 60) {
      return "just now";
    }

    const minutes = Math.floor(seconds / 60);
    if (minutes < 60) {
      return `${minutes}m ago`;
    }

    const hours = Math.floor(minutes / 60);
    if (hours < 24) {
      return `${hours}h ago`;
    }

    const days = Math.floor(hours / 24);
    return `${days}d ago`;
  }
}
