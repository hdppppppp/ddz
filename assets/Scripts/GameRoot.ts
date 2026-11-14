import { _decorator, Component, Node, Color, UITransform, Widget, Sprite, SpriteFrame, Texture2D, ImageAsset, Label, LabelOutline, UIOpacity, Layout, view, ResolutionPolicy, EventTouch, Button, Vec3, Vec2 } from 'cc';
import { GameController, GamePhase, GameResult } from './Core/Flow/GameController';
import { Card } from './Core/Model/Card';
import { HandPattern } from './Core/Pattern/HandPattern';
import { decideBid } from './Core/AI/BidAI';
import { decidePlay } from './Core/AI/PlayAI';
import { ResultDialog } from './UI/ResultDialog';
import { ScoreStore } from './UI/ScoreStore';
import { CardAtlas } from './UI/CardAtlas';

const { ccclass } = _decorator;

const RED = new Color(200, 30, 30, 255);
const BLACK = new Color(20, 20, 20, 255);
const JOKER_RED = new Color(150, 40, 160, 255);

function makeSolidSprite(color: Color): SpriteFrame {
    const img = new ImageAsset();
    // 1x1 白色像素（Blob 版本）
    const base64 = 'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAYAAAAfFcSJAAAADUlEQVR42mP8z8BQDwAEhQGAhKmMIQAAAABJRU5ErkJggg==';
    const bin = atob(base64);
    const bytes = new Uint8Array(bin.length);
    for (let i = 0; i < bin.length; i++) bytes[i] = bin.charCodeAt(i);
    img.reset({ _data: bytes, width: 1, height: 1, format: Texture2D.PixelFormat.RGBA8888, _compressed: false, mipmapLevel: 0 });
    const tex = new Texture2D();
    tex.image = img;
    const sf = new SpriteFrame();
    sf.texture = tex;
    return sf;
}

/** 纯代码 UI 的斗地主主控制器（Cocos 3.8，无预制体依赖）。 */
@ccclass('GameRoot')
export class GameRoot extends Component {
    private game = new GameController();
    private aiTimer = 0;
    private aiWaiting = false;
    private humanSeat = 0;
    private cardNodes = new Map<number, Node>();
    private selected = new Set<number>();
    private bottomCardNodes: Node[] = [];
    private seatNodes: Node[] = [];
    private seatLabels: Label[] = [];
    private handNode: Node = null!;
    private playedNodes: (Node | null)[] = [null, null, null];
    private actionNode: Node = null!;
    private biddingNode: Node = null!;
    private toastLabel: Label = null!;
    private toastTime = 0;
    private scoreLabels: Label[] = [];
    private multiplierLabel: Label = null!;
    private resultDialog: ResultDialog = null!;
    private scoreHeader: Label = null!;

    start() {
        // 竖屏设计分辨率
        view.setDesignResolutionSize(1080, 1920, ResolutionPolicy.FIXED_WIDTH);
        // 预载牌面图集（失败自动回退纯代码卡面）
        CardAtlas.preload().then(() => {
            if (CardAtlas.isAvailable) this.renderHand();
        });

        this.game.events = {
            onCardsDealt: (seat, hand) => this.onCardsDealt(seat, hand),
            onBidMade: (seat, score) => this.onBidMade(seat, score),
            onBiddingRestarted: () => this.restart(),
            onLandlordDecided: (seat, bottom) => this.onLandlord(seat, bottom),
            onTurnStarted: (seat) => this.onTurn(seat),
            onCardsPlayed: (seat, pattern) => this.onPlayed(seat, pattern),
            onPassed: (seat) => this.onPass(seat),
            onGameSettled: (r) => this.onSettled(r),
        };

        this.buildUI();
        this.newRound(true);
    }

