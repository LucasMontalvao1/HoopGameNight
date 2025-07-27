import { Injectable, signal, effect } from '@angular/core';

export type Theme = 'light' | 'dark' | 'system';

@Injectable({
  providedIn: 'root'
})
export class ThemeService {
  private readonly THEME_KEY = 'app-theme';
  
  private readonly _theme = signal<Theme>(this.getInitialTheme());
  private readonly _isDark = signal<boolean>(false);
  
  readonly theme = this._theme.asReadonly();
  readonly isDark = this._isDark.asReadonly();

  constructor() {
    effect(() => {
      this.applyTheme();
    });

    effect(() => {
      localStorage.setItem(this.THEME_KEY, this._theme());
    });

    this.watchSystemTheme();
  }

  setTheme(theme: Theme): void {
    this._theme.set(theme);
  }

  toggleTheme(): void {
    const current = this._theme();
    if (current === 'system') {
      this.setTheme('light');
    } else {
      this.setTheme(current === 'light' ? 'dark' : 'light');
    }
  }

  private getInitialTheme(): Theme {
    const saved = localStorage.getItem(this.THEME_KEY) as Theme;
    return saved || 'system';
  }

  private applyTheme(): void {
    const theme = this._theme();
    const isDark = this.calculateIsDark(theme);
    
    this._isDark.set(isDark);
    
    document.documentElement.classList.remove('light', 'dark');
    document.documentElement.classList.add(isDark ? 'dark' : 'light');

    const metaTheme = document.querySelector('meta[name="theme-color"]');
    if (metaTheme) {
      metaTheme.setAttribute('content', isDark ? '#1f2937' : '#ffffff');
    }
  }

  private calculateIsDark(theme: Theme): boolean {
    if (theme === 'system') {
      return window.matchMedia('(prefers-color-scheme: dark)').matches;
    }
    return theme === 'dark';
  }

  private watchSystemTheme(): void {
    const mediaQuery = window.matchMedia('(prefers-color-scheme: dark)');
    mediaQuery.addEventListener('change', () => {
      if (this._theme() === 'system') {
        this.applyTheme();
      }
    });
  }
}