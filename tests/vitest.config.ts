import { defineConfig } from 'vitest/config';
import { fileURLToPath } from 'url';

export default defineConfig({
    test: {
        include: ['**/*.spec.ts'],
        environment: 'node',
        alias: {
            '@core': fileURLToPath(new URL('../assets/Scripts/Core', import.meta.url)),
        },
    },
});