    update(dt: number) {
        if (this.toastTime > 0) {
            this.toastTime -= dt;
            if (this.toastTime <= 0) this.toastLabel.node.active = false;
        }

        if (this.game.phase !== GamePhase.Bidding && this.game.phase !== GamePhase.Playing) return;
        const seat = this.game.phase === GamePhase.Bidding ? this.game.bidTurnSeat : this.game.currentSeat;
        if (seat < 0 || seat === this.humanSeat) { this.aiWaiting = false; return; }

        if (!this.aiWaiting) { this.aiWaiting = true; this.aiTimer = 0.7 + Math.random() * 0.7; return; }
        this.aiTimer -= dt;
        if (this.aiTimer > 0) return;
        this.aiWaiting = false;

        // AI 行动
        if (this.game.phase === GamePhase.Bidding) {
            this.game.bid(seat, decideBid(this.game.players[seat].hand, this.game.highestBid));
        } else {
            const move = decidePlay(this.game, seat);
            if (move) this.game.playCards(seat, move);
            else this.game.pass(seat);
        }
    }

    // ================= UI 构建 =================

    private buildUI() {
        const canvas = this.node;
        const white = makeSolidSprite(Color.WHITE);

        // 背景色
        const bg = new Node('Bg');
        bg.parent = canvas;
        const bgSp = bg.addComponent(Sprite);
        bgSp.type = Sprite.SizeMode.CUSTOM;
        bgSp.sizeMode = Sprite.SizeMode.CUSTOM;
        bgSp.spriteFrame = white;
        bgSp.color = new Color(16, 70, 40, 255);
        const bgTr = bg.addComponent(UITransform);
        bgTr.setContentSize(1080, 1920);
        bg.setPosition(0, 0);

        // 倍数
        this.multiplierLabel = this.makeLabel(canvas, 'multiplier', '倍数 x1', 36, new Vec3(0, 860));
        // 累计积分头（左上）
        const scores = ScoreStore.load();
        this.scoreHeader = this.makeLabel(canvas, 'scoreHeader',
            `第 ${ScoreStore.gameCount + 1} 局   我的积分 ${scores[0]}`, 30, new Vec3(-280, 900));
        // 结算弹窗（挂 canvas 下）
        const dialogNode = new Node('ResultDialogHolder');
        dialogNode.parent = canvas;
        this.resultDialog = dialogNode.addComponent(ResultDialog);
        // 底牌区
        const bottomArea = new Node('Bottom');
        bottomArea.parent = canvas;
        bottomArea.addComponent(UITransform).setContentSize(360, 140);
        bottomArea.setPosition(0, 770);
        // 三个座位
        for (let seat = 0; seat < 3; seat++) {
            const pos = seat === 0 ? new Vec3(-450, -640) : seat === 1 ? new Vec3(-450, 120) : new Vec3(450, 120);
            const area = new Node(`Seat${seat}`);
            area.parent = canvas;
            area.addComponent(UITransform).setContentSize(300, 300);
            area.setPosition(pos);
            const label = this.makeLabel(area, `SeatLabel${seat}`, '', 30, new Vec3(0, 0));
            this.seatLabels.push(label);
            const playedArea = new Node(`Played${seat}`);
            playedArea.parent = canvas;
            playedArea.addComponent(UITransform).setContentSize(560, 300);
            playedArea.setPosition(seat === 0 ? new Vec3(0, -180) : seat === 1 ? new Vec3(140, 320) : new Vec3(-140, 320));
            this.playedNodes[seat] = playedArea;
        }
        // 手牌区
        this.handNode = new Node('Hand');
        this.handNode.parent = canvas;
        this.handNode.addComponent(UITransform).setContentSize(1020, 220);
        this.handNode.setPosition(0, -760);
        // 操作条
        this.actionNode = new Node('Actions');
        this.actionNode.parent = canvas;
        this.actionNode.addComponent(UITransform).setContentSize(1080, 140);
        this.actionNode.setPosition(0, -560);
        this.actionNode.active = false;
        this.makeButton(this.actionNode, '不出', new Vec3(-340, 0), () => this.tryPass(), new Color(120, 90, 30));
        this.makeButton(this.actionNode, '提示', new Vec3(0, 0), () => this.onHint(), new Color(40, 90, 160));
        this.makeButton(this.actionNode, '出牌', new Vec3(340, 0), () => this.tryPlay(), new Color(170, 40, 40));
        // 叫分条
        this.biddingNode = new Node('Bidding');
        this.biddingNode.parent = canvas;
        this.biddingNode.addComponent(UITransform).setContentSize(1080, 140);
        this.biddingNode.setPosition(0, -560);
        this.biddingNode.active = false;
        this.makeButton(this.biddingNode, '不叫', new Vec3(-420, 0), () => this.humanBid(0), new Color(70, 70, 70));
        this.makeButton(this.biddingNode, '1 分', new Vec3(-140, 0), () => this.humanBid(1), new Color(50, 120, 70));
        this.makeButton(this.biddingNode, '2 分', new Vec3(140, 0), () => this.humanBid(2), new Color(50, 120, 70));
        this.makeButton(this.biddingNode, '3 分', new Vec3(420, 0), () => this.humanBid(3), new Color(200, 120, 30));
        // Toast
        this.toastLabel = this.makeLabel(canvas, 'Toast', '', 42, new Vec3(0, 300));
        this.toastLabel.node.active = false;
    }

