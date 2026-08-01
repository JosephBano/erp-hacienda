import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { ApiService, Animal } from '../../services/api.service';

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './dashboard.component.html',
  styleUrls: ['./dashboard.component.css']
})
export class DashboardComponent implements OnInit {
  private api = inject(ApiService);
  
  animalsCount = 0;
  withdrawalCount = 0;
  todayMilkLiters = 148.5; // Demo / dynamic summary
  activeLactations = 12;
  recentAnimals: Animal[] = [];

  ngOnInit(): void {
    this.api.getAnimals().subscribe({
      next: (data) => {
        this.animalsCount = data.length;
        this.withdrawalCount = data.filter(a => a.isInWithdrawal).length;
        this.recentAnimals = data.slice(0, 5);
      },
      error: () => {
        // Fallback demo data if backend offline
        this.animalsCount = 28;
        this.withdrawalCount = 2;
        this.recentAnimals = [
          { id: '1', farmTag: 'VACA-001', officialTag: 'EC-17-00123', name: 'Mariposa', gender: 'Female', speciesName: 'Bovino', breedName: 'Holstein', categoryName: 'Vaca en Producción', status: 'Active', isInWithdrawal: true, withdrawalUntil: '2026-08-05' },
          { id: '2', farmTag: 'VACA-002', officialTag: 'EC-17-00124', name: 'Estrella', gender: 'Female', speciesName: 'Bovino', breedName: 'Jersey', categoryName: 'Vaca en Producción', status: 'Active', isInWithdrawal: false }
        ];
      }
    });
  }
}
