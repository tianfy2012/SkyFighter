using Godot;

/// <summary>
/// 背景：两张 bg_tile 上下无缝拼接，匀速向下滚动，超出视口后循环回顶部。
/// 画面尺寸为 800x600，与 bg_tile.svg(800x600) 完全吻合，因此两张瓦片即可无缝覆盖。
/// </summary>
public partial class Background : Node2D
{
    [Export] public Texture2D BgTexture;
    [Export] public float ScrollSpeed = 140f;

    private Sprite2D _top;
    private Sprite2D _bottom;
    private float _tileHeight;

    public override void _Ready()
    {
        _top = GetNode<Sprite2D>("Top");
        _bottom = GetNode<Sprite2D>("Bottom");

        _tileHeight = BgTexture != null ? BgTexture.GetHeight() : GetViewportRect().Size.Y;

        foreach (var sprite in new[] { _top, _bottom })
        {
            sprite.Texture = BgTexture;
            sprite.Centered = false;
        }

        _top.Position = new Vector2(0f, -_tileHeight);
        _bottom.Position = Vector2.Zero;
    }

    public override void _Process(double delta)
    {
        float step = ScrollSpeed * (float)delta;
        _top.Position += Vector2.Down * step;
        _bottom.Position += Vector2.Down * step;

        // 循环回卷：移出屏幕底部后，叠放到另一张瓦片正上方，保持无缝。
        if (_top.Position.Y >= _tileHeight)
            _top.Position = new Vector2(_top.Position.X, _bottom.Position.Y - _tileHeight);
        if (_bottom.Position.Y >= _tileHeight)
            _bottom.Position = new Vector2(_bottom.Position.X, _top.Position.Y - _tileHeight);
    }
}