    private makeLabel(parent: Node, name: string, text: string, size: number, pos: Vec3): Label {
        const go = new Node(name);
        go.parent = parent;
        go.addComponent(UITransform).setContentSize(600, 60);
        go.setPosition(pos);
        const label = go.addComponent(Label);
        label.string = text;
        label.fontSize = size;
        label.lineHeight = size + 6;
        label.color = Color.WHITE.clone();
        label.horizontalAlign = Label.HorizontalAlign.CENTER;
        label.verticalAlign = Label.VerticalAlign.CENTER;
        label.overflow = Label.Overflow.NONE;
        return label;
    }

    private makeButton(parent: Node, text: string, pos: Vec3, onClick: () => void, color: Color) {
        const go = new Node(`Btn${text}`);
        go.parent = parent;
        const tr = go.addComponent(UITransform);
        tr.setContentSize(240, 92);
        go.setPosition(pos);
        const sp = go.addComponent(Sprite);
        sp.type = Sprite.SizeMode.CUSTOM;
        sp.sizeMode = Sprite.SizeMode.CUSTOM;
        sp.spriteFrame = makeSolidSprite(Color.WHITE);
        sp.color = color;
        const labelNode = new Node('Label');
        labelNode.parent = go;
        labelNode.addComponent(UITransform).setContentSize(240, 92);
        const label = labelNode.addComponent(Label);
        label.string = text;
        label.fontSize = 38;
        label.lineHeight = 44;
        label.color = Color.WHITE.clone();
        label.horizontalAlign = Label.HorizontalAlign.CENTER;
        label.verticalAlign = Label.VerticalAlign.CENTER;
        const btn = go.addComponent(Button);
        btn.transition = Button.Transition.SCALE;
        btn.node.on(Button.EventType.CLICK, onClick);
        return go;
    }

