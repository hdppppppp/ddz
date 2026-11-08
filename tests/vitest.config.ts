import { defineConfig } from 'vitest/config';

export default defineConfig({
    test: {
        include: ['**/*.spec.ts'],
        environment: 'node',
        alias: {
            '@core': new URL('../assets/Scripts/Core', import.meta.url).pathname,
        },
    },
});
