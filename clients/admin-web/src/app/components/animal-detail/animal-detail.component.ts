import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { ApiService, AnimalDetail } from '../../services/api.service';

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
  activeTab: 'events' | 'milking' | 'withdrawals' = 'events';

  ngOnInit(): void {
    const id = this.route.snapshot.paramMap.get('id');
    if (id) {
      this.api.getAnimalById(id).subscribe({
        next: (data) => this.animal = data,
        error: () => {
          // Demo fallback
          this.animal = {
            id,
            farmTag: 'VACA-001',
            officialTag: 'EC-17-00123',
            name: 'Mariposa',
            gender: 'Female',
            speciesName: 'Bovino',
            breedName: 'Holstein',
            categoryName: 'Vaca en Producción',
            status: 'Active',
            isInWithdrawal: true,
            withdrawalUntil: '2026-08-05',
            events: [
              { id: 'e1', eventType: 'Weight', eventDate: '2026-07-15', detailsJson: '{"WeightKg": 540.5}', recordedBy: 'veterinario 1' },
              { id: 'e2', eventType: 'Treatment', eventDate: '2026-07-28', detailsJson: '{"MedicationName": "Oxitetraciclina", "WithdrawalDays": 7}', recordedBy: 'veterinario 1' }
            ],
            milkYields: [
              { id: 'm1', date: '2026-08-01', session: 'Morning', liters: 14.5 },
              { id: 'm2', date: '2026-08-01', session: 'Afternoon', liters: 12.0 }
            ]
          };
        }
      });
    }
  }

  setTab(tab: 'events' | 'milking' | 'withdrawals'): void {
    this.activeTab = tab;
  }
}
