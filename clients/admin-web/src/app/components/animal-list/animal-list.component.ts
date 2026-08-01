import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
import { ApiService, Animal } from '../../services/api.service';

@Component({
  selector: 'app-animal-list',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  templateUrl: './animal-list.component.html',
  styleUrls: ['./animal-list.component.css']
})
export class AnimalListComponent implements OnInit {
  private api = inject(ApiService);

  animals: Animal[] = [];
  filteredAnimals: Animal[] = [];
  
  searchTerm = '';
  statusFilter = 'All';

  ngOnInit(): void {
    this.loadAnimals();
  }

  loadAnimals(): void {
    this.api.getAnimals().subscribe({
      next: (data) => {
        this.animals = data;
        this.applyFilter();
      },
      error: () => {
        // Fallback demo data
        this.animals = [
          { id: '1', farmTag: 'VACA-001', officialTag: 'EC-17-00123', name: 'Mariposa', gender: 'Female', speciesName: 'Bovino', breedName: 'Holstein', categoryName: 'Vaca en Producción', status: 'Active', isInWithdrawal: true, withdrawalUntil: '2026-08-05' },
          { id: '2', farmTag: 'VACA-002', officialTag: 'EC-17-00124', name: 'Estrella', gender: 'Female', speciesName: 'Bovino', breedName: 'Jersey', categoryName: 'Vaca en Producción', status: 'Active', isInWithdrawal: false },
          { id: '3', farmTag: 'TORO-010', officialTag: 'EC-17-00155', name: 'Fierro', gender: 'Male', speciesName: 'Bovino', breedName: 'Angus', categoryName: 'Toro Reproductor', status: 'Active', isInWithdrawal: false }
        ];
        this.applyFilter();
      }
    });
  }

  applyFilter(): void {
    this.filteredAnimals = this.animals.filter(a => {
      const matchesSearch = !this.searchTerm || 
        (a.farmTag && a.farmTag.toLowerCase().includes(this.searchTerm.toLowerCase())) ||
        (a.officialTag && a.officialTag.toLowerCase().includes(this.searchTerm.toLowerCase())) ||
        (a.name && a.name.toLowerCase().includes(this.searchTerm.toLowerCase()));

      const matchesStatus = this.statusFilter === 'All' ||
        (this.statusFilter === 'Withdrawal' && a.isInWithdrawal) ||
        (this.statusFilter === 'Active' && !a.isInWithdrawal);

      return matchesSearch && matchesStatus;
    });
  }
}
