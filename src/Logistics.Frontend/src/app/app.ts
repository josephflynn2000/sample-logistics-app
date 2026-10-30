import { Component } from '@angular/core';
import { Shell } from './core/components/shell/shell';

@Component({
  imports: [Shell],
  selector: 'app-root',
  styleUrl: './app.css',
  templateUrl: './app.html',
})
export class App {}
