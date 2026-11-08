/** 累计积分持久化（sys.localStorage，跨局保留）。 */
export class ScoreStore {
    private static KEY = 'ddz_scores_v1';
    private static GAMES_KEY = 'ddz_games_v1';

    static load(): number[] {
        const raw = localStorage.getItem(this.KEY);
        if (raw) {
            try {
                const arr = JSON.parse(raw);
                if (Array.isArray(arr) && arr.length === 3) return arr as number[];
            } catch { /* fallthrough */ }
        }
        return [0, 0, 0];
    }

    static save(scores: number[]): void {
        localStorage.setItem(this.KEY, JSON.stringify(scores));
    }

    static add(deltas: number[]): number[] {
        const scores = this.load();
        for (let i = 0; i < 3; i++) scores[i] += deltas[i] ?? 0;
        this.save(scores);
        return scores;
    }

    static get gameCount(): number {
        return parseInt(localStorage.getItem(this.GAMES_KEY) ?? '0', 10) || 0;
    }

    static incrementGames(): void {
        localStorage.setItem(this.GAMES_KEY, String(this.gameCount + 1));
    }

    static reset(): void {
        localStorage.removeItem(this.KEY);
        localStorage.removeItem(this.GAMES_KEY);
    }
}
