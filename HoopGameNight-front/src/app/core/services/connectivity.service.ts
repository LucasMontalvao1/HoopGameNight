import { Injectable, signal, inject, PLATFORM_ID } from '@angular/core';
import { isPlatformBrowser } from '@angular/common';
import { fromEvent, merge, map, startWith } from 'rxjs';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { NotificationService } from './notification.service';

@Injectable({
    providedIn: 'root'
})
export class ConnectivityService {
    private readonly notificationService = inject(NotificationService);
    private readonly platformId = inject(PLATFORM_ID);

    private readonly _isOnline = signal<boolean>(true);
    readonly isOnline = this._isOnline.asReadonly();

    constructor() {
        if (isPlatformBrowser(this.platformId)) {
            this._isOnline.set(navigator.onLine);

            merge(
                fromEvent(window, 'online').pipe(map(() => true)),
                fromEvent(window, 'offline').pipe(map(() => false))
            ).pipe(
                takeUntilDestroyed()
            ).subscribe(online => {
                this._isOnline.set(online);
                if (online) {
                    this.notificationService.success('Conexão restabelecida!', 3000);
                } else {
                    this.notificationService.warning('Você está offline. Algumas funcionalidades podem não estar disponíveis.', 0);
                }
            });
        }
    }
}
