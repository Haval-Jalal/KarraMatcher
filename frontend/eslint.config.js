import js from '@eslint/js'
import prettier from 'eslint-config-prettier'
import reactHooks from 'eslint-plugin-react-hooks'
import reactRefresh from 'eslint-plugin-react-refresh'
import globals from 'globals'
import tseslint from 'typescript-eslint'

export default tseslint.config(
  { ignores: ['dist', 'coverage', 'node_modules'] },

  js.configs.recommended,

  {
    // Typade regler kräver ett tsconfig-projekt och gäller därför bara källkoden.
    files: ['**/*.{ts,tsx}'],
    extends: [...tseslint.configs.recommendedTypeChecked],
    languageOptions: {
      ecmaVersion: 2023,
      globals: globals.browser,
      parserOptions: {
        projectService: true,
        tsconfigRootDir: import.meta.dirname,
      },
    },
    plugins: {
      'react-hooks': reactHooks,
      'react-refresh': reactRefresh,
    },
    rules: {
      // CLAUDE.md → Frontend kräver att eslint-plugin-react-hooks är aktivt.
      ...reactHooks.configs.recommended.rules,
      'react-refresh/only-export-components': ['warn', { allowConstantExport: true }],

      // CLAUDE.md: undvik any.
      '@typescript-eslint/no-explicit-any': 'error',
      '@typescript-eslint/consistent-type-imports': 'error',
      '@typescript-eslint/no-unused-vars': ['error', { argsIgnorePattern: '^_' }],
    },
  },

  {
    /*
     * §KM.5: konverteringen mellan UTC och svensk tid sker på **exakt ett ställe** i
     * frontenden — src/lib/time.ts. Utan den här regeln är det en ambition; med den är
     * det verkställbart.
     *
     * Det som förbjuds är inte formatering i sig, utan att formatera *utan* att ange
     * tidszon. `toLocaleTimeString()` använder webbläsarens egen zon, vilket ger rätt
     * svar för en förälder i Kärra och fel svar för en på semester i Spanien — ett fel
     * som är osynligt för den som bygger appen.
     */
    files: ['src/**/*.{ts,tsx}'],
    ignores: ['src/lib/time.ts', 'src/lib/time.test.ts'],
    rules: {
      'no-restricted-syntax': [
        'error',
        {
          selector: 'CallExpression[callee.property.name=/^toLocale(Date|Time)?String$/]',
          message:
            'Använd hjälpfunktionerna i @/lib/time. toLocale*-metoderna använder ' +
            'webbläsarens tidszon, inte Europe/Stockholm (§KM.5).',
        },
        {
          selector: "NewExpression[callee.object.name='Intl']",
          message:
            'Använd hjälpfunktionerna i @/lib/time i stället för att formatera datum ' +
            'på egen hand. Konverteringen ska ske på ett enda ställe (§KM.5).',
        },
      ],
    },
  },

  {
    // Konfigfiler ligger utanför tsconfig-projekten — inga typade regler här.
    files: ['*.config.js', '*.config.ts'],
    extends: [tseslint.configs.disableTypeChecked],
    languageOptions: { globals: globals.node },
  },

  {
    files: ['**/*.test.{ts,tsx}', 'src/test/**'],
    languageOptions: { globals: { ...globals.browser, ...globals.node } },
  },

  // Måste ligga sist: stänger av regler som krockar med Prettier.
  prettier,
)
