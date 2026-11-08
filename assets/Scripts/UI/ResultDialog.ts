import { _decorator, Component, Node, Color, UITransform, Sprite, SpriteFrame, Texture2D, ImageAsset, Label, Button, Vec3 } from 'cc';
import { GameResult } from '../Core/Flow/GameController';

const { ccclass } = _decorator;

/**
 * 结算弹窗：胜负 + 倍数明细 + 三方得分 + 再来一局。
 * 纯代码构建，无预制体。show() 后由 onRestart 回调驱动下一局。
 */
@ccclass('ResultDialog')
export class ResultDialog extends Component {
    private root: Node = null!;
    private titleLabel: Label = null!;
    private detailLabel: Label = null!;
    private restartHandler: (() => void) | null = null;

    start() { this.ensureBuilt(); }

    private ensureBuilt() {
        if (this.root) return;

        // 半透明遮罩层
        this.root = new Node('ResultDialog');
        this.root.parent = this.node;
        const tr = this.root.addComponent(UITransform);
        tr.setContentSize(1080, 1920);
        const mask = this.root.addComponent(Sprite);
        mask.type = Sprite.SizeMode.CUSTOM;
        mask.sizeMode = Sprite.SizeMode.CUSTOM;
        mask.spriteFrame = this.whiteFrame();
        mask.color = new Color(0, 0, 0, 200);

        // 标题
        this.titleLabel = this.makeLabel('Title', 72, new Vec3(0, 380));
        // 明细
        this.detailLabel = this.makeLabel('Detail', 38, new Vec3(0, 60));
        this.detailLabel.node.getComponent(UITransform)!.setContentSize(1000, 700);

        // 再来一局按钮
        const btn = new Node('BtnRestart');
        btn.parent = this.root;
        btn.addComponent(UITransform).setContentSize(360, 110);
        btn.setPosition(new Vec3(0, -380));
        const sp = btn.addComponent(Sprite);
        sp.type = Sprite.SizeMode.CUSTOM;
        sp.sizeMode = Sprite.SizeMode.CUSTOM;
        sp.spriteFrame = this.whiteFrame();
        sp.color = new Color(220, 130, 30);
        const bl = this.makeLabel('BtnLabel', 42, new Vec3(0, 0));
        bl.node.parent = btn;
        bl.string = '再来一局';
        btn.addComponent(Button);
        btn.on(Button.EventType.CLICK, () => {
            this.hide();
            this.restartHandler?.();
        });

        this.root.active = false;
    }

    show(result: GameResult, scores: number[], humanSeat: number, onRestart: () => void) {
        this.ensureBuilt();
        this.restartHandler = onRestart;

        const humanWin = result.landlordWin === (humanSeat === result.landlordSeat);
        this.titleLabel.string = humanWin ? '🎉 胜 利' : '你输了';
        this.titleLabel.color = humanWin
            ? new Color(255, 215, 80)
            : new Color(220, 120, 120);

        let msg = `底分 ${result.baseScore} × 倍数 ${result.multiplier}`;
        if (result.bombCount > 0) msg += `（炸弹×${result.bombCount}）`;
        if (result.spring) msg += '（春天）';
        msg += '\n\n';
        for (let seat = 0; seat < 3; seat++) {
            const role = seat === result.landlordSeat ? '地主' : '农民';
            const you = seat === humanSeat ? '（你）' : '';
            const delta = result.scoreDeltas[seat];
            msg += `玩家${seat + 1}${you} [${role}]  ${delta > 0 ? '+' : ''}${delta}   累计 ${scores[seat]}\n`;
        }
        this.detailLabel.string = msg;

        this.root.active = true;
    }

    hide() {
        if (this.root) this.root.active = false;
    }

    private makeLabel(name: string, size: number, pos: Vec3): Label {
        const go = new Node(name);
        go.parent = this.root;
        go.addComponent(UITransform).setContentSize(900, 90);
        go.setPosition(pos);
        const label = go.addComponent(Label);
        label.fontSize = size;
        label.lineHeight = size + 10;
        label.color = Color.WHITE.clone();
        label.horizontalAlign = Label.HorizontalAlign.CENTER;
        label.verticalAlign = Label.VerticalAlign.CENTER;
        label.overflow = Label.Overflow.NONE;
        return label;
    }

    private whiteFrame(): SpriteFrame {
        const img = new ImageAsset();
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
}
