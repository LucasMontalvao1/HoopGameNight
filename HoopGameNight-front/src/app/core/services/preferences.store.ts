import { Injectable, signal, computed, effect, inject } from '@angular/core';
import { StorageService } from './storage.service';

export interface UserPreferences {
    favoriteTeamIds: number[];
    favoritePlayerIds: number[];
    theme: 'light' | 'dark' | 'system';
    compactMode: boolean;
}

@Injectable({
    providedIn: 'root'
})
export class PreferencesStore {
    private readonly storageService = inject(StorageService);
    private readonly STORAGE_KEY = 'hgn_preferences';

    // State
    private readonly _state = signal<UserPreferences>({
        favoriteTeamIds: [],
        favoritePlayerIds: [],
        theme: 'dark',
        compactMode: false
    });

    // Selectors
    readonly state = this._state.asReadonly();
    readonly favoriteTeamIds = computed(() => this._state().favoriteTeamIds);
    readonly favoritePlayerIds = computed(() => this._state().favoritePlayerIds);
    readonly theme = computed(() => this._state().theme);

    constructor() {
        this.loadFromStorage();

        // Auto-save effect
        effect(() => {
            this.storageService.setAppDataSync('core', this.STORAGE_KEY, this._state());
        });
    }

    toggleFavoriteTeam(teamId: number): void {
        this._state.update(s => {
            const isFav = s.favoriteTeamIds.includes(teamId);
            return {
                ...s,
                favoriteTeamIds: isFav
                    ? s.favoriteTeamIds.filter(id => id !== teamId)
                    : [...s.favoriteTeamIds, teamId]
            };
        });
    }

    isFavoriteTeam(teamId: number): boolean {
        return this._state().favoriteTeamIds.includes(teamId);
    }

    toggleFavoritePlayer(playerId: number): void {
        this._state.update(s => {
            const isFav = s.favoritePlayerIds.includes(playerId);
            return {
                ...s,
                favoritePlayerIds: isFav
                    ? s.favoritePlayerIds.filter(id => id !== playerId)
                    : [...s.favoritePlayerIds, playerId]
            };
        });
    }

    isFavoritePlayer(playerId: number): boolean {
        return this._state().favoritePlayerIds.includes(playerId);
    }

    setTheme(theme: UserPreferences['theme']): void {
        this._state.update(s => ({ ...s, theme }));
    }

    private loadFromStorage(): void {
        const saved = this.storageService.getAppDataSync<UserPreferences>('core', this.STORAGE_KEY);
        if (saved) {
            this._state.set(saved);
        }
    }
}
