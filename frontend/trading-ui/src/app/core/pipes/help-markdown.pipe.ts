import { Pipe, PipeTransform, inject } from "@angular/core";
import { DomSanitizer, SafeHtml } from "@angular/platform-browser";
import { marked } from "marked";

@Pipe({
  name: "helpMarkdown",
  standalone: true
})
export class HelpMarkdownPipe implements PipeTransform {
  private readonly _sanitizer = inject(DomSanitizer);

  public transform(value: string | null | undefined): SafeHtml {
    if (!value) {
      return "";
    }
    const html = marked.parse(value, { async: false }) as string;
    return this._sanitizer.bypassSecurityTrustHtml(html);
  }
}
