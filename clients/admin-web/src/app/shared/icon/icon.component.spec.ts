import { TestBed } from '@angular/core/testing';
import { ICON_NAMES, IconComponent } from './icon.component';

describe('IconComponent', () => {
  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [IconComponent],
    }).compileComponents();
  });

  it('should render every icon name as a non-empty local SVG with the shared stroke contract', () => {
    for (const name of ICON_NAMES) {
      const fixture = TestBed.createComponent(IconComponent);
      fixture.componentRef.setInput('name', name);

      fixture.detectChanges();

      const svg = fixture.nativeElement.querySelector('svg') as SVGElement | null;
      expect(svg, `${name} should render an SVG`).not.toBeNull();
      expect(svg?.innerHTML.trim(), `${name} should have local vector content`).not.toBe('');
      expect(svg?.getAttribute('viewBox')).toBe('0 0 24 24');
      expect(svg?.getAttribute('stroke')).toBe('currentColor');
      expect(svg?.querySelector('text, tspan, image, foreignObject, script')).toBeNull();
    }
  });

  it('should expose decorative and labelled accessibility states correctly', () => {
    const decorativeFixture = TestBed.createComponent(IconComponent);
    decorativeFixture.componentRef.setInput('name', 'file-text');
    decorativeFixture.detectChanges();

    const decorativeSvg = decorativeFixture.nativeElement.querySelector('svg') as SVGElement;
    expect(decorativeSvg.getAttribute('aria-hidden')).toBe('true');
    expect(decorativeSvg.getAttribute('role')).toBeNull();
    expect(decorativeSvg.getAttribute('aria-label')).toBeNull();

    const labelledFixture = TestBed.createComponent(IconComponent);
    labelledFixture.componentRef.setInput('name', 'file-text');
    labelledFixture.componentRef.setInput('label', 'Actividad');
    labelledFixture.detectChanges();

    const labelledSvg = labelledFixture.nativeElement.querySelector('svg') as SVGElement;
    expect(labelledSvg.getAttribute('role')).toBe('img');
    expect(labelledSvg.getAttribute('aria-label')).toBe('Actividad');
    expect(labelledSvg.getAttribute('aria-hidden')).toBeNull();
  });
});
