/**
 * Design tokens for a screen used at 5 AM, outdoors, with gloves on.
 *
 * Three constraints drive every value here and none of them are aesthetic:
 * - **Sun.** Contrast is pushed well past the usual minimums; mid-greys vanish on a phone
 *   held under Ecuadorian sun.
 * - **Gloves.** Nothing tappable is below 64pt. The usual 44pt target assumes a
 *   fingertip, not a wet glove.
 * - **Speed.** Type is large enough to read at arm's length while the animal moves.
 */
export const theme = {
  color: {
    background: '#0B1220',
    surface: '#16213A',
    surfaceRaised: '#1E2C4A',
    border: '#33456B',
    text: '#FFFFFF',
    textMuted: '#B9C6E0',
    primary: '#2FA84F',
    primaryText: '#04140A',
    danger: '#E5484D',
    warning: '#F5A524',
    warningText: '#241701',
    info: '#3B82F6',
  },
  space: {
    xs: 4,
    sm: 8,
    md: 16,
    lg: 24,
    xl: 32,
  },
  radius: {
    md: 12,
    lg: 20,
  },
  font: {
    body: 18,
    label: 16,
    title: 26,
    display: 40,
  },
  /** Minimum tappable height. Below this, a gloved hand misses. */
  touchTarget: 64,
} as const;
