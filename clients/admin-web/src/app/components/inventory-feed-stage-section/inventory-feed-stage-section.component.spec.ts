import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { ApiService, FeedStageDto } from '../../services/api.service';
import { InventoryFeedStageSectionComponent } from './inventory-feed-stage-section.component';

describe('InventoryFeedStageSectionComponent', () => {
  let apiStub: Partial<ApiService>;
  const stages: FeedStageDto[] = [{ id: 'f1', key: 'starter', labelEs: 'Inicio', isActive: true }];
  beforeEach(async () => { apiStub = { setInventoryItemFeedStage: () => of(undefined) }; await TestBed.configureTestingModule({ imports: [InventoryFeedStageSectionComponent], providers: [provideRouter([]), { provide: ApiService, useValue: apiStub }] }).compileComponents(); });
  it('shows active feed stages', () => { const fixture = TestBed.createComponent(InventoryFeedStageSectionComponent); fixture.componentInstance.stages = stages; fixture.detectChanges(); expect(fixture.nativeElement.textContent).toContain('Inicio'); });
  it('saves the selected stage', () => { const fixture = TestBed.createComponent(InventoryFeedStageSectionComponent); fixture.componentInstance.itemId = 'i1'; fixture.componentInstance.selectedStageId = 'f1'; fixture.detectChanges(); vi.spyOn(apiStub, 'setInventoryItemFeedStage'); fixture.componentInstance.save(); expect(apiStub.setInventoryItemFeedStage).toHaveBeenCalledWith('i1', { feedStageId: 'f1' }); });
  it('supports clearing the stage', () => { const fixture = TestBed.createComponent(InventoryFeedStageSectionComponent); fixture.componentInstance.itemId = 'i1'; fixture.componentInstance.selectedStageId = ''; fixture.detectChanges(); vi.spyOn(apiStub, 'setInventoryItemFeedStage'); fixture.componentInstance.save(); expect(apiStub.setInventoryItemFeedStage).toHaveBeenCalledWith('i1', { feedStageId: null }); });
  it('has no emoji', () => { const fixture = TestBed.createComponent(InventoryFeedStageSectionComponent); fixture.detectChanges(); expect(/[\u{1F000}-\u{1FAFF}\u{2600}-\u{27BF}]/u.test(fixture.nativeElement.textContent)).toBe(false); });
});
