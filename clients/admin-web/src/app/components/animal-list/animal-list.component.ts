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
  loadError = false;

  searchTerm = '';
  statusFilter = 'All';

  ngOnInit(): void {
    this.loadAnimals();
  }

  loadAnimals(): void {
    this.loadError = false;
    this.api.getAnimals().subscribe({
      next: (data) => {
        this.animals = data;
        this.applyFilter();
      },
      error: () => {
        this.loadError = true;
        this.animals = [];
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
