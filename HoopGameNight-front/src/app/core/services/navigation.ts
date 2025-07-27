import { Injectable, signal } from '@angular/core';

export interface NavigationItem {
  id: string;
  label: string;
  icon: string;
  route: string;
  badge?: number;
  disabled?: boolean;
}

@Injectable({
  providedIn: 'root'
})
export class NavigationService {
  private readonly _isMobileMenuOpen = signal<boolean>(false);
  private readonly _navigationItems = signal<NavigationItem[]>([
    {
      id: 'dashboard',
      label: 'Dashboard',
      icon: 'dashboard',
      route: '/dashboard'
    },
    {
      id: 'games',
      label: 'Games',
      icon: 'sports_basketball',
      route: '/games'
    },
    
    {
      id: 'teams',
      label: 'Teams',
      icon: 'groups',
      route: '/teams'
    },

    {
      id: 'api-status',
      label: 'API Status',
      icon: 'monitor_heart',
      route: '/api-status'
    }

    // {
    //   id: 'players',
    //   label: 'Players',
    //   icon: 'person',
    //   route: '/players'
    // },
    // {
    //   id: 'settings',
    //   label: 'Settings',
    //   icon: 'settings',
    //   route: '/settings'
    // }
  ]);

  readonly isMobileMenuOpen = this._isMobileMenuOpen.asReadonly();
  readonly navigationItems = this._navigationItems.asReadonly();

  toggleMobileMenu(): void {
    this._isMobileMenuOpen.update(current => !current);
  }

  closeMobileMenu(): void {
    this._isMobileMenuOpen.set(false);
  }

  updateBadge(itemId: string, count: number): void {
    this._navigationItems.update(items => 
      items.map(item => 
        item.id === itemId 
          ? { ...item, badge: count > 0 ? count : undefined }
          : item
      )
    );
  }
}