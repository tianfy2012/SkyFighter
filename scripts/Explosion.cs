using Godot;

/// <summary>
/// 爆炸动画（AnimatedSprite2D）：
/// - 运行时用 4 张 @export 帧贴图构建 SpriteFrames（顺序 exp1→exp2→exp3→exp4）；
/// - 不循环；监听 AnimationFinished 信号，播放完毕执行 queue_free()。
/// 场景中 process_mode=Always，保证游戏结束暂停后动画仍能播完。
/// </summary>
public partial class Explosion : AnimatedSprite2D
{
    [Export] public Texture2D Frame1;
    [Export] public Texture2D Frame2;
    [Export] public Texture2D Frame3;
    [Export] public Texture2D Frame4;
    [Export] public float FrameDuration = 0.1f;

    public override void _Ready()
    {
        var frames = new SpriteFrames();
        // Godot 4.7 中 new SpriteFrames() 自带空的 "default" 动画，无需 AddAnimation。
        frames.AddFrame("default", Frame1);
        frames.AddFrame("default", Frame2);
        frames.AddFrame("default", Frame3);
        frames.AddFrame("default", Frame4);
        frames.SetAnimationSpeed("default", 1f / FrameDuration);
        frames.SetAnimationLoopMode("default", SpriteFrames.LoopMode.None);

        SpriteFrames = frames;
        Play("default");

        AnimationFinished += OnAnimationFinished;
    }

    private void OnAnimationFinished()
    {
        QueueFree();
    }
}