    /** 一张牌 UI（返回节点；parent 定位由调用方负责）。 */
    private makeCardNode(card: Card, scale: number): Node {
        const go = new Node(`Card${card.id}`);
        const tr = go.addComponent(UITransform);
        tr.setContentSize(92 * scale, 128 * scale);
        const sp = go.addComponent(Sprite);
        sp.type = Sprite.SizeMode.CUSTOM;
        sp.sizeMode = Sprite.SizeMode.CUSTOM;
        sp.spriteFrame = CardAtlas.get(card.id) ?? makeSolidSprite(Color.WHITE);
        sp.color = CardAtlas.get(card.id) ? Color.WHITE : (card.isJoker ? new Color(235, 225, 250) : Color.WHITE);
        if (CardAtlas.get(card.id)) {
            // 图集模式下卡面已含全部内容，直接返回
            return go;
        }

        const isRed = card.suit === 1 || card.suit === 3 || card.isJoker; // ♥/♦/王
        const color = card.isJoker ? JOKER_RED : isRed ? RED : BLACK;

        const corner = new Node('Corner');
        corner.parent = go;
        corner.addComponent(UITransform).setContentSize(80 * scale, 60 * scale);
        corner.setPosition(new Vec3(-92 * scale / 2 + 14 * scale, 128 * scale / 2 - 18 * scale));
        const cl = corner.addComponent(Label);
        cl.string = card.isJoker ? (card.rank === 18 ? '小王' : '大王') : `${card.toString().slice(card.isJoker ? 0 : 1)}\n${card.toString().slice(0, 1)}`;
        cl.fontSize = 30 * scale;
        cl.lineHeight = 34 * scale;
        cl.color = color;
        cl.horizontalAlign = Label.HorizontalAlign.LEFT;

        const center = new Node('Center');
        center.parent = go;
        center.addComponent(UITransform).setContentSize(92 * scale, 128 * scale);
        const ct = center.addComponent(Label);
        ct.string = card.isJoker ? '🃏' : card.toString().slice(0, 1);
        ct.fontSize = 52 * scale;
        ct.lineHeight = 60 * scale;
        ct.color = color;
        ct.horizontalAlign = Label.HorizontalAlign.CENTER;
        ct.verticalAlign = Label.VerticalAlign.CENTER;
        return go;
    }

    // ================= 事件渲染 =================

    private newRound(humanPlays: boolean) {
        this.humanSeat = humanPlays ? 0 : -1;
        this.clearPlayed();
        this.clearHand();
        this.bottomCardNodes.forEach(n => n.destroy());
        this.bottomCardNodes = [];
        // 刷新积分头
        const scores = ScoreStore.load();
        this.scoreHeader.string = `第 ${ScoreStore.gameCount + 1} 局   我的积分 ${scores[0]}`;
        this.game.startGame();
    }

    private restart() { this.newRound(true); }

    private onCardsDealt(seat: number, hand: Card[]) {
        this.seatLabels[seat].string = `玩家${seat + 1}\n剩 17 张`;
        if (seat === this.humanSeat) this.renderHand();
    }

    private onBidMade(seat: number, score: number) {
        this.showToast(`玩家${seat + 1} ${score === 0 ? '不叫' : `叫 ${score} 分`}`);
        this.biddingNode.active = seat === this.humanSeat && this.game.phase === GamePhase.Bidding;
    }

    private onLandlord(seat: number, bottom: Card[]) {
        this.showToast(seat === this.humanSeat ? '你是地主！' : `玩家${seat + 1} 成为地主`);
        // 底牌展示
        for (const n of this.bottomCardNodes) n.destroy();
        this.bottomCardNodes = [];
        const scale = 0.5;
        bottom.forEach((card, i) => {
            const n = this.makeCardNode(card, scale);
            n.parent = this.multiplierLabel.node.parent!;
            n.setPosition(new Vec3((i - 1) * 105, 770));
            this.bottomCardNodes.push(n);
        });
        this.updateSeatLabels();
        if (seat === this.humanSeat) this.renderHand();
    }

    private onTurn(seat: number) {
        if (this.game.phase !== GamePhase.Playing) return;
        const yourTurn = seat === this.humanSeat;
        this.actionNode.active = yourTurn;
        this.biddingNode.active = false;
    }

    private onPlayed(seat: number, pattern: HandPattern) {
        this.showPlayed(seat, pattern.cards);
        this.updateSeatLabels();
        if (seat !== this.humanSeat) return;
        this.clearHand();
        this.renderHand();
    }

    private onPass(seat: number) {
        this.showToast(`玩家${seat + 1} 不出`);
    }

    private onSettled(r: GameResult) {
        this.actionNode.active = false;
        // 积分持久化
        const scores = ScoreStore.add(r.scoreDeltas);
        ScoreStore.incrementGames();
        // 结算弹窗（按钮驱动下一局，不再自动开新局）
        this.resultDialog.show(r, scores, this.humanSeat, () => this.newRound(true));
    }

    // ================= 玩家操作 =================

