import { ImageAsset, SpriteFrame, Texture2D } from 'cc';
import { Card } from '../Core/Model/Card';

/**
 * 牌面图集：运行时从 resources/cards/<id> 加载程序化生成的 PNG。
 * gen_cards.py（CI 内）产出图集；加载失败时回退到纯代码绘制卡面。
 */
export class CardAtlas {
    private static frames = new Map<number, SpriteFrame>();
    private static loaded = false;
    private static loading: Promise<void> | null = null;

    /** 预加载全部 54 张。幂等。 */
    static preload(): Promise<void> {
        if (this.loaded) return Promise.resolve();
        if (this.loading) return this.loading;

        this.loading = new Promise(resolve => {
            let pending = Card.TOTAL;
            let ok = 0;
            const done = () => {
                if (ok > 0) this.loaded = true;
                resolve();
            };
            for (let id = 0; id < Card.TOTAL; id++) {
                (window as any).cc?.resources?.load
                    ? this.loadOne(id, () => { ok++; if (--pending === 0) done(); })
                    : (pending--, (pending === 0 && done()));
            }
            if (pending === 0) done();
        });
        return this.loading;
    }

    private static loadOne(id: number, onDone: () => void): void {
        const ccAny = (window as any).cc;
        ccAny.resources.load(`cards/${id}/spriteFrame`, SpriteFrame, (err: Error | null, sf: SpriteFrame) => {
            if (!err && sf) this.frames.set(id, sf);
            onDone();
        });
    }

    static get(id: number): SpriteFrame | null {
        return this.frames.get(id) ?? null;
    }

    static get isAvailable(): boolean {
        return this.loaded && this.frames.size > 0;
    }
}
