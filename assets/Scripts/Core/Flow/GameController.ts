import { Card, HAND_COUNT, BOTTOM_COUNT, sortAsc, shuffle } from '../Model/Card';
import { HandPattern } from '../Pattern/HandPattern';
import { validate } from '../Rules/PlayValidator';

/** 对局阶段。 */
export enum GamePhase { NotStarted = 0, Bidding = 1, Playing = 2, Settled = 3 }

/** 一个座位的玩家状态。 */
export class PlayerState {
    readonly hand: Card[] = [];
    isLandlord = false;
    playCount = 0;

    constructor(public readonly seat: number) { }

    get handCount(): number { return this.hand.length; }

    reset(): void {
        this.hand.length = 0;
        this.isLandlord = false;
        this.playCount = 0;
    }
}

/** 对局结算结果。 */
export interface GameResult {
    landlordSeat: number;
    winnerSeat: number;
    landlordWin: boolean;
    baseScore: number;
    bombCount: number;
    spring: boolean;
    multiplier: number;
    scoreDeltas: number[];
}

/** 领域事件（表现层订阅刷新 UI）。 */
export interface GameEvents {
    onCardsDealt?: (seat: number, hand: Card[]) => void;
    onBidMade?: (seat: number, score: number) => void;
    onBiddingRestarted?: () => void;
    onLandlordDecided?: (seat: number, bottom: Card[]) => void;
    onTurnStarted?: (seat: number, phase: GamePhase) => void;
    onCardsPlayed?: (seat: number, pattern: HandPattern) => void;
    onPassed?: (seat: number) => void;
    onGameSettled?: (result: GameResult) => void;
}

/**
 * 对局控制器：发牌 → 叫分 → 出牌 → 结算 状态机。
 * 纯逻辑，不含定时器；AI 动作由宿主按节奏调用 AI 决策后提交。
 */
export class GameController {
    readonly players: PlayerState[] = [];
    events: GameEvents = {};

    private bottomCards: Card[] = [];
    private _bidTurn = 0;
    private bidCount = 0;
    private _highestBid = 0;
    private highestBidder = -1;
    private _lastPlay: HandPattern | null = null;
    private _lastPlaySeat = -1;
    private passCount = 0;
    private bombCount = 0;
    private _currentSeat = 0;
    biddingNeedsRestart = false;

    private _phase = GamePhase.NotStarted;
    private _landlordSeat = -1;
    private _baseScore = 1;
    private _multiplier = 1;

    get phase(): GamePhase { return this._phase; }
    get landlordSeat(): number { return this._landlordSeat; }
    get baseScore(): number { return this._baseScore; }
    get multiplier(): number { return this._multiplier; }
    get currentSeat(): number { return this._currentSeat; }
    get highestBid(): number { return this._highestBid; }
    get lastPlay(): HandPattern | null { return this._lastPlay; }
    get lastPlaySeat(): number { return this._lastPlaySeat; }
    get bottom(): Card[] { return this.bottomCards; }

    /** 当前该叫分的座位（非叫分阶段返回 -1）。 */
    get bidTurnSeat(): number { return this._phase === GamePhase.Bidding ? this._bidTurn : -1; }

    /** 是否自由出牌（当前轮到者没有需要压的牌）。 */
    isFreePlay(seat: number): boolean {
        return this._lastPlay === null || this._lastPlaySeat === seat;
    }

    startGame(): void {
        this.players.length = 0;
        for (let seat = 0; seat < 3; seat++) this.players.push(new PlayerState(seat));

        const deck = shuffle();
        for (let seat = 0; seat < 3; seat++) {
            const p = this.players[seat];
            p.reset();
            p.hand.push(...deck.slice(seat * HAND_COUNT, (seat + 1) * HAND_COUNT));
            const sorted = sortAsc(p.hand);
            p.hand.length = 0;
            p.hand.push(...sorted);
            this.events.onCardsDealt?.(seat, [...p.hand]);
        }

        this.bottomCards = deck.slice(3 * HAND_COUNT, 3 * HAND_COUNT + BOTTOM_COUNT);

        this._phase = GamePhase.Bidding;
        this._bidTurn = Math.floor(Math.random() * 3);
        this.bidCount = 0;
        this._highestBid = 0;
        this.highestBidder = -1;
        this.biddingNeedsRestart = false;
        this._lastPlay = null;
        this._lastPlaySeat = -1;
        this.passCount = 0;
        this.bombCount = 0;
        this._landlordSeat = -1;
        this._baseScore = 1;
        this._multiplier = 1;
    }

