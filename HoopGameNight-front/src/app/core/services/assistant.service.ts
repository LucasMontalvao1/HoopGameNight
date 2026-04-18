import { Injectable, signal, inject } from '@angular/core';
import { AskApiService } from './ask-api.service';
import { AskResponse } from '../interfaces/api.interface';

export interface ChatMessage {
    role: 'user' | 'assistant';
    content: string;
    parsedContent?: string;
    timestamp: Date;
    gamesAnalyzed?: number;
}

@Injectable({
    providedIn: 'root'
})
export class AssistantService {
    private readonly apiService = inject(AskApiService);

    private readonly _messages = signal<ChatMessage[]>([]);
    private readonly _isTyping = signal<boolean>(false);
    private readonly _error = signal<string | null>(null);

    readonly messages = this._messages.asReadonly();
    readonly isTyping = this._isTyping.asReadonly();
    readonly error = this._error.asReadonly();

    async sendMessage(content: string): Promise<void> {
        if (!content.trim()) return;

        const userMsg: ChatMessage = {
            role: 'user',
            content,
            timestamp: new Date()
        };
        this._messages.set([...this._messages(), userMsg]);
        this._isTyping.set(true);
        this._error.set(null);

        try {
            const response = await this.apiService.ask(content);
            const { marked } = await import('marked');

            const parsed = await marked.parse(response.answer);

            const assistantMsg: ChatMessage = {
                role: 'assistant',
                content: response.answer,
                parsedContent: parsed as string,
                timestamp: new Date(),
                gamesAnalyzed: response.gamesAnalyzed
            };

            this._messages.set([...this._messages(), assistantMsg]);
        } catch (err: any) {
            console.error('Error in Assistant:', err);
            this._error.set(err.error?.message || 'O Assistente está ocupado ou offline. Tente novamente em instantes.');
        } finally {
            this._isTyping.set(false);
        }
    }

    clearChat(): void {
        this._messages.set([]);
        this._error.set(null);
    }
}