    private humanBid(score: number) {
        if (this.game.phase !== GamePhase.Bidding || this.game.bidTurnSeat !== this.humanSeat) return;
        this.game.bid(this.humanSeat, score);
    }

    private tryPlay() {
        if (this.game.currentSeat !== this.humanSeat) return;
        const hand = this.game.players[this.humanSeat].hand;
        const chosen = hand.filter(c => this.selected.has(c.id));
        if (!chosen.length) { this.showToast('请先选牌'); return; }
        try {
            this.game.playCards(this.humanSeat, chosen);
            this.selected.clear();
        } catch (e: any) {
            this.showToast(String(e.message ?? e));
        }
    }

    private tryPass() {
        try { this.game.pass(this.humanSeat); }
        catch (e: any) { this.showToast(String(e.message ?? e)); }
    }

    private onHint() {
        // 复用 AI 枚举：跟牌取最小可压，自由出牌取分解首组
        const seat = this.humanSeat;
        if (this.game.currentSeat !== seat) return;
        const free = this.game.isFreePlay(seat);
        if (free) {
            const whole = this.game.players[seat].hand;
            this.selected.clear();
            for (const c of whole) this.selected.add(c.id);
            this.renderHand();
            return;
        }
        import('./Core/AI/PlayAI').then(({ decidePlay }) => {
            const move = decidePlay(this.game, seat);
            this.selected.clear();
            if (move) for (const c of move) this.selected.add(c.id);
            this.renderHand();
        });
    }

    // ================= 渲染工具 =================

    private renderHand() {
        this.clearHand();
        const hand = this.game.players[this.humanSeat].hand;
        const n = hand.length;
        const overlap = n > 12 ? 52 : 62;
        const scale = 0.95;
        const totalW = 92 * scale + overlap * (n - 1);
        const startX = -totalW / 2 + (92 * scale) / 2;

        hand.forEach((card, i) => {
            const node = this.makeCardNode(card, scale);
            node.parent = this.handNode;
            const x = startX + i * overlap;
            const y = this.selected.has(card.id) ? 40 : 0;
            node.setPosition(new Vec3(x, y));
            // 命中区放大
            node.addComponent(Button);
            node.on(Node.EventType.TOUCH_END, () => {
                if (this.selected.has(card.id)) this.selected.delete(card.id);
                else this.selected.add(card.id);
                this.renderHand();
            });
            this.cardNodes.set(card.id, node);
        });
    }

    private clearHand() {
        for (const [, n] of this.cardNodes) n.destroy();
        this.cardNodes.clear();
    }

    private showPlayed(seat: number, cards: Card[]) {
        const area = this.playedNodes[seat];
        if (area) area.destroyAllChildren();
        const scale = cards.length > 12 ? 0.7 : cards.length > 5 ? 0.8 : 0.85;
        const overlap = cards.length > 8 ? 40 : cards.length > 5 ? 52 : 66;
        const w = 92 * scale;
        const totalW = w + overlap * (cards.length - 1);
        const startX = -totalW / 2 + w / 2;
        cards.forEach((card, i) => {
            const node = this.makeCardNode(card, scale);
            node.parent = area!;
            node.setPosition(new Vec3(startX + i * overlap, 0));
        });
    }

    private clearPlayed() {
        for (let seat = 0; seat < 3; seat++) {
            const area = this.playedNodes[seat];
            if (area) area.destroyAllChildren();
        }
    }

    private updateSeatLabels() {
        for (let seat = 0; seat < 3; seat++) {
            const p = this.game.players[seat];
            const role = p.isLandlord ? ' 👑' : '';
            this.seatLabels[seat].string = `玩家${seat + 1}${role}\n剩 ${p.handCount} 张`;
        }
        this.multiplierLabel.string = `倍数 x${this.game.multiplier}`;
    }

    private showToast(msg: string, seconds = 2) {
        this.toastLabel.string = msg;
        this.toastLabel.node.active = true;
        this.toastTime = seconds;
    }
}
