import React from 'react';
import { View } from 'react-native';
import { fireEvent, render, screen } from '@testing-library/react-native';

import { BigButton, Screen } from '../src/ui/components';
import { theme } from '../src/ui/theme';

/**
 * feature-0006 commit 1 — the scroll container itself.
 *
 * These are contract tests, not layout measurements: React Native Testing Library
 * renders through the mock host, so nothing here proves a pixel is on screen. What
 * it does prove is that the two settings that *cause* unreachable content — a
 * clamped content box and React Native's default tap-swallowing with the keyboard
 * open — are pinned so a later edit cannot quietly reintroduce them. The on-device
 * verification is `test-e2e.md`, and it is the one that closes the spec (criterion 6).
 */
describe('Screen', () => {
  /** Counts the host scrollers in the rendered tree — the D1 "one vertical gesture" check. */
  const countScrollers = (node: unknown): number => {
    if (!node || typeof node !== 'object') return 0;
    const element = node as { type?: string; children?: unknown[] };
    const self = element.type === 'RCTScrollView' || element.type === 'ScrollView' ? 1 : 0;
    return (element.children ?? []).reduce<number>((acc, child) => acc + countScrollers(child), self);
  };

  const flatten = (style: unknown): Record<string, unknown> =>
    Array.isArray(style)
      ? style.reduce<Record<string, unknown>>((acc, part) => ({ ...acc, ...flatten(part) }), {})
      : ((style ?? {}) as Record<string, unknown>);

  it('leaves a non-scrollable screen exactly as it was', async () => {
    await render(
      <Screen testID="plain">
        <BigButton testID="only" label="Único" onPress={() => undefined} />
      </Screen>,
    );

    // No ScrollView at all: screens that own a bounded inner list must not gain a
    // second vertical scroller behind their back (D1).
    expect(countScrollers(screen.toJSON())).toBe(0);
    expect(screen.getByTestId('plain')).toBeTruthy();
  });

  it('lets content taller than the window grow past it instead of clamping', async () => {
    await render(
      <Screen testID="tall" scrollable>
        {Array.from({ length: 12 }, (_, index) => (
          <BigButton key={index} testID={`row-${index}`} label={`Fila ${index}`} onPress={() => undefined} />
        ))}
      </Screen>,
    );

    const content = flatten(screen.getByTestId('tall').props.contentContainerStyle);

    // `flex: 1` here is the defect: it clamps the content box to the viewport, so a
    // 12-row picker (12 × 64 = 768 units, before padding) has its tail cut off with
    // nothing to scroll. `flexGrow: 1` still fills a short screen but lets a long
    // one exceed the window.
    expect(content.flexGrow).toBe(1);
    expect(content.flex).toBeUndefined();
  });

  it('keeps every row of an over-long screen in the tree and pressable', async () => {
    const pressed: number[] = [];
    await render(
      <Screen testID="tall" scrollable>
        {Array.from({ length: 12 }, (_, index) => (
          <BigButton
            key={index}
            testID={`row-${index}`}
            label={`Fila ${index}`}
            onPress={() => pressed.push(index)}
          />
        ))}
      </Screen>,
    );

    fireEvent.press(screen.getByTestId('row-11'));
    expect(pressed).toEqual([11]);
  });

  it('lets the first tap with the keyboard open reach the button', async () => {
    await render(
      <Screen testID="form" scrollable>
        <BigButton testID="confirm" label="Registrar" onPress={() => undefined} />
      </Screen>,
    );

    const scroller = screen.getByTestId('form');

    // React Native's default is 'never', which spends the first tap dismissing the
    // keyboard and never delivers it to the button — the employee taps "Registrar",
    // nothing happens, they tap again. 'handled' delivers the tap.
    expect(scroller.props.keyboardShouldPersistTaps).toBe('handled');
    // Dragging the list away from a field puts the keyboard down and registers
    // nothing (D2).
    expect(scroller.props.keyboardDismissMode).toBe('on-drag');
  });

  it('opens exactly one vertical scroller so no drag is trapped', async () => {
    await render(
      <Screen testID="tall" scrollable>
        <View>
          <BigButton testID="row" label="Fila" onPress={() => undefined} />
        </View>
      </Screen>,
    );

    expect(countScrollers(screen.toJSON())).toBe(1);
  });

  it('keeps the 64-unit gloved-hand touch target', async () => {
    await render(
      <Screen testID="tall" scrollable>
        <BigButton testID="row" label="Fila" onPress={() => undefined} />
      </Screen>,
    );

    expect(flatten(screen.getByTestId('row').props.style).minHeight).toBe(theme.touchTarget);
    expect(theme.touchTarget).toBe(64);
  });
});
