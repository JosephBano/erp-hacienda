module.exports = {
  extends: ['expo'],
  env: {
    node: true,
    jest: true,
  },
  ignorePatterns: ['/dist/*', 'node_modules/*'],
  overrides: [
    {
      files: ['tests/**/*', '*.test.ts', '*.test.tsx'],
      env: {
        jest: true,
      },
      rules: {
        '@typescript-eslint/no-require-imports': 'off',
        'no-undef': 'off',
      },
    },
  ],
  rules: {
    'react-hooks/set-state-in-effect': 'warn',
  },
};
