import { Routes } from '@angular/router';

export const routes: Routes = [
  { 
    path: '', 
    redirectTo: '/dashboard', 
    pathMatch: 'full' 
  },
  { 
    path: 'dashboard', 
    loadComponent: () => import('./features/dashboard/dashboard').then(m => m.Dashboard),
    title: 'Dashboard - HoopGameNight'
  },
  { 
    path: 'games', 
    loadComponent: () => import('./features/games/games').then(m => m.Games),
    title: 'Games - HoopGameNight'
  },
  { 
    path: 'api-status', 
    loadComponent: () => import('./features/api-status/api-status').then(m => m.ApiStatus),
    title: 'API Status - HoopGameNight'
  },

  { 
    path: 'teams', 
    loadComponent: () => import('./features/teams/teams').then(m => m.Teams),
    title: 'Teams - HoopGameNight'
  },
  // { 
  //   path: 'players', 
  //   loadComponent: () => import('./features/players/players').then(m => m.Players),
  //   title: 'Players - HoopGameNight'
  // },
  // { 
  //   path: 'settings', 
  //   loadComponent: () => import('./features/settings/settings').then(m => m.Settings),
  //   title: 'Settings - HoopGameNight'
  // },
  { 
    path: '**', 
    redirectTo: '/dashboard' 
  }
];