    /** 提交叫分（0=不叫，1/2/3）。返回地主座位，未决出 -1。 */
    bid(seat: number, score: number): number {
        this.ensurePhase(GamePhase.Bidding);
        if (seat !== this._bidTurn) throw new Error(`还没轮到座位 ${seat} 叫分（当前 ${this._bidTurn}）`);
        if (score !== 0 && (score <= this._highestBid || score > 3)) {
            throw new Error(`叫分 ${score} 无效（当前最高 ${this._highestBid}）`);
        }

        this.events.onBidMade?.(seat, score);
        if (score > this._highestBid) {
            this._highestBid = score;
            this.highestBidder = seat;
        }
        this.bidCount++;

        if (this._highestBid === 3 || this.bidCount >= 3) {
            if (this.highestBidder < 0) {
                this.biddingNeedsRestart = true;
                this.events.onBiddingRestarted?.();
                return -1;
            }
            this.becomeLandlord(this.highestBidder);
            return this._landlordSeat;
        }

        this._bidTurn = (this._bidTurn + 1) % 3;
        return -1;
    }

    private becomeLandlord(seat: number): void {
        this._landlordSeat = seat;
        this._baseScore = Math.max(this._highestBid, 1);
        this.players[seat].isLandlord = true;
        this.players[seat].hand.push(...this.bottomCards);
        const sorted = sortAsc(this.players[seat].hand);
        this.players[seat].hand.length = 0;
        this.players[seat].hand.push(...sorted);
        this.events.onLandlordDecided?.(seat, [...this.bottomCards]);

        this._phase = GamePhase.Playing;
        this._currentSeat = seat;
        this.events.onTurnStarted?.(seat, this._phase);
    }

    /** 出牌（校验 + 移除 + 推进）。 */
    playCards(seat: number, cards: Card[]): void {
        this.ensurePhase(GamePhase.Playing);
        if (seat !== this._currentSeat) throw new Error(`还没轮到座位 ${seat}（当前 ${this._currentSeat}）`);

        const freePlay = this.isFreePlay(seat);
        let pattern: HandPattern;
        if (!validate(cards, freePlay, freePlay ? null : this._lastPlay).valid) {
            throw new Error('出牌不合法');
        }
        pattern = validate(cards, freePlay, freePlay ? null : this._lastPlay).pattern!;

        const player = this.players[seat];
        const playedIds = new Set(cards.map(c => c.id));
        if (playedIds.size !== cards.length) throw new Error('出牌包含重复的牌');
        if (!cards.every(c => player.hand.some(h => h.id === c.id))) throw new Error('出牌包含不持有的牌');

        player.hand = player.hand.filter(h => !playedIds.has(h.id));
        player.playCount++;
        if (pattern.isBombLike) {
            this.bombCount++;
            this._multiplier *= 2;
        }

        this._lastPlay = pattern;
        this._lastPlaySeat = seat;
        this.passCount = 0;
        this.events.onCardsPlayed?.(seat, pattern);

        if (player.handCount === 0) {
            this.settle(seat);
            return;
        }
        this.advanceTurn();
    }

    /** 不出。 */
    pass(seat: number): void {
        this.ensurePhase(GamePhase.Playing);
        if (seat !== this._currentSeat) throw new Error(`还没轮到座位 ${seat}（当前 ${this._currentSeat}）`);
        if (this.isFreePlay(seat)) throw new Error('自由出牌时不能不出');

        this.passCount++;
        this.events.onPassed?.(seat);

        if (this.passCount >= 2) {
            this._lastPlay = null;
            this._lastPlaySeat = -1;
            this.passCount = 0;
        }
        this.advanceTurn();
    }

    private advanceTurn(): void {
        this._currentSeat = (this._currentSeat + 1) % 3;
        this.events.onTurnStarted?.(this._currentSeat, this._phase);
    }

    private settle(winnerSeat: number): void {
        this._phase = GamePhase.Settled;
        const landlordWin = winnerSeat === this._landlordSeat;

        const spring = landlordWin && this.players.every(p => p.isLandlord || p.playCount === 0);
        const antiSpring = !landlordWin && this.players[this._landlordSeat].playCount <= 1;
        if (spring || antiSpring) this._multiplier *= 2;

        const unit = this._baseScore * this._multiplier;
        const scoreDeltas = [0, 0, 0];
        for (let seat = 0; seat < 3; seat++) {
            const isLandlord = seat === this._landlordSeat;
            const win = isLandlord === landlordWin;
            const share = isLandlord ? 2 : 1;
            scoreDeltas[seat] = (win ? 1 : -1) * unit * share;
        }

        this.events.onGameSettled?.({
            landlordSeat: this._landlordSeat,
            winnerSeat,
            landlordWin,
            baseScore: this._baseScore,
            bombCount: this.bombCount,
            spring: spring || antiSpring,
            multiplier: this._multiplier,
            scoreDeltas,
        });
    }

    private ensurePhase(phase: GamePhase): void {
        if (this._phase !== phase) throw new Error(`当前阶段 ${this._phase}，需要 ${phase}`);
    }
}
