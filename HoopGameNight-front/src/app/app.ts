import { Component } from '@angular/core';
import { MainLayout } from './layout/main-layout/main-layout';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [MainLayout],
  template: `<app-main-layout></app-main-layout>`
})
export class App {
  title = 'meu-app';
}