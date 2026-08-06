import { CommonModule } from '@angular/common';
import { Component, OnInit, inject } from '@angular/core';
import { RouterModule } from '@angular/router';
import { forkJoin } from 'rxjs';
import { ApiService, Animal } from '../../services/api.service';
import { IconComponent } from '../../shared/icon/icon.component';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule, RouterModule, IconComponent],
  templateUrl: './dashboard.component.html',
  styleUrls: ['./dashboard.component.css']
})
export class DashboardComponent implements OnInit {
  private api = inject(ApiService);

  animalsCount = 0;
  withdrawalCount = 0;
  todayMilkLiters = 0;
  todaySessionsCount = 0;
  recentAnimals: Animal[] = [];

  loadError = false;

  ngOnInit(): void {
    this.loadError = false;

    // Both calls default to "today" server-side, so a KPI here always matches what
    // /milking and /animals would show if you navigated there yourself.
    forkJoin({
      animals: this.api.getAnimals(),
      sessions: this.api.getMilkingSessions()
    }).subscribe({
      next: ({ animals, sessions }) => {
        this.animalsCount = animals.length;
        this.withdrawalCount = animals.filter((a) => a.isInWithdrawal).length;
        this.recentAnimals = animals.slice(0, 5);

        this.todaySessionsCount = sessions.length;
        this.todayMilkLiters = Math.round(sessions.reduce((sum, s) => sum + s.totalLiters, 0) * 10) / 10;
      },
      error: () => {
        // No fallback to invented numbers: a real outage must look like an outage,
        // not like a farm with 28 animals and 148.5 L milked that don't exist.
        this.loadError = true;
        this.animalsCount = 0;
        this.withdrawalCount = 0;
        this.todayMilkLiters = 0;
        this.todaySessionsCount = 0;
        this.recentAnimals = [];
      }
    });
  }
}
