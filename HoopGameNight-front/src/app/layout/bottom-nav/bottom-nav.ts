import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';

@Component({
  selector: 'app-bottom-nav',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './bottom-nav.html',
  styleUrls: ['./bottom-nav.scss']
})
export class BottomNav {
  navItems = [
    { label: 'Home', icon: 'home', route: '/' },
    { label: 'Jogos', icon: 'sports_basketball', route: '/games' },
    { label: 'IA', icon: 'psychology', route: '/ask', highlight: true },
    { label: 'Jogadores', icon: 'person', route: '/players' },
    { label: 'Times', icon: 'groups', route: '/teams' }
  ];
}
