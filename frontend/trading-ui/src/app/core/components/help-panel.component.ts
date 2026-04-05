import { CommonModule } from "@angular/common";
import {
  Component,
  DestroyRef,
  ElementRef,
  OnInit,
  ViewChild,
  inject
} from "@angular/core";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { FormsModule } from "@angular/forms";
import { MatButtonModule } from "@angular/material/button";
import { MatIconModule } from "@angular/material/icon";
import { MatTooltipModule } from "@angular/material/tooltip";
import { HelpChatMessage, HelpTopic } from "../models/help-topic.model";
import { HelpMarkdownPipe } from "../pipes/help-markdown.pipe";
import { HelpService } from "../services/help.service";

@Component({
  selector: "app-help-panel",
  standalone: true,
  imports: [CommonModule, FormsModule, MatIconModule, MatButtonModule, MatTooltipModule, HelpMarkdownPipe],
  templateUrl: "./help-panel.component.html",
  styleUrl: "./help-panel.component.scss"
})
export class HelpPanelComponent implements OnInit {
  private readonly _helpService = inject(HelpService);
  private readonly _destroyRef = inject(DestroyRef);

  @ViewChild("chatScrollContainer")
  public chatScrollContainer!: ElementRef<HTMLDivElement>;

  public isOpen = false;
  public activeTopic: HelpTopic | null = null;
  public chatMessages: HelpChatMessage[] = [];
  public chatLoading = false;
  public chatInput = "";
  public showChat = false;
  public topics: HelpTopic[] = [];

  public ngOnInit(): void {
    this.topics = this._helpService.topics;

    this._helpService.open$
      .pipe(takeUntilDestroyed(this._destroyRef))
      .subscribe((open: boolean) => {
        this.isOpen = open;
      });

    this._helpService.activeTopic$
      .pipe(takeUntilDestroyed(this._destroyRef))
      .subscribe((topic: HelpTopic | null) => {
        this.activeTopic = topic;
      });

    this._helpService.chatMessages$
      .pipe(takeUntilDestroyed(this._destroyRef))
      .subscribe((messages: HelpChatMessage[]) => {
        this.chatMessages = messages;
        this.scrollChatToBottom();
      });

    this._helpService.chatLoading$
      .pipe(takeUntilDestroyed(this._destroyRef))
      .subscribe((loading: boolean) => {
        this.chatLoading = loading;
      });
  }

  public onClose(): void {
    this._helpService.close();
    this.showChat = false;
  }

  public onSelectTopic(topic: HelpTopic): void {
    this._helpService.selectTopic(topic);
  }

  public onBackToTopics(): void {
    this._helpService.clearTopic();
  }

  public onToggleChat(): void {
    this.showChat = !this.showChat;
    if (this.showChat) {
      this.scrollChatToBottom();
    }
  }

  public onSendMessage(): void {
    const message = this.chatInput.trim();
    if (!message || this.chatLoading) {
      return;
    }
    this.chatInput = "";
    this._helpService.sendChatMessage(message);
  }

  public onChatKeydown(event: KeyboardEvent): void {
    if (event.key === "Enter" && !event.shiftKey) {
      event.preventDefault();
      this.onSendMessage();
    }
  }

  public onOverlayClick(): void {
    this.onClose();
  }

  private scrollChatToBottom(): void {
    setTimeout(() => {
      if (this.chatScrollContainer?.nativeElement) {
        const el = this.chatScrollContainer.nativeElement;
        el.scrollTop = el.scrollHeight;
      }
    });
  }
}
