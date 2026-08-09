using Wobble.Screens;

namespace Wobble.Tests.Screens.Tests.BackgroundClipping
{
    public sealed class TestBackgroundClippingScreen : Screen
    {
        public sealed override ScreenView View { get; protected set; }

        public TestBackgroundClippingScreen() => View = new TestBackgroundClippingScreenView(this);
    }
}
