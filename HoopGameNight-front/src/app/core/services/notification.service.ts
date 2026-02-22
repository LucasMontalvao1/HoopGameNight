import { Injectable, signal } from '@angular/core';

export interface Notification {
    id: number;
    message: string;
    type: 'success' | 'error' | 'warning' | 'info';
    duration?: number;
}

@Injectable({
    providedIn: 'root'
})
export class NotificationService {
    private readonly _notifications = signal<Notification[]>([]);
    readonly notifications = this._notifications.asReadonly();
    private nextId = 0;

    show(message: string, type: Notification['type'] = 'info', duration: number = 4000): void {
        const id = this.nextId++;
        const notification: Notification = { id, message, type, duration };

        this._notifications.update(prev => [...prev, notification]);

        if (duration > 0) {
            setTimeout(() => this.dismiss(id), duration);
        }
    }

    success(message: string, duration?: number): void {
        this.show(message, 'success', duration);
    }

    error(message: string, duration?: number): void {
        this.show(message, 'error', duration);
    }

    warning(message: string, duration?: number): void {
        this.show(message, 'warning', duration);
    }

    info(message: string, duration?: number): void {
        this.show(message, 'info', duration);
    }

    dismiss(id: number): void {
        this._notifications.update(prev => prev.filter(n => n.id !== id));
    }
}
