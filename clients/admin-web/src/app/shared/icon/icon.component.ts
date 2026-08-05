import { ChangeDetectionStrategy, Component, input } from '@angular/core';

export const ICON_NAMES = [
  'alert',
  'arrow-left',
  'calendar',
  'check',
  'close',
  'dashboard',
  'dna',
  'file-text',
  'lock',
  'log-out',
  'menu',
  'milk',
  'pig',
  'save',
  'search',
  'shield',
  'user',
  'wifi',
] as const;

export type IconName = (typeof ICON_NAMES)[number];
export type IconSize = 'sm' | 'md' | 'lg' | 'xl';

@Component({
  selector: 'app-icon',
  standalone: true,
  templateUrl: './icon.component.html',
  styleUrl: './icon.component.css',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class IconComponent {
  readonly name = input.required<IconName>();
  readonly size = input<IconSize>('md');
  readonly label = input<string>();
}
