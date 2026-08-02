import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { ApiService, AnimalDetail, PedigreeDto, DamKpisDto } from '../../services/api.service';

@Component({
  selector: 'app-animal-detail',
  standalone: true,
  imports: [CommonModule, RouterModule],
  templateUrl: './animal-detail.component.html',
  styleUrls: ['./animal-detail.component.css']
})
export class AnimalDetailComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private api = inject(ApiService);

  animal: AnimalDetail | null = null;
  pedigree: PedigreeDto | null = null;
  damKpis: DamKpisDto | null = null;
  activeTab: 'events' | 'milking' | 'breeding' | 'withdrawals' = 'events';
  loadError = false;

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.api.getAnimalById(id).subscribe({
        next: (data) => this.animal = data,
        error: () => {
          this.loadError = true;
          this.animal = null;
        }
      });

      this.api.getPedigree(id).subscribe({
        next: (data) => (this.pedigree = data),
        error: () => {}
      });

      this.api.getDamKpis(id).subscribe({
        next: (data) => (this.damKpis = data),
        error: () => {}
      });
    }
  }

  setTab(tab: 'events' | 'milking' | 'breeding' | 'withdrawals'): void {
    this.activeTab = tab;
  }
}
