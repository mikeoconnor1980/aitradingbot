import { Component, DestroyRef, inject, OnInit, signal } from "@angular/core";
import { takeUntilDestroyed } from "@angular/core/rxjs-interop";
import { MatButtonModule } from "@angular/material/button";
import { MatCardModule } from "@angular/material/card";
import { MatIconModule } from "@angular/material/icon";
import { MatProgressBarModule } from "@angular/material/progress-bar";
import { interval, switchMap, takeWhile, tap } from "rxjs";
import { TelegramService, TelegramStatusResponse } from "../../core/services/telegram.service";
import { NotificationService } from "../../core/services/notification.service";
import { Clipboard } from "@angular/cdk/clipboard";
import QRCode from "qrcode";

@Component({
  selector: "app-telegram-link",
  standalone: true,
  imports: [MatCardModule, MatButtonModule, MatIconModule, MatProgressBarModule],
  templateUrl: "./telegram-link.component.html",
  styleUrl: "./telegram-link.component.scss"
})
export class TelegramLinkComponent implements OnInit {
  private readonly _destroyRef = inject(DestroyRef);
  private readonly _telegramService = inject(TelegramService);
  private readonly _notification = inject(NotificationService);
  private readonly _clipboard = inject(Clipboard);

  public readonly status = signal<TelegramStatusResponse | null>(null);
  public readonly linkCode = signal<string | null>(null);
  public readonly botUsername = signal<string | null>(null);
  public readonly qrCodeDataUrl = signal<string | null>(null);
  public readonly expiresAt = signal<Date | null>(null);
  public readonly loading = signal(false);
  public readonly polling = signal(false);

  public ngOnInit(): void {
    this._loadStatus();
  }

  public onGenerateCode(): void {
    this.loading.set(true);
    this._telegramService.generateLinkCode().subscribe({
      next: (response) => {
        this.linkCode.set(response.code);
        this.botUsername.set(response.botUsername || null);
        this.expiresAt.set(new Date(response.expiresAtUtc));
        this.loading.set(false);
        this._generateQrCode(response.botUsername);
        this._startPolling();
      },
      error: () => {
        this._notification.error("Failed to generate link code");
        this.loading.set(false);
      }
    });
  }

  public onCopyCode(): void {
    const code = this.linkCode();
    if (code) {
      this._clipboard.copy(`/link ${code}`);
      this._notification.success("Copied to clipboard");
    }
  }

  public onUnlink(): void {
    this.loading.set(true);
    this._telegramService.unlink().subscribe({
      next: () => {
        this.status.set({ linked: false, chatId: null });
        this.linkCode.set(null);
        this.loading.set(false);
        this._notification.success("Telegram unlinked");
      },
      error: () => {
        this._notification.error("Failed to unlink Telegram");
        this.loading.set(false);
      }
    });
  }

  public onSendTest(): void {
    this.loading.set(true);
    this._telegramService.sendTest().subscribe({
      next: () => {
        this.loading.set(false);
        this._notification.success("Test notification sent — check Telegram!");
      },
      error: () => {
        this._notification.error("Failed to send test notification");
        this.loading.set(false);
      }
    });
  }

  private _loadStatus(): void {
    this._telegramService.getStatus().subscribe({
      next: (status) => this.status.set(status),
      error: () => this.status.set({ linked: false, chatId: null })
    });
  }

  private _startPolling(): void {
    this.polling.set(true);

    interval(5000).pipe(
      takeUntilDestroyed(this._destroyRef),
      takeWhile(() => this.polling() && !this.status()?.linked),
      switchMap(() => this._telegramService.getStatus()),
      tap((status) => {
        if (status.linked) {
          this.status.set(status);
          this.linkCode.set(null);
          this.polling.set(false);
          this._notification.success("Telegram linked successfully!");
        }

        // Stop polling if code expired
        const expires = this.expiresAt();
        if (expires && new Date() > expires) {
          this.linkCode.set(null);
          this.polling.set(false);
        }
      })
    ).subscribe();
  }

  private _generateQrCode(botUsername: string): void {
    if (!botUsername) {
      this.qrCodeDataUrl.set(null);
      return;
    }
    const url = `https://t.me/${botUsername}`;
    QRCode.toDataURL(url, {
      width: 160,
      margin: 1,
      color: { dark: "#f8fafc", light: "#00000000" }
    }).then((dataUrl: string) => {
      this.qrCodeDataUrl.set(dataUrl);
    }).catch(() => {
      this.qrCodeDataUrl.set(null);
    });
  }
}
