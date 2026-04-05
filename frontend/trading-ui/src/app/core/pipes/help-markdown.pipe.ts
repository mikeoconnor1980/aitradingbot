import { Pipe, PipeTransform } from "@angular/core";
import { DomSanitizer, SafeHtml } from "@angular/platform-browser";
import { marked } from "marked";

@Pipe({
  name: "helpMarkdown",
  standalone: true
})
export class HelpMarkdownPipe implements PipeTransform {
  private readonly _sanitizer: DomSanitizer;

  public constructor(sanitizer: DomSanitizer) {
    this._sanitizer = sanitizer;
  }

  public transform(value: string | null | undefined): SafeHtml {
    if (!value) {
      return "";
    }
    const html = marked.parse(value, { async: false }) as string;
    return this._sanitizer.bypassSecurityTrustHtml(html);
  }
}
