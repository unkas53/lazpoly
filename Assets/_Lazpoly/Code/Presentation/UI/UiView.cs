using System;

namespace Lazpoly.Presentation.UI
{
    /// <summary>
    /// Redraws a container's content when its key changes or something asked for a redraw. Screens
    /// call <see cref="Refresh"/> every frame with a key made from what they show; building happens
    /// only when that key changes, so text fields keep their focus between changes.
    /// </summary>
    public sealed class UiView
    {
        private readonly UiBuilder _builder;
        private readonly Action<UiBuilder> _render;
        private int _key;
        private bool _dirty = true;

        public UiView(UiBuilder builder, Action<UiBuilder> render)
        {
            _builder = builder ?? throw new ArgumentNullException(nameof(builder));
            _render = render ?? throw new ArgumentNullException(nameof(render));
            _builder.Changed = Invalidate;
        }

        public int BuildCount { get; private set; }

        public void Invalidate() => _dirty = true;

        /// <returns>True when the content was rebuilt.</returns>
        public bool Refresh(int key)
        {
            if (!_dirty && key == _key) return false;
            _key = key;
            _dirty = false;
            _builder.Clear();
            _render(_builder);
            BuildCount++;
            return true;
        }
    }